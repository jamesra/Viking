"""In-memory image cache with two retention modes.

Arbitrary uploads (one client, one image id) use LRU, a short TTL, and an entry
cap. Shared tiles are the same cell for every client, so they stay until the
encoded-byte cap or a GPU-memory check says an embedding has to be dropped.
"""

from __future__ import annotations

import asyncio
import logging
import threading
import time
from dataclasses import dataclass, field
from typing import Any, Callable, Dict, Optional, Tuple

from segmentation_server.cuda_errors import UnrecoverableGpuError
from segmentation_server.tile_work import tile_work

logger = logging.getLogger(__name__)

DEFAULT_MAX_MEMORY_BYTES = 1073741824
DEFAULT_TTL_SECONDS = 300
DEFAULT_MAX_ENTRIES = 32

# volume, section, channel, transform, downsample, row, col
TileCacheKey = Tuple[str, int, str, str, int, int, int]


class PredictorCreationError(RuntimeError):
    """Raised when a cached image cannot get an initialized predictor."""


class CacheFullError(RuntimeError):
    """Raised when an upload cannot fit under the byte cap even after every idle entry is evicted.

    What is left is held by in-flight requests (pinned entries, and entries retiring after a
    delete while still pinned). The caller should answer ``RESOURCE_EXHAUSTED``; the client can
    retry once those requests finish.
    """


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
    in_use: int = 0
    tile_key: Optional[TileCacheKey] = None
    # Shared grid cell. Exempt from the arbitrary-upload entry cap and TTL.
    shared_tile: bool = False


