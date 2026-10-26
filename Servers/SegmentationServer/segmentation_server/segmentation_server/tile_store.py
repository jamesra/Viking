"""Encoded grid tiles kept after the image cache drops them, so the server can rebuild a tile without its client.

The image cache holds a tile only while it holds the tile's GPU embedding, and GPU pressure drops both. The encoded
bytes are small next to the embedding (a 1024 PNG is one to three MiB; the embedding is about 9 MiB on the GPU and 40
to 85 MiB on disk), so they are kept here for longer: in host memory, least recently used first, and on disk under the
embedding cache root when one is configured. A tile found here goes back into the image cache, where the disk
embedding store usually skips the encoder as well.

Tiles are keyed by the whole ``TileCacheKey``. Two uploads with one key are taken to be the same picture, so the
client's key must name everything that changes the render.
"""

from __future__ import annotations

import hashlib
import logging
import os
import threading
from collections import OrderedDict
from pathlib import Path
from typing import Optional

from segmentation_server.embedding_store import EmbeddingStore
from segmentation_server.image_cache import TileCacheKey

logger = logging.getLogger(__name__)

DEFAULT_RAM_BYTES = 2 * 1024**3
DEFAULT_DISK_BYTES = 8 * 1024**3
# How a key becomes a file name. It is the disk store's stamp, so changing it purges the files made the old way.
KEY_SCHEME = "tile-key-v2"
_MODE = "png"
_SUFFIX = ".png"


def _key_digest(key: TileCacheKey) -> str:
    return hashlib.sha256("\x1f".join(str(part) for part in key).encode("utf-8")).hexdigest()


class TileStore:
    """Encoded tiles by key: host memory first (bounded by bytes), then an optional disk store.

    Thread safe. :meth:`recall` and :meth:`remember` only use memory, so they are cheap enough for the event loop;
    :meth:`load`, :meth:`persist`, :meth:`touch` and :meth:`forget` touch the disk and belong on a worker thread.
    """

    def __init__(self, ram_bytes: int, disk: Optional[EmbeddingStore] = None) -> None:
        self._ram_bytes = max(0, ram_bytes)
        self._disk = disk
        self._entries: "OrderedDict[TileCacheKey, bytes]" = OrderedDict()
        self._held_bytes = 0
        self._lock = threading.Lock()

    @property
    def has_disk(self) -> bool:
        return self._disk is not None

    @property
    def held_bytes(self) -> int:
        """Bytes held in memory."""
        return self._held_bytes

    def recall(self, key: TileCacheKey) -> Optional[bytes]:
        """The tile's bytes from memory, now the most recently used, or None."""
        with self._lock:
            data = self._entries.get(key)
            if data is not None:
                self._entries.move_to_end(key)
            return data

    def contains(self, key: TileCacheKey) -> bool:
        """True when the tile is held in memory or listed on disk. Cheap enough for the event loop: it reads no
        file and leaves the memory order as it is, so asking does not keep a tile."""
        with self._lock:
            if key in self._entries:
                return True
        return self._disk is not None and self._disk.contains(_key_digest(key), _MODE)

    def load(self, key: TileCacheKey) -> Optional[bytes]:
        """The tile's bytes from memory, else from disk (and then kept in memory), or None."""
        data = self.recall(key)
        if data is not None or self._disk is None:
            return data
        data = self._disk.try_load(_key_digest(key), _MODE)
        if data:
            self.remember(key, data)
            logger.info("Tile disk hit key=%s bytes=%s", key, len(data))
            return data
        return None

    def persist(self, key: TileCacheKey, data: bytes) -> None:
        """Write ``data`` to disk as the tile for ``key``. A failed write is logged, not raised."""
        if not data or self._disk is None:
            return
        try:
            self._disk.save(_key_digest(key), data, _MODE)
        except Exception:
            logger.exception("Tile disk write failed key=%s", key)

    def touch(self, key: TileCacheKey) -> None:
        """Mark the tile's disk file as just used, so a tile the image cache keeps hot is not the first disk eviction."""
        if self._disk is not None:
            self._disk.touch(_key_digest(key), _MODE)

    def forget(self, key: TileCacheKey) -> None:
        """Drop the tile everywhere, for example when its bytes would not decode."""
        with self._lock:
            data = self._entries.pop(key, None)
            if data is not None:
                self._held_bytes -= len(data)
        if self._disk is not None:
            self._disk.discard(_key_digest(key), _MODE)

    def remember(self, key: TileCacheKey, data: bytes) -> None:
        """Keep ``data`` in memory as the tile for ``key``, dropping the least recently used tiles over the cap."""
        if not data or len(data) > self._ram_bytes:
            return
        with self._lock:
            previous = self._entries.pop(key, None)
            if previous is not None:
                self._held_bytes -= len(previous)
            self._entries[key] = data
            self._held_bytes += len(data)
            while self._held_bytes > self._ram_bytes:
                _oldest, dropped = self._entries.popitem(last=False)
                self._held_bytes -= len(dropped)


def _read_bytes(name: str, default: int) -> int:
    raw = os.environ.get(name, "").strip()
    if not raw:
        return default
    try:
        return int(raw)
    except ValueError:
        logger.warning("Invalid %s=%r; using %s", name, raw, default)
        return default


def open_tile_store() -> TileStore:
    """The tile store the environment asks for.

    ``SEGMENTATION_TILE_RAM_BYTES`` caps the host memory (default 2 GiB, ``0`` keeps nothing in memory). Tiles also
    go to disk under ``<SEGMENTATION_EMBEDDING_CACHE>/tiles`` when the embedding disk cache is on, capped by
    ``SEGMENTATION_TILE_CACHE_MAX_BYTES`` (default 8 GiB, ``0`` for no cap). An unusable directory leaves the store in
    memory only rather than failing startup.
    """
    ram_bytes = max(0, _read_bytes("SEGMENTATION_TILE_RAM_BYTES", DEFAULT_RAM_BYTES))
    disk: Optional[EmbeddingStore] = None
    root = os.environ.get("SEGMENTATION_EMBEDDING_CACHE", "").strip()
    if root:
        max_bytes = _read_bytes("SEGMENTATION_TILE_CACHE_MAX_BYTES", DEFAULT_DISK_BYTES)
        stamp = hashlib.sha256(KEY_SCHEME.encode("utf-8")).hexdigest()[:32]
        try:
            disk = EmbeddingStore(
                str(Path(root) / "tiles"), stamp, max_bytes=None if max_bytes <= 0 else max_bytes, suffix=_SUFFIX
            )
        except OSError:
            logger.exception("Tile disk cache unavailable under %s", root)
    logger.info(
        "Tile store holds %.1f GiB in memory; disk %s",
        ram_bytes / 1024**3,
        "off" if disk is None else f"{Path(root) / 'tiles'} ({disk.total_bytes} bytes)",
    )
    return TileStore(ram_bytes, disk)
