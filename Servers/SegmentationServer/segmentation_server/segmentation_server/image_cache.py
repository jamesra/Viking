"""In-memory image cache with LRU eviction, TTL expiration, and per-image predictors."""

from __future__ import annotations

import asyncio
import logging
import threading
import time
from dataclasses import dataclass, field
from typing import Any, Callable, Dict, Optional, Tuple

logger = logging.getLogger(__name__)


class PredictorCreationError(RuntimeError):
    """Raised when a cached image cannot get an initialized predictor."""


@dataclass
class CachedImage:
    """One cached upload and its optional SAM2 predictor."""

    image_data: bytes
    width: int
    height: int
    last_access_time: float
    upload_time: float
    size_bytes: int
    predictor: Optional[Any] = field(default=None)
    predictor_lock: threading.Lock = field(default_factory=threading.Lock)


class ImageCache:
    """Thread-safe image cache with LRU eviction and TTL expiration.

    Cache mutations take an asyncio.Lock. Predictor inference uses a per-image
    threading.Lock because it runs in executor threads. Only encoded image bytes
    count toward max_memory_bytes; GPU embeddings are not included.
    """

    def __init__(
        self,
        max_memory_bytes: int = 1073741824,
        ttl_seconds: int = 300,
        create_predictor_func: Optional[Callable[[bytes], Any]] = None,
        time_fn: Callable[[], float] = time.time,
    ) -> None:
        """
        Args:
            max_memory_bytes: Cap on sum of cached image_data lengths.
            ttl_seconds: Evict entries unused for this many seconds.
            create_predictor_func: bytes -> initialized predictor. If None, images
                are stored without predictors (tests / decode-only use).
            time_fn: Clock for TTL/LRU; inject a fake in tests.
        """
        self._cache: Dict[int, CachedImage] = {}
        self._next_id: int = 1
        self._lock: asyncio.Lock = asyncio.Lock()
        self._max_memory_bytes: int = max_memory_bytes
        self._ttl_seconds: int = ttl_seconds
        self._current_memory_bytes: int = 0
        self._create_predictor_func = create_predictor_func
        self._time_fn = time_fn

        logger.info(
            "ImageCache initialized with max_memory=%s bytes (%.2f GB), TTL=%ss",
            max_memory_bytes,
            max_memory_bytes / (1024**3),
            ttl_seconds,
        )

    def _create_predictor_in_executor(self, image_data: bytes, image_id: int) -> Any:
        """Build the predictor off the event loop. Returns None on failure."""
        if not self._create_predictor_func:
            logger.warning("Cannot create predictor for image ID=%s - function not set", image_id)
            return None
        try:
            predictor = self._create_predictor_func(image_data)
            logger.info("Predictor created for image ID=%s", image_id)
            return predictor
        except Exception:
            logger.exception("Error creating predictor for image ID=%s", image_id)
            return None

    async def upload_image(
        self,
        image_data: bytes,
        width: int,
        height: int,
        executor: Optional[Any] = None,
    ) -> int:
        """Store an image, initialize its predictor, and return a new ID.

        Raises:
            PredictorCreationError: Predictor setup failed, or the entry was evicted
                before setup finished. The image is not left in the cache on failure.
        """
        async with self._lock:
            await self._cleanup_expired()
            size_bytes = len(image_data)
            while self._current_memory_bytes + size_bytes > self._max_memory_bytes and self._cache:
                await self._evict_oldest()

            image_id = self._next_id
            self._next_id += 1
            now = self._time_fn()
            self._cache[image_id] = CachedImage(
                image_data=image_data,
                width=width,
                height=height,
                last_access_time=now,
                upload_time=now,
                size_bytes=size_bytes,
            )
            self._current_memory_bytes += size_bytes
            logger.info(
                "Image uploaded: ID=%s, size=%s bytes, total_cache=%s bytes (%.2f MB), count=%s",
                image_id,
                size_bytes,
                self._current_memory_bytes,
                self._current_memory_bytes / (1024**2),
                len(self._cache),
            )

        if self._create_predictor_func:
            predictor = await asyncio.get_running_loop().run_in_executor(
                executor,
                self._create_predictor_in_executor,
                image_data,
                image_id,
            )
            async with self._lock:
                cached = self._cache.get(image_id)
                if cached is None:
                    raise PredictorCreationError(
                        f"Image ID={image_id} was evicted before predictor could be created"
                    )
                if predictor is None:
                    await self._delete_image_internal(image_id)
                    raise PredictorCreationError(
                        f"Failed to create predictor for image ID={image_id}"
                    )
                cached.predictor = predictor

        return image_id

    async def get_image(
        self, image_id: int
    ) -> Optional[Tuple[bytes, int, int, Optional[Any], threading.Lock]]:
        """Return (bytes, width, height, predictor, lock) and refresh LRU, or None if missing."""
        async with self._lock:
            await self._cleanup_expired()
            cached_image = self._cache.get(image_id)
            if cached_image is None:
                logger.info("Image not found: ID=%s", image_id)
                return None

            cached_image.last_access_time = self._time_fn()
            logger.debug(
                "Image retrieved: ID=%s, size=%s bytes, predictor=%s",
                image_id,
                cached_image.size_bytes,
                "ready" if cached_image.predictor is not None else "pending",
            )
            return (
                cached_image.image_data,
                cached_image.width,
                cached_image.height,
                cached_image.predictor,
                cached_image.predictor_lock,
            )

    async def _delete_image_internal(self, image_id: int) -> bool:
        """Remove an entry. Caller must hold `_lock`."""
        cached_image = self._cache.pop(image_id, None)
        if cached_image is None:
            return False
        self._current_memory_bytes -= cached_image.size_bytes
        return True

    async def delete_image(self, image_id: int) -> bool:
        """Delete by ID. Returns False if the ID was not present."""
        async with self._lock:
            if not await self._delete_image_internal(image_id):
                logger.info("Image deletion failed (not found): ID=%s", image_id)
                return False
            logger.info(
                "Image deleted: ID=%s, total_cache=%s bytes (%.2f MB), count=%s",
                image_id,
                self._current_memory_bytes,
                self._current_memory_bytes / (1024**2),
                len(self._cache),
            )
            return True

    async def _evict_oldest(self) -> None:
        """Drop the LRU entry. Caller must hold `_lock`."""
        if not self._cache:
            return
        oldest_id = min(self._cache.keys(), key=lambda k: self._cache[k].last_access_time)
        await self._delete_image_internal(oldest_id)
        logger.info(
            "Image evicted (LRU): ID=%s, total_cache=%s bytes (%.2f MB), count=%s",
            oldest_id,
            self._current_memory_bytes,
            self._current_memory_bytes / (1024**2),
            len(self._cache),
        )

    async def _cleanup_expired(self) -> None:
        """Drop entries unused longer than TTL. Caller must hold `_lock`."""
        current_time = self._time_fn()
        expired_ids = [
            image_id
            for image_id, cached_image in self._cache.items()
            if current_time - cached_image.last_access_time > self._ttl_seconds
        ]
        for image_id in expired_ids:
            cached_image = self._cache.get(image_id)
            age = current_time - cached_image.last_access_time if cached_image else 0
            await self._delete_image_internal(image_id)
            logger.info("Image expired (TTL): ID=%s, age=%.1fs", image_id, age)
        if expired_ids:
            logger.info(
                "Cleanup complete: removed %s expired images, total_cache=%s bytes (%.2f MB), count=%s",
                len(expired_ids),
                self._current_memory_bytes,
                self._current_memory_bytes / (1024**2),
                len(self._cache),
            )

    async def get_stats(self) -> Dict[str, Any]:
        """Snapshot of occupancy for GetServerStatus."""
        async with self._lock:
            return {
                "total_images": len(self._cache),
                "total_memory_bytes": self._current_memory_bytes,
                "total_memory_mb": self._current_memory_bytes / (1024**2),
                "max_memory_bytes": self._max_memory_bytes,
                "max_memory_mb": self._max_memory_bytes / (1024**2),
                "memory_usage_percent": (
                    self._current_memory_bytes / self._max_memory_bytes * 100
                    if self._max_memory_bytes > 0
                    else 0
                ),
                "ttl_seconds": self._ttl_seconds,
            }