class ImageCache:
    """Thread-safe image cache with LRU eviction and TTL expiration.

    Cache mutations take an asyncio.Lock. Predictor inference uses a per-image
    threading.Lock because it runs in executor threads. Encoded image bytes count
    toward max_memory_bytes for both modes. max_entries and the TTL apply only to
    arbitrary uploads. Shared tiles are evicted when that byte cap is exceeded or
    when gpu_under_pressure reports that the next embedding needs the GPU.
    """

    def __init__(
        self,
        max_memory_bytes: int = DEFAULT_MAX_MEMORY_BYTES,
        ttl_seconds: int = DEFAULT_TTL_SECONDS,
        max_entries: int = DEFAULT_MAX_ENTRIES,
        create_predictor_func: Optional[Callable[[bytes], Any]] = None,
        release_predictor_func: Optional[Callable[[Any], None]] = None,
        time_fn: Callable[[], float] = time.time,
        gpu_under_pressure: Optional[Callable[[], bool]] = None,
        predictor_generation: Optional[Callable[[Any], Optional[str]]] = None,
        current_generation: Optional[Callable[[], Optional[str]]] = None,
    ) -> None:
        """
        Args:
            max_memory_bytes: Cap on sum of cached image_data lengths.
            ttl_seconds: Evict arbitrary uploads unused for this many seconds.
                Shared tiles are not expired by the TTL.
            max_entries: Cap on arbitrary uploads. Shared tiles do not count.
            create_predictor_func: bytes -> initialized predictor. If None, images
                are stored without predictors (tests / decode-only use).
            release_predictor_func: Called with a predictor before its entry is
                dropped (delete, LRU, TTL). ImageCache stays torch-free.
            time_fn: Clock for TTL/LRU; inject a fake in tests.
            gpu_under_pressure: Return True only when a shared-tile embedding
                should be dropped to free GPU memory. None disables that check.
            predictor_generation: predictor -> the encoder generation its embedding was
                made with (for example ``"eager"``), or None when unknown.
            current_generation: () -> the generation new embeddings are made with now.
                Together these let :meth:`flush_stale` drop only entries made with an
                encoder that has since been replaced.
        """
        self._cache: Dict[int, CachedImage] = {}
        # Deleted/evicted while SegmentImage still holds a pin; predictor reset waits for check-in.
        self._retiring: Dict[int, CachedImage] = {}
        self._coord_index: Dict[TileCacheKey, int] = {}
        self._next_id: int = 1
        self._lock: asyncio.Lock = asyncio.Lock()
        self._max_memory_bytes: int = max_memory_bytes
        self._ttl_seconds: int = ttl_seconds
        self._max_entries: int = max(1, max_entries)
        self._current_memory_bytes: int = 0
        # Entries removed from the live cache while a request still holds them. Their bytes stay
        # in memory until the last release, so they count toward the byte cap.
        self._retiring_bytes: int = 0
        self._create_predictor_func = create_predictor_func
        self._release_predictor_func = release_predictor_func
        self._time_fn = time_fn
        self._gpu_under_pressure_fn = gpu_under_pressure
        self._predictor_generation_fn = predictor_generation
        self._current_generation_fn = current_generation

        logger.info(
            "ImageCache initialized with max_memory=%s bytes (%.2f GB), "
            "max_entries=%s, TTL=%ss",
            max_memory_bytes,
            max_memory_bytes / (1024**3),
            self._max_entries,
            ttl_seconds,
        )

    def _create_predictor_in_executor(
        self,
        image_data: bytes,
        image_id: int,
        tile_key: Optional[TileCacheKey] = None,
        decoded: Optional[Any] = None,
    ) -> Any:
        """Build the predictor off the event loop. Returns None on failure.

        tile_key is set for shared mosaic cells so embedding logs name that cell.
        Arbitrary uploads leave it unset.
        """
        if tile_key is None:
            return self._create_predictor_body(image_data, image_id, decoded)
        volume, section, _channel, _transform, _downsample, row, col = tile_key
        with tile_work(volume, section, col, row):
            return self._create_predictor_body(image_data, image_id, decoded)

    def _create_predictor_body(self, image_data: bytes, image_id: int, decoded: Optional[Any] = None) -> Any:
        if not self._create_predictor_func:
            logger.warning("Cannot create predictor for image ID=%s - function not set", image_id)
            return None
        try:
            if decoded is None:
                predictor = self._create_predictor_func(image_data)
            else:
                predictor = self._create_predictor_func(image_data, decoded)
            logger.info("Predictor created for image ID=%s", image_id)
            return predictor
        except UnrecoverableGpuError:
            raise
        except Exception:
            logger.exception("Error creating predictor for image ID=%s", image_id)
            return None

    async def upload_image(
        self,
        image_data: bytes,
        width: int,
        height: int,
        executor: Optional[Any] = None,
        shared_tile: bool = False,
        tile_key: Optional[TileCacheKey] = None,
        decoded: Optional[Any] = None,
    ) -> int:
        """Store an image, initialize its predictor, and return a new ID.

        ``decoded`` is the image already decoded to pixels (the upload path decodes to validate
        it). It is handed to the predictor factory so it is not decoded again, and it is not kept.

        Raises:
            CacheFullError: The image does not fit under the byte cap even with every idle
                entry evicted. Pinned and retiring entries cannot be dropped.
            PredictorCreationError: Predictor setup failed, or the entry was evicted
                before setup finished. The image is not left in the cache on failure.
            UnrecoverableGpuError: CUDA context is dead. Caller should exit the process.
        """
        async with self._lock:
            await self._cleanup_expired()
            size_bytes = len(image_data)
            while True:
                reason = self._eviction_reason(size_bytes, shared_tile=shared_tile)
                if reason is None:
                    break
                if not await self._evict_oldest(shared_tile=shared_tile, reason=reason):
                    break
            if self._host_bytes_exceeded(size_bytes):
                # Nothing idle is left to drop. The entry count and the GPU check stay soft (a
                # pinned entry may briefly push past them), but the byte cap is a memory bound.
                raise CacheFullError(
                    f"Cannot cache {size_bytes} bytes: {self._current_memory_bytes} bytes are cached and "
                    f"{self._retiring_bytes} bytes are held by requests still running, "
                    f"against a {self._max_memory_bytes} byte cap."
                )

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
                shared_tile=shared_tile,
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
            # A second pass only happens when the encoder was swapped while the first
            # predictor was being built, so it would otherwise stay on the replaced encoder.
            for attempt in range(2):
                try:
                    predictor = await asyncio.get_running_loop().run_in_executor(
                        executor,
                        self._create_predictor_in_executor,
                        image_data,
                        image_id,
                        tile_key,
                        decoded,
                    )
                except UnrecoverableGpuError:
                    async with self._lock:
                        await self._delete_image_internal(image_id)
                    raise
                async with self._lock:
                    cached = self._cache.get(image_id)
                    if cached is None:
                        # Predictor was never published; no concurrent predict() holders.
                        self._release_predictor(predictor, threading.Lock())
                        raise PredictorCreationError(
                            f"Image ID={image_id} was evicted before predictor could be created"
                        )
                    if predictor is None:
                        await self._delete_image_internal(image_id)
                        raise PredictorCreationError(
                            f"Failed to create predictor for image ID={image_id}"
                        )
                    if attempt == 0 and self._is_stale(predictor):
                        logger.info(
                            "Predictor for image ID=%s was built on a replaced encoder; building again",
                            image_id,
                        )
                        self._release_predictor(predictor, threading.Lock())
                        continue
                    cached.predictor = predictor
                    break

        return image_id

    def _is_stale(self, predictor: Any) -> bool:
        """True when the predictor's encoder generation is known and no longer the current one."""
        if self._current_generation_fn is None:
            return False
        current = self._current_generation_fn()
        generation = self._generation_of(predictor)
        return current is not None and generation is not None and generation != current

    async def upload_tile(
        self,
        tile_key: TileCacheKey,
        image_data: bytes,
        width: int,
        height: int,
        executor: Optional[Any] = None,
        decoded: Optional[Any] = None,
    ) -> Tuple[int, bool]:
        """Store a grid cell under tile_key (the cross-client reusable identity).

        Identical bytes for an existing key refresh TTL and skip set_image().
        Different bytes replace the entry and encode again. The returned image_id is
        an internal predictor handle; callers that speak gRPC use TileCoord, not this id.
        Logs on this call name the cell: volume, section (z), column (x), row (y).

        Returns:
            (image_id, already_cached). already_cached is True only for the identical-bytes hit.
        """
        volume, section, _channel, _transform, _downsample, row, col = tile_key
        with tile_work(volume, section, col, row):
            return await self._upload_tile_body(tile_key, image_data, width, height, executor, decoded)

    async def _upload_tile_body(
        self,
        tile_key: TileCacheKey,
        image_data: bytes,
        width: int,
        height: int,
        executor: Optional[Any],
        decoded: Optional[Any] = None,
    ) -> Tuple[int, bool]:
        async with self._lock:
            await self._cleanup_expired()
            existing_id = self._coord_index.get(tile_key)
            if existing_id is not None:
                cached = self._cache.get(existing_id)
                if cached is not None and cached.image_data == image_data:
                    cached.last_access_time = self._time_fn()
                    logger.info("Tile cache hit (bytes unchanged): key=%s id=%s", tile_key, existing_id)
                    return existing_id, True
                if cached is not None:
                    await self._delete_image_internal(existing_id)

        image_id = await self.upload_image(
            image_data,
            width,
            height,
            executor=executor,
            shared_tile=True,
            tile_key=tile_key,
            decoded=decoded,
        )
        async with self._lock:
            cached = self._cache.get(image_id)
            if cached is None:
                raise PredictorCreationError(
                    f"Tile key={tile_key} was evicted before it could be indexed"
                )
            cached.tile_key = tile_key
            self._coord_index[tile_key] = image_id
        return image_id, False

    async def get_image_by_tile(
        self, tile_key: TileCacheKey
    ) -> Optional[Tuple[int, bytes, int, int, Optional[Any], threading.Lock]]:
        """Pin the cell for tile_key. Returns None when it is not cached.

        Caller must release_image with the returned id.
        """
        async with self._lock:
            image_id = self._coord_index.get(tile_key)
            if image_id is None or image_id not in self._cache:
                return None
        pinned = await self.get_image(image_id)
        if pinned is None:
            return None
        data, width, height, predictor, lock = pinned
        return image_id, data, width, height, predictor, lock

    async def get_image(
        self, image_id: int
    ) -> Optional[Tuple[bytes, int, int, Optional[Any], threading.Lock]]:
        """Return (bytes, width, height, predictor, lock), pin for use, and refresh LRU.

        Caller must invoke :meth:`release_image` when finished so eviction/delete can
        reset the predictor. Returns None if missing.
        """
        async with self._lock:
            await self._cleanup_expired()
            cached_image = self._cache.get(image_id)
            if cached_image is None:
                logger.info("Image not found: ID=%s", image_id)
                return None

            cached_image.last_access_time = self._time_fn()
            cached_image.in_use += 1
            logger.debug(
                "Image retrieved: ID=%s, size=%s bytes, predictor=%s, in_use=%s",
                image_id,
                cached_image.size_bytes,
                "ready" if cached_image.predictor is not None else "pending",
                cached_image.in_use,
            )
            return (
                cached_image.image_data,
                cached_image.width,
                cached_image.height,
                cached_image.predictor,
                cached_image.predictor_lock,
            )

    async def release_image(self, image_id: int) -> None:
        """Drop one pin from :meth:`get_image`. Resets predictor when a deferred delete finishes."""
        async with self._lock:
            cached_image = self._cache.get(image_id)
            if cached_image is None:
                cached_image = self._retiring.get(image_id)
            if cached_image is None:
                return
            if cached_image.in_use > 0:
                cached_image.in_use -= 1
            if cached_image.in_use == 0 and image_id in self._retiring:
                retiring = self._retiring.pop(image_id)
                self._retiring_bytes -= retiring.size_bytes
                self._release_predictor(retiring.predictor, retiring.predictor_lock)
                logger.info("Image retired after last user: ID=%s", image_id)

    def _arbitrary_count(self) -> int:
        return sum(1 for cached in self._cache.values() if not cached.shared_tile)

    def _held_bytes(self) -> int:
        """Bytes actually in memory: live entries plus entries retiring while a request holds them."""
        return self._current_memory_bytes + self._retiring_bytes

    def _host_bytes_exceeded(self, incoming_size_bytes: int = 0) -> bool:
        return self._held_bytes() + incoming_size_bytes > self._max_memory_bytes

    def _gpu_under_pressure(self) -> bool:
        """True only for a real boolean. A missing or failed check does not evict."""
        if self._gpu_under_pressure_fn is None:
            return False
        try:
            return self._gpu_under_pressure_fn() is True
        except Exception:
            logger.exception("GPU memory check failed")
            return False

    def _eviction_reason(self, incoming_size_bytes: int, *, shared_tile: bool) -> Optional[str]:
        """Why the next insert must drop something, or None when it fits.

        Arbitrary uploads still stop at max_entries. Shared tiles ignore that
        count and the TTL; they yield only for the byte cap or GPU pressure.
        """
        if self._host_bytes_exceeded(incoming_size_bytes):
            return "memory cap"
        if shared_tile:
            if self._gpu_under_pressure():
                return "GPU memory"
            return None
        if self._arbitrary_count() >= self._max_entries:
            return "entry cap"
        return None

    def _release_predictor(self, predictor: Any, predictor_lock: threading.Lock) -> None:
        if predictor is None or self._release_predictor_func is None:
            return
        try:
            # Serialize with predict() so reset_predictor cannot clear mid-inference.
            with predictor_lock:
                self._release_predictor_func(predictor)
        except Exception:
            logger.exception("Error releasing predictor")

    async def _retire_or_delete(self, image_id: int) -> bool:
        """Remove from the live cache. Caller must hold `_lock`.

        If the entry is pinned, move it to `_retiring` and delay predictor reset until
        the last :meth:`release_image`. Returns False if the ID was not live.
        """
        cached_image = self._cache.pop(image_id, None)
        if cached_image is None:
            return False
        if cached_image.tile_key is not None and self._coord_index.get(cached_image.tile_key) == image_id:
            del self._coord_index[cached_image.tile_key]
        self._current_memory_bytes -= cached_image.size_bytes
        if cached_image.in_use > 0:
            self._retiring[image_id] = cached_image
            self._retiring_bytes += cached_image.size_bytes
            logger.info(
                "Image remove deferred (in use): ID=%s, in_use=%s",
                image_id,
                cached_image.in_use,
            )
            return True
        self._release_predictor(cached_image.predictor, cached_image.predictor_lock)
        return True

    async def _delete_image_internal(self, image_id: int) -> bool:
        """Remove a live entry (or finish a retirement). Caller must hold `_lock`."""
        return await self._retire_or_delete(image_id)

    async def delete_image(self, image_id: int) -> bool:
        """Delete by ID. Returns False if the ID was not present.

        A shared tile is kept when the client asks to drop it and the cache is
        under both the byte cap and the GPU check. The cell is still in use by
        other clients. The delete runs when one of those caps says memory is needed.
        """
        async with self._lock:
            cached = self._cache.get(image_id)
            if (
                cached is not None
                and cached.shared_tile
                and not self._host_bytes_exceeded()
                and not self._gpu_under_pressure()
            ):
                logger.info(
                    "Shared tile retained (client delete ignored): ID=%s key=%s",
                    image_id,
                    cached.tile_key,
                )
                return True
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

    async def clear(self) -> None:
        """Drop every live entry. Pinned images defer predictor reset until release."""
        async with self._lock:
            for image_id in list(self._cache.keys()):
                await self._delete_image_internal(image_id)
            logger.info(
                "Image cache cleared, total_cache=%s bytes, count=%s, retiring=%s",
                self._current_memory_bytes,
                len(self._cache),
                len(self._retiring),
            )

    async def flush_stale(self) -> int:
        """Drop live entries whose embedding was made with a replaced encoder generation.

        Entries made with the current generation, and entries whose predictor is not built
        yet or reports no generation, are kept: a client may already hold their id, and an
        entry that is consistent with its own model is correct to serve. A pinned stale entry
        retires when its last user releases it, as with any delete. Returns how many entries
        were dropped.
        """
        if self._predictor_generation_fn is None or self._current_generation_fn is None:
            return 0
        async with self._lock:
            current = self._current_generation_fn()
            if current is None:
                return 0
            stale = [
                image_id
                for image_id, cached in self._cache.items()
                if cached.predictor is not None
                and self._generation_of(cached.predictor) not in (None, current)
            ]
            for image_id in stale:
                await self._delete_image_internal(image_id)
            logger.info(
                "Image cache flushed %s stale entries (generation now %s), kept %s, retiring=%s",
                len(stale),
                current,
                len(self._cache),
                len(self._retiring),
            )
            return len(stale)

    def _generation_of(self, predictor: Any) -> Optional[str]:
        if self._predictor_generation_fn is None:
            return None
        try:
            return self._predictor_generation_fn(predictor)
        except Exception:
            logger.exception("Predictor generation check failed")
            return None

    async def _evict_oldest(self, *, shared_tile: bool, reason: str) -> bool:
        """Drop the LRU idle entry. Caller must hold `_lock`. False if none idle.

        A shared-tile insert drops another shared tile first. An arbitrary-upload
        entry-cap eviction does not take a shared tile.
        """
        idle = [
            (image_id, cached)
            for image_id, cached in self._cache.items()
            if cached.in_use == 0
        ]
        if shared_tile:
            tiles = [(image_id, cached) for image_id, cached in idle if cached.shared_tile]
            pool = tiles or idle
        elif reason == "entry cap":
            pool = [(image_id, cached) for image_id, cached in idle if not cached.shared_tile]
        else:
            pool = idle
        if not pool:
            return False
        oldest_id = min(pool, key=lambda item: item[1].last_access_time)[0]
        await self._delete_image_internal(oldest_id)
        logger.info(
            "Image evicted (%s): ID=%s, total_cache=%s bytes (%.2f MB), count=%s",
            reason,
            oldest_id,
            self._current_memory_bytes,
            self._current_memory_bytes / (1024**2),
            len(self._cache),
        )
        return True

    async def _cleanup_expired(self) -> None:
        """Drop idle entries unused longer than TTL. Caller must hold `_lock`."""
        current_time = self._time_fn()
        expired_ids = [
            image_id
            for image_id, cached_image in self._cache.items()
            if not cached_image.shared_tile
            and cached_image.in_use == 0
            and current_time - cached_image.last_access_time > self._ttl_seconds
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
            held = self._held_bytes()
            return {
                "total_images": len(self._cache),
                "retiring_images": len(self._retiring),
                "retiring_bytes": self._retiring_bytes,
                "total_memory_bytes": held,
                "total_memory_mb": held / (1024**2),
                "max_memory_bytes": self._max_memory_bytes,
                "max_memory_mb": self._max_memory_bytes / (1024**2),
                "memory_usage_percent": (
                    held / self._max_memory_bytes * 100
                    if self._max_memory_bytes > 0
                    else 0
                ),
                "ttl_seconds": self._ttl_seconds,
                "max_entries": self._max_entries,
            }
