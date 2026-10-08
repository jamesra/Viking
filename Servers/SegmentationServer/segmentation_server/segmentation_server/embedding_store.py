"""Disk store for SAM2 tile embeddings.

The GPU cache holds a few dozen live predictors. This store keeps the per-image
feature maps (not the network weights) so a later upload of the same pixels can
skip ``set_image()``. Files are keyed by the checkpoint identity, the encoder
generation (eager vs compiled), and a hash of the image bytes.

Callers pass opaque blobs. The segmentation model owns the tensor format.
"""

from __future__ import annotations

import hashlib
import logging
import os
import re
import shutil
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Optional

logger = logging.getLogger(__name__)

# About 40–85 MiB per 1024 cell. 32 GiB covers a tracing working set, not a whole volume.
DEFAULT_MAX_BYTES = 32 * 1024**3
_DIGEST_RE = re.compile(r"^[0-9a-f]{64}$")
_MODE_RE = re.compile(r"^[a-z0-9_-]{1,32}$")
# Current stamps are 32 hex characters. Shorter ones from older releases still match so that
# their folders are purged once instead of lingering.
_STAMP_RE = re.compile(r"^[0-9a-f]{16,64}$")
# How an embedding is keyed inside a stamp's folder. It is part of the stamp, so changing it
# makes every older folder look like another checkpoint's and the next start purges it. Version 2
# keys on a hash of the decoded pixels; version 1 hashed the encoded PNG bytes.
KEY_SCHEME = "pixels-v2"
# No real embedding is near this. A bigger file is not read into memory.
MAX_BLOB_BYTES = 1024**3


def _directory_bytes(directory: Path) -> int:
    """Total size of the files under ``directory``. Unreadable entries count as zero."""
    total = 0
    for dirpath, _dirnames, filenames in os.walk(directory):
        for name in filenames:
            try:
                total += (Path(dirpath) / name).stat().st_size
            except OSError:
                continue
    return total


def checkpoint_stamp(checkpoint_path: str, model_cfg: str) -> str:
    """Stable id for one weights file. A replaced checkpoint gets a new stamp."""
    try:
        stat = os.stat(checkpoint_path)
        raw = f"{KEY_SCHEME}|{model_cfg}|{stat.st_size}|{stat.st_mtime_ns}"
    except OSError:
        raw = f"{KEY_SCHEME}|{model_cfg}|missing|{checkpoint_path}"
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()[:32]


def open_embedding_store(checkpoint_path: str, model_cfg: str) -> Optional["EmbeddingStore"]:
    """Open the store when ``SEGMENTATION_EMBEDDING_CACHE`` is set. Otherwise None.

    ``SEGMENTATION_EMBEDDING_CACHE_MAX_BYTES`` caps the tree. ``0`` means no cap.
    An unreadable directory disables the store rather than failing server startup.
    """
    root = os.environ.get("SEGMENTATION_EMBEDDING_CACHE", "").strip()
    if not root:
        logger.info("Embedding disk cache disabled (SEGMENTATION_EMBEDDING_CACHE unset)")
        return None
    raw_max = os.environ.get("SEGMENTATION_EMBEDDING_CACHE_MAX_BYTES", "").strip()
    if raw_max == "":
        max_bytes: Optional[int] = DEFAULT_MAX_BYTES
    else:
        try:
            parsed = int(raw_max)
        except ValueError:
            logger.warning(
                "Invalid SEGMENTATION_EMBEDDING_CACHE_MAX_BYTES=%r; using %s",
                raw_max,
                DEFAULT_MAX_BYTES,
            )
            max_bytes = DEFAULT_MAX_BYTES
        else:
            max_bytes = None if parsed <= 0 else parsed
    stamp = checkpoint_stamp(checkpoint_path, model_cfg)
    try:
        store = EmbeddingStore(root, stamp, max_bytes=max_bytes)
    except OSError:
        logger.exception("Embedding disk cache unavailable at %s", root)
        return None
    cap = "none" if max_bytes is None else f"{max_bytes / (1024**3):.1f} GiB"
    logger.info(
        "Embedding disk cache root=%s stamp=%s files=%s bytes=%s cap=%s",
        root,
        stamp,
        len(store._files),
        store.total_bytes,
        cap,
    )
    return store


@dataclass
class _FileRecord:
    path: Path
    size: int
    mtime: float


