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
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Optional

logger = logging.getLogger(__name__)

# About 40–85 MiB per 1024 cell. 64 GiB covers a tracing working set, not a whole volume.
DEFAULT_MAX_BYTES = 64 * 1024**3
_DIGEST_RE = re.compile(r"^[0-9a-f]{64}$")
_MODE_RE = re.compile(r"^[a-z0-9_-]{1,32}$")


def checkpoint_stamp(checkpoint_path: str, model_cfg: str) -> str:
    """Stable id for one weights file. A replaced checkpoint gets a new stamp."""
    try:
        stat = os.stat(checkpoint_path)
        raw = f"{model_cfg}|{stat.st_size}|{stat.st_mtime_ns}"
    except OSError:
        raw = f"{model_cfg}|missing|{checkpoint_path}"
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()[:16]


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
        self._index_existing()

    @property
    def total_bytes(self) -> int:
        return self._total_bytes

    def touch(self, digest: str, mode: str) -> None:
        """Refresh mtime so a GPU-hot cell is not the first disk eviction."""
        path = self._path(digest, mode)
        if path is None or not path.is_file():
            return
        now = time.time()
        try:
            os.utime(path, (now, now))
        except OSError:
            return
        with self._lock:
            record = self._files.get(str(path))
            if record is not None:
                record.mtime = now

    def try_load(self, digest: str, mode: str) -> Optional[bytes]:
        """Return the blob for this image hash, or None on miss or a bad file."""
        path = self._path(digest, mode)
        if path is None or not path.is_file():
            return None
        try:
            data = path.read_bytes()
        except OSError:
            logger.warning("Embedding disk read failed path=%s", path)
            return None
        self.touch(digest, mode)
        return data

    def save(self, digest: str, blob: bytes, mode: str) -> None:
        """Write ``blob`` atomically, then evict oldest files until under the cap."""
        path = self._path(digest, mode)
        if path is None:
            raise ValueError(f"invalid embedding key digest={digest!r} mode={mode!r}")
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