class EmbeddingStore:
    """LRU directory of embedding blobs.

    ``mode`` separates eager-encoder files from compiled-encoder files. A load
    refreshes mtime so a cell that is still in use is not the first eviction.
    """

    def __init__(self, root: str, stamp: str, max_bytes: Optional[int]) -> None:
        self._root = Path(root)
        self._stamp = stamp
        self._max_bytes = max_bytes
        self._lock = threading.Lock()
        self._files: Dict[str, _FileRecord] = {}
        self._total_bytes = 0
        self._root.mkdir(parents=True, exist_ok=True)
        self._purge_other_stamps()
        self._index_existing()

    @property
    def total_bytes(self) -> int:
        return self._total_bytes

    def touch(self, digest: str, mode: str) -> None:
        """Refresh mtime so a GPU-hot cell is not the first disk eviction.

        Runs under the store lock, the same lock eviction holds, so a file cannot be evicted
        between being found and being refreshed. A file that is indexed but gone from disk is
        dropped from the index.
        """
        path = self._path(digest, mode)
        if path is None:
            return
        with self._lock:
            if not path.is_file():
                self._delete(str(path))
                return
            now = time.time()
            try:
                os.utime(path, (now, now))
            except OSError:
                self._delete(str(path))
                return
            record = self._files.get(str(path))
            if record is not None:
                record.mtime = now

    def try_load(self, digest: str, mode: str) -> Optional[bytes]:
        """Return the blob for this image hash, or None on miss, a bad file, or an absurd size.

        A file that cannot be read, or is indexed but missing, leaves the index so its size no
        longer counts toward the cap.
        """
        path = self._path(digest, mode)
        if path is None:
            return None
        try:
            size = path.stat().st_size
        except OSError:
            with self._lock:
                self._delete(str(path))
            return None
        if size > MAX_BLOB_BYTES:
            logger.warning("Embedding disk file too large to load path=%s bytes=%s", path, size)
            with self._lock:
                self._delete(str(path))
            return None
        try:
            data = path.read_bytes()
        except OSError:
            logger.warning("Embedding disk read failed path=%s", path)
            with self._lock:
                self._delete(str(path))
            return None
        self.touch(digest, mode)
        return data

    def save(self, digest: str, blob: bytes, mode: str) -> bool:
        """Write ``blob`` atomically, then evict oldest files until under the cap.

        A blob bigger than the whole cap is not written: it could never fit, and writing it
        would leave the store permanently over the cap. Returns True when the blob was stored.
        """
        path = self._path(digest, mode)
        if path is None:
            raise ValueError(f"invalid embedding key digest={digest!r} mode={mode!r}")
        if self._max_bytes is not None and len(blob) > self._max_bytes:
            logger.warning(
                "Embedding disk blob not stored: %s bytes is larger than the %s byte cap (path=%s)",
                len(blob),
                self._max_bytes,
                path,
            )
            return False
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(path.suffix + ".tmp")
        temporary.write_bytes(blob)
        os.replace(temporary, path)
        stat = path.stat()
        with self._lock:
            previous = self._files.get(str(path))
            if previous is not None:
                self._total_bytes -= previous.size
            self._files[str(path)] = _FileRecord(path=path, size=stat.st_size, mtime=stat.st_mtime)
            self._total_bytes += stat.st_size
            self._evict_oldest(protected=str(path))
        return True

    def discard(self, digest: str, mode: str) -> None:
        """Remove one blob. Used when a file cannot be decoded."""
        path = self._path(digest, mode)
        if path is None:
            return
        with self._lock:
            self._delete(str(path))

    def _path(self, digest: str, mode: str) -> Optional[Path]:
        if not _DIGEST_RE.match(digest) or not _MODE_RE.match(mode):
            return None
        return self._root / self._stamp / mode / digest[:2] / f"{digest}.pt"

    def _purge_other_stamps(self) -> None:
        """Delete cache folders left by other checkpoints.

        The stamp is part of every path, so once the weights file is replaced the old stamp's
        files can never be read again, yet nothing else removes them and they would sit on disk
        until the byte cap evicted them by age. Only direct children whose names look like a stamp
        (see ``checkpoint_stamp``) are removed, so anything else sharing a mounted cache folder is
        left alone. This assumes one server owns this cache root: a second server running another
        checkpoint against the same folder would have its cache removed at this server's startup.
        """
        try:
            children = list(self._root.iterdir())
        except OSError:
            logger.warning("Embedding disk cache could not list %s to purge old stamps", self._root)
            return
        for child in children:
            if child.name == self._stamp or not _STAMP_RE.match(child.name) or not child.is_dir():
                continue
            freed = _directory_bytes(child)
            try:
                shutil.rmtree(child)
            except OSError:
                logger.warning("Embedding disk cache could not remove old stamp %s", child, exc_info=True)
                continue
            logger.info(
                "Embedding disk cache removed old stamp %s (current %s) freed=%s bytes",
                child.name,
                self._stamp,
                freed,
            )

    def _index_existing(self) -> None:
        if not self._root.is_dir():
            return
        for dirpath, _dirnames, filenames in os.walk(self._root):
            for name in filenames:
                if not name.endswith(".pt"):
                    continue
                path = Path(dirpath) / name
                try:
                    stat = path.stat()
                except OSError:
                    continue
                self._files[str(path)] = _FileRecord(path=path, size=stat.st_size, mtime=stat.st_mtime)
                self._total_bytes += stat.st_size

    def _evict_oldest(self, protected: str) -> None:
        """Drop the oldest other files until under the cap. ``protected`` was just written."""
        if self._max_bytes is None:
            return
        while self._total_bytes > self._max_bytes:
            candidates = [key for key in self._files if key != protected]
            if not candidates:
                return
            oldest_key = min(candidates, key=lambda key: self._files[key].mtime)
            record = self._files[oldest_key]
            logger.info(
                "Embedding disk evict path=%s bytes=%s",
                record.path,
                record.size,
            )
            self._delete(oldest_key)

    def _delete(self, key: str) -> None:
        record = self._files.pop(key, None)
        if record is None:
            return
        self._total_bytes -= record.size
        try:
            record.path.unlink(missing_ok=True)
        except OSError:
            logger.warning("Embedding disk delete failed path=%s", record.path)
