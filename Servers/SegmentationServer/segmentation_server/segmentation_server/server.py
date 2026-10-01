"""gRPC server that exposes SAM2 segmentation over SegmentationService."""

from __future__ import annotations

import asyncio
import hashlib
import importlib.metadata
import logging
import os
import signal
import threading
import time
from concurrent import futures
from typing import TYPE_CHECKING, Any, Callable, List, Optional, Tuple

import grpc
import numpy as np
from grpc.aio import ServicerContext
from numpy.typing import NDArray

from segmentation_grpc import (
    DeleteImageRequest,
    DeleteImageResponse,
    MultiSegmentationRequest,
    Point,
    Polygon,
    SegmentationRequest,
    SegmentationResponse,
    SegmentationServiceServicer,
    SegmentImageSetRequest,
    SegmentResult,
    SegmentTilesRequest,
    ServerStatusRequest,
    ServerStatusResponse,
    TileCoord,
    UploadImageRequest,
    UploadImageResponse,
    UploadTileRequest,
    UploadTileResponse,
    add_SegmentationServiceServicer_to_server,
)
from segmentation_server.cuda_errors import UnrecoverableGpuError
from segmentation_server.growth_memory import GrowthMemory, grow_remembering, prompt_key
from segmentation_server.image_cache import (
    DEFAULT_MAX_ENTRIES,
    DEFAULT_MAX_MEMORY_BYTES,
    DEFAULT_TTL_SECONDS,
    ImageCache,
    TileCacheKey,
)
from segmentation_server.mask_utils import (
    SegmentInfo,
    combined_mask_to_segments,
    encode_png,
    prepare_image_for_sam2,
)
from segmentation_server.tile_work import (
    install_sam2_log_filter,
    install_tile_work_logging,
    tile_work,
)
from segmentation_server.tile_growth import (
    TILE_SIZE,
    GrowthCancelled,
    PredictUnavailable,
    TileIndex,
    max_requested_tiles_from_env,
    stitch_half_tile,
    tile_of_point,
)

if TYPE_CHECKING:
    from segmentation_server.segmentation_service import SegmentationModel

logger = logging.getLogger(__name__)

_EMPTY_COORDINATES_MESSAGE = "No coordinates provided. At least one coordinate point is required."
_TLS_PORT = 443
_MISSING_IMAGE_ID_MESSAGE = "image_id is required on the first SegmentImageSets message."
_MIXED_IMAGE_ID_MESSAGE = "SegmentImageSets messages must use one image_id for the whole stream."
_EMPTY_FOREGROUND_POINTS_MESSAGE = "No foreground_points provided. At least one point is required."


def _remember_tile_image(
    images: dict,
    row: int,
    col: int,
    image_bytes: bytes,
) -> None:
    """Decode a pinned tile so a seam can be stitched without another upload.

    A decode failure leaves the cell out of the map. Growth then asks for the
    cell again instead of prompting on the tile cut.
    """
    if not image_bytes or (row, col) in images:
        return
    try:
        images[(row, col)] = prepare_image_for_sam2(image_bytes)
    except (OSError, ValueError):
        logger.exception("Could not decode tile row=%s col=%s for a half-tile", row, col)


class RequestLoadTracker:
    """In-flight count and EWMA latency for GetServerStatus / picker ranking."""

    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._in_flight = 0
        self._recent_latency_ms = 0.0
        self._samples = 0

    def begin(self) -> None:
        with self._lock:
            self._in_flight += 1

    def end(self, elapsed_seconds: float) -> None:
        elapsed_ms = max(0.0, elapsed_seconds) * 1000.0
        with self._lock:
            self._in_flight = max(0, self._in_flight - 1)
            if self._samples == 0:
                self._recent_latency_ms = elapsed_ms
            else:
                self._recent_latency_ms = 0.3 * elapsed_ms + 0.7 * self._recent_latency_ms
            self._samples += 1

    def snapshot(self) -> Tuple[int, float]:
        with self._lock:
            return self._in_flight, self._recent_latency_ms


class SegmentationServicer(SegmentationServiceServicer):
    """gRPC handlers for upload, segment, delete, and status.

    Cached images each own a predictor created at upload. Inline images use the
    shared predictor and are slower because set_image() runs per request.
    """

    def __init__(
        self,
        cache_max_memory_bytes: int = DEFAULT_MAX_MEMORY_BYTES,
        cache_ttl_seconds: int = DEFAULT_TTL_SECONDS,
        cache_max_images: int = DEFAULT_MAX_ENTRIES,
        inference_executor: Optional[Any] = None,
        server_start_time: Optional[float] = None,
        model: Optional[SegmentationModel] = None,
        image_cache: Optional[ImageCache] = None,
        inference_workers: int = 1,
        exit_process: Callable[[int], None] = os._exit,
        compile_image_encoder: bool = True,
    ) -> None:
        """
        Args:
            cache_max_memory_bytes: Image-byte cap for the cache (default 1 GiB).
                Shared tiles are kept until this cap, or until the GPU needs room.
            cache_ttl_seconds: Unused-entry lifetime for arbitrary uploads (default 5 minutes).
                Shared tiles are not expired by the TTL.
            cache_max_images: Max arbitrary uploads (default 32). Shared tiles do not count.
            inference_executor: Thread pool for SAM2 work; None uses the default executor.
            server_start_time: time.monotonic() at process start, for uptime.
            model: Injected SAM2 wrapper; constructed here if omitted.
            image_cache: Injected cache; constructed here if omitted.
            inference_workers: Size of the inference thread pool, reported in GetServerStatus.
            exit_process: Called after an unrecoverable GPU error (inject in tests).
            compile_image_encoder: torch.compile the SAM2 Hiera encoder (CUDA only).
        """
        if model is None:
            from segmentation_server.segmentation_service import SegmentationModel as _SegmentationModel
            model = _SegmentationModel(compile_image_encoder=compile_image_encoder)
        self.model = model
        self.inference_executor = inference_executor
        self._inference_workers = max(1, inference_workers)
        self._exit_process = exit_process
        self._server_start_time = server_start_time if server_start_time is not None else 0.0
        self._load = RequestLoadTracker()
        self.image_cache = image_cache if image_cache is not None else ImageCache(
            max_memory_bytes=cache_max_memory_bytes,
            ttl_seconds=cache_ttl_seconds,
            max_entries=cache_max_images,
            create_predictor_func=self.model.create_initialized_predictor,
            release_predictor_func=self.model.release_predictor,
            gpu_under_pressure=self._gpu_under_pressure,
        )
        self._max_requested_tiles = max_requested_tiles_from_env()
        self._growth_memory = GrowthMemory()
        try:
            self._loop: Optional[asyncio.AbstractEventLoop] = asyncio.get_running_loop()
        except RuntimeError:
            self._loop = None
        if hasattr(self.model, "set_on_compiled_ready"):
            self.model.set_on_compiled_ready(self._flush_cache_after_compile)

    def _gpu_under_pressure(self) -> bool:
        """True when the model says the next shared-tile embedding needs GPU memory."""
        check = getattr(self.model, "gpu_memory_under_pressure", None)
        if not callable(check):
            return False
        try:
            return check() is True
        except Exception:
            logger.exception("GPU memory check failed")
            return False

    def _flush_cache_after_compile(self) -> None:
        """Drop eager embeddings once the compiled encoder is serving."""
        if self._loop is None or self._loop.is_closed():
            logger.warning("Compiled encoder ready but no event loop; image cache not flushed")
            return
        asyncio.run_coroutine_threadsafe(self.image_cache.clear(), self._loop)

    def _schedule_process_exit(self, delay_seconds: float = 0.25) -> None:
        """Exit after the current RPC can send UNAVAILABLE. Docker then restarts us."""
        def _exit() -> None:
            self._exit_process(1)

        try:
            asyncio.get_running_loop().call_later(delay_seconds, _exit)
        except RuntimeError:
            self._exit_process(1)

    async def _abort_unrecoverable_gpu(
        self,
        exc: BaseException,
        context: ServicerContext,
        delay_seconds: float = 0.25,
    ) -> None:
        logger.critical(
            "CUDA context lost (%s); exiting so Docker can restart the process",
            exc,
        )
        self._schedule_process_exit(delay_seconds=delay_seconds)
        await context.abort(
            grpc.StatusCode.UNAVAILABLE,
            "CUDA context lost after a GPU reset or host sleep; segmentation server is restarting.",
        )

    def _build_segmentation_response(
        self,
        labeled_image: NDArray[np.uint16],
        segments: List[SegmentInfo],
        width: int,
        height: int,
        omit_labeled_image: bool = False,
    ) -> SegmentationResponse:
        """Encode labeled PNG plus per-segment cropped masks and polygons."""
        if omit_labeled_image or labeled_image.size == 0:
            response = SegmentationResponse(width=width, height=height)
        else:
            response = SegmentationResponse(
                labeled_image=encode_png(labeled_image),
                width=width,
                height=height,
            )

        for segment in segments:
            if 'mask' in segment:
                mask_bool: NDArray[np.bool_] = segment['mask']
                mask_bool = self.model.fill_small_holes(mask_bool)
                x, y, mask_width, mask_height = self.model.get_mask_bounds(mask_bool)
                if mask_width > 0 and mask_height > 0:
                    cropped_mask = mask_bool[y:y + mask_height, x:x + mask_width]
                else:
                    cropped_mask = np.zeros((0, 0), dtype=np.bool_)
                mask_bytes = encode_png(cropped_mask.astype(np.uint8) * 255)
                polygons = self.model.mask_to_polygons(mask_bool)
            else:
                mask_bytes = b''
                x, y, mask_width, mask_height = (
                    segment.get('x', 0),
                    segment.get('y', 0),
                    segment.get('width', 0),
                    segment.get('height', 0),
                )
                polygons = []

            segment_result = SegmentResult(
                index=segment['index'],
                score=segment['score'],
                mask=mask_bytes,
                x=x,
                y=y,
            )
            for polygon in polygons:
                poly = Polygon()
                for point in polygon:
                    poly.points.append(Point(x=int(point[0]), y=int(point[1])))
                segment_result.polygons.append(poly)
            response.segments.append(segment_result)

        return response

    async def _abort_if_image_not_found(
        self,
        cached_result: Optional[Tuple],
        image_id: int,
        context: ServicerContext,
    ) -> bool:
        """Abort with NOT_FOUND when the cache has no entry. Returns True if aborted."""
        if cached_result is None:
            await context.abort(
                grpc.StatusCode.NOT_FOUND,
                f"Image ID {image_id} not found in cache. "
                "It may have been evicted or expired. Please re-upload the image.",
            )
            return True
        return False

    async def _abort_if_predictor_unavailable(
        self,
        predictor: Optional[Any],
        image_id: int,
        context: ServicerContext,
    ) -> bool:
        """Abort with UNAVAILABLE when the predictor is still None. Returns True if aborted."""
        if predictor is None:
            await context.abort(
                grpc.StatusCode.UNAVAILABLE,
                f"Predictor for image ID {image_id} is not yet ready or failed to create. "
                "Please wait a moment and try again, or re-upload the image.",
            )
            return True
        return False

    async def _validate_coordinates(
        self,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        context: ServicerContext,
        empty_message: str = _EMPTY_COORDINATES_MESSAGE,
    ) -> bool:
        """Abort with INVALID_ARGUMENT on empty points or label-length mismatch."""
        if len(coordinates) == 0:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, empty_message)
            return True

        if len(coordinates) != len(labels):
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                f"Coordinates and labels length mismatch: {len(coordinates)} coordinates but {len(labels)} labels. "
                "Each coordinate must have a corresponding label.",
            )
            return True

        return False

    def _extract_coordinates_and_labels_from_segment_request(
        self,
        request: SegmentationRequest,
    ) -> Tuple[List[Tuple[int, int]], List[int]]:
        """Map SegmentationRequest.coordinates/labels into parallel lists."""
        coordinates: List[Tuple[int, int]] = [(point.x, point.y) for point in request.coordinates]
        labels: List[int] = list(request.labels)
        return coordinates, labels

    def _extract_coordinates_and_labels_from_multi_request(
        self,
        request: MultiSegmentationRequest,
    ) -> Tuple[List[Tuple[int, int]], List[int]]:
        """Map MultiSegmentationRequest.foreground_points; id 0 is background."""
        coordinates: List[Tuple[int, int]] = []
        labels: List[int] = []
        for point_id, point in request.foreground_points.items():
            coordinates.append((point.x, point.y))
            labels.append(0 if point_id == 0 else 1)
        return coordinates, labels

    def _segment_with_locked_predictor(
        self,
        predictor: Any,
        predictor_lock: threading.Lock,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        multimask_output: bool,
        empty_shape: Tuple[int, int],
    ) -> Tuple[NDArray[np.uint16], List[SegmentInfo]]:
        """Executor entry: serialize predict() on one cached predictor."""
        with predictor_lock:
            return self.model.segment_image_with_predictor(
                predictor=predictor,
                coordinates=coordinates,
                labels=labels,
                multimask_output=multimask_output,
                empty_shape=empty_shape,
            )

    def _segment_inline_image(
        self,
        image_data: bytes,
        width: int,
        height: int,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        multimask_output: bool,
    ) -> Tuple[NDArray[np.uint16], List[SegmentInfo]]:
        """Executor entry for the uncached set_image() + predict() path."""
        return self.model.segment_image(
            image_data=image_data,
            width=width,
            height=height,
            coordinates=coordinates,
            labels=labels,
            multimask_output=multimask_output,
        )

    async def _handle_cached_image_segmentation(
        self,
        image_id: int,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        multimask_output: bool,
        context: ServicerContext,
        empty_message: str,
    ) -> SegmentationResponse:
        """Segment using a cache hit. Aborts the RPC on cache/predictor/input errors."""
        cached_result = await self.image_cache.get_image(image_id)
        if await self._abort_if_image_not_found(cached_result, image_id, context):
            return SegmentationResponse()

        try:
            _image_data, width, height, predictor, predictor_lock = cached_result

            if await self._abort_if_predictor_unavailable(predictor, image_id, context):
                return SegmentationResponse()

            start_time = time.perf_counter()
            logger.debug("Using cached image with predictor: ID=%s, %sx%s", image_id, width, height)

            if await self._validate_coordinates(coordinates, labels, context, empty_message=empty_message):
                logger.info("Cached segmentation validation failed in %.3fs", time.perf_counter() - start_time)
                return SegmentationResponse()

            try:
                labeled_image, segments = await asyncio.get_running_loop().run_in_executor(
                    self.inference_executor,
                    self._segment_with_locked_predictor,
                    predictor,
                    predictor_lock,
                    coordinates,
                    labels,
                    multimask_output,
                    (height, width),
                )
            except UnrecoverableGpuError as e:
                await self._abort_unrecoverable_gpu(e, context)
                return SegmentationResponse()
            except Exception as e:
                logger.exception("Predictor error for image ID %s", image_id)
                await self.image_cache.delete_image(image_id)
                await context.abort(
                    grpc.StatusCode.INTERNAL,
                    f"Error processing segmentation request: {e}. Image has been removed from cache. Please re-upload the image.",
                )
                return SegmentationResponse()

            n_fg = sum(1 for label in labels if int(label) == 1)
            n_bg = len(labels) - n_fg
            elapsed = time.perf_counter() - start_time
            log = logger.warning if not segments else logger.info
            log(
                "Cached segmentation id=%s %sx%s fg=%s bg=%s segments=%s in %.3fs",
                image_id,
                width,
                height,
                n_fg,
                n_bg,
                len(segments),
                elapsed,
            )
            return self._build_segmentation_response(labeled_image, segments, width, height)
        finally:
            await self.image_cache.release_image(image_id)

    async def _handle_inline_image_segmentation(
        self,
        image_data: bytes,
        width: int,
        height: int,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        multimask_output: bool,
        context: ServicerContext,
        empty_message: str,
    ) -> SegmentationResponse:
        """Segment an image sent on the request. Aborts the RPC on input or model errors."""
        start_time = time.perf_counter()
        logger.debug("Using inline image data: %sx%s", width, height)

        if await self._validate_coordinates(coordinates, labels, context, empty_message=empty_message):
            return SegmentationResponse()

        try:
            labeled_image, segments = await asyncio.get_running_loop().run_in_executor(
                self.inference_executor,
                self._segment_inline_image,
                image_data,
                width,
                height,
                coordinates,
                labels,
                multimask_output,
            )
        except UnrecoverableGpuError as e:
            await self._abort_unrecoverable_gpu(e, context)
            return SegmentationResponse()
        except Exception as e:
            logger.exception("Error during inline segmentation")
            await context.abort(grpc.StatusCode.INTERNAL, f"Error processing request: {e}")
            return SegmentationResponse()

        n_fg = sum(1 for label in labels if int(label) == 1)
        n_bg = len(labels) - n_fg
        elapsed = time.perf_counter() - start_time
        log = logger.warning if not segments else logger.info
        log(
            "Inline segmentation %sx%s fg=%s bg=%s segments=%s in %.3fs",
            width,
            height,
            n_fg,
            n_bg,
            len(segments),
            elapsed,
        )
        return self._build_segmentation_response(labeled_image, segments, width, height)

    async def _dispatch_segmentation(
        self,
        image_id: int,
        image_data: bytes,
        width: int,
        height: int,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        multimask_output: bool,
        context: ServicerContext,
        empty_message: str,
    ) -> SegmentationResponse:
        """Choose cached vs inline path from image_id."""
        if image_id != 0:
            if image_data:
                logger.debug(
                    "Ignoring inline image_data for cached image_id=%s (%s bytes)",
                    image_id,
                    len(image_data),
                )
            return await self._handle_cached_image_segmentation(
                image_id, coordinates, labels, multimask_output, context, empty_message
            )
        return await self._handle_inline_image_segmentation(
            image_data, width, height, coordinates, labels, multimask_output, context, empty_message
        )

    async def UploadImage(
        self,
        request: UploadImageRequest,
        context: ServicerContext,
    ) -> UploadImageResponse:
        """Cache image bytes and return an ID after the predictor is ready."""
        image_data: bytes = request.image_data
        width: int = request.width
        height: int = request.height

        if not image_data or width <= 0 or height <= 0:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                "Upload requires non-empty image_data and positive width and height.",
            )

        try:
            decoded = prepare_image_for_sam2(image_data)
        except (OSError, ValueError) as e:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                f"Could not decode image_data: {e}",
            )
            raise

        actual_height, actual_width = decoded.shape[:2]
        if actual_width != width or actual_height != height:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                f"width/height {width}x{height} do not match decoded image {actual_width}x{actual_height}.",
            )

        logger.info("UploadImage RPC: %sx%s, %s bytes", width, height, len(image_data))
        self._load.begin()
        start_time = time.perf_counter()
        try:
            image_id: int = await self.image_cache.upload_image(
                image_data, width, height, executor=self.inference_executor
            )
        except UnrecoverableGpuError as e:
            logger.exception("Error uploading image")
            await self._abort_unrecoverable_gpu(e, context)
            raise
        except Exception as e:
            logger.exception("Error uploading image")
            await context.abort(grpc.StatusCode.INTERNAL, f"Error uploading image: {e}")
            raise
        finally:
            self._load.end(time.perf_counter() - start_time)

        logger.info("UploadImage assigned ID=%s in %.3fs", image_id, time.perf_counter() - start_time)
        return UploadImageResponse(image_id=image_id)

    async def DeleteImage(
        self,
        request: DeleteImageRequest,
        context: ServicerContext,
    ) -> DeleteImageResponse:
        """Remove a cached image. success is False when the ID was already gone."""
        start_time = time.perf_counter()
        image_id: int = request.image_id
        logger.info("DeleteImage RPC: ID=%s", image_id)
        try:
            success: bool = await self.image_cache.delete_image(image_id)
        except Exception as e:
            logger.exception("Error deleting image")
            await context.abort(grpc.StatusCode.INTERNAL, f"Error deleting image: {e}")
            return DeleteImageResponse(success=False)

        logger.info("DeleteImage ID=%s success=%s in %.3fs", image_id, success, time.perf_counter() - start_time)
        return DeleteImageResponse(success=success)

    async def GetServerStatus(
        self,
        request: ServerStatusRequest,
        context: ServicerContext,
    ) -> ServerStatusResponse:
        """Version, uptime, device, and cache occupancy for health/selection UI."""
        try:
            version = importlib.metadata.version("segmentation_server")
        except importlib.metadata.PackageNotFoundError:
            version = "0.1.0"
        uptime_seconds = time.monotonic() - self._server_start_time if self._server_start_time else 0.0
        cache_stats = await self.image_cache.get_stats()
        device = getattr(self.model, "device", "unknown")
        compile_status = getattr(self.model, "compile_status", "off")
        in_flight, recent_latency_ms = self._load.snapshot()
        return ServerStatusResponse(
            version=version,
            uptime_seconds=uptime_seconds,
            message=(
                f"device={device}; "
                f"compile={compile_status}; "
                f"cache={cache_stats['total_images']} images, "
                f"{cache_stats['total_memory_mb']:.1f} MiB"
            ),
            in_flight_requests=in_flight,
            cached_images=int(cache_stats["total_images"]),
            cache_memory_bytes=int(cache_stats["total_memory_bytes"]),
            recent_latency_ms=recent_latency_ms,
            inference_workers=self._inference_workers,
            capabilities=self.model.capabilities,
        )

    async def UploadTile(
        self,
        request: UploadTileRequest,
        context: ServicerContext,
    ) -> UploadTileResponse:
        """Cache one grid cell. Identical bytes for the same coord skip set_image()."""
        coord = request.coord
        if coord is None or coord.downsample <= 0:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                "UploadTile requires a coord with a positive downsample.",
            )
            return UploadTileResponse()
        if request.width != TILE_SIZE or request.height != TILE_SIZE:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                f"UploadTile requires a {TILE_SIZE}x{TILE_SIZE} image, got {request.width}x{request.height}.",
            )
            return UploadTileResponse()
        image_data: bytes = request.image_data
        if not image_data:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, "UploadTile requires non-empty image_data.")
            return UploadTileResponse()
        try:
            decoded = prepare_image_for_sam2(image_data)
        except (OSError, ValueError) as e:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, f"Could not decode image_data: {e}")
            return UploadTileResponse()
        actual_height, actual_width = decoded.shape[:2]
        if actual_width != request.width or actual_height != request.height:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                f"width/height {request.width}x{request.height} do not match decoded image {actual_width}x{actual_height}.",
            )
            return UploadTileResponse()

        # Public reusable identity is TileCoord (volume/section/channel/transform/ds/row/col).
        # cache_id is an internal sequential handle for predictors; clients must key on Coord.
        tile_key = _tile_cache_key(coord)
        self._load.begin()
        start_time = time.perf_counter()
        try:
            cache_id, already_cached = await self.image_cache.upload_tile(
                tile_key,
                image_data,
                request.width,
                request.height,
                executor=self.inference_executor,
            )
            if already_cached and hasattr(self.model, "note_disk_embedding_used"):
                try:
                    self.model.note_disk_embedding_used(image_data)
                except Exception:
                    logger.exception("Embedding disk touch failed")
        except UnrecoverableGpuError as e:
            await self._abort_unrecoverable_gpu(e, context)
            return UploadTileResponse()
        except Exception as e:
            logger.exception("Error uploading tile")
            await context.abort(grpc.StatusCode.INTERNAL, f"Error uploading tile: {e}")
            return UploadTileResponse()
        finally:
            self._load.end(time.perf_counter() - start_time)
        logger.info(
            "UploadTile ok key=vol=%s|sec=%s|ch=%s|xf=%s|ds=%s|row=%s|col=%s "
            "cache_id=%s %sx%s %s bytes already_cached=%s in %.3fs",
            coord.volume,
            coord.section,
            coord.channel,
            coord.transform,
            coord.downsample,
            coord.row,
            coord.col,
            cache_id,
            request.width,
            request.height,
            len(image_data),
            already_cached,
            time.perf_counter() - start_time,
        )
        if not already_cached:
            self._growth_memory.invalidate_tile(
                coord.volume,
                int(coord.section),
                coord.channel,
                coord.transform,
                int(coord.downsample),
                int(coord.row),
                int(coord.col),
            )
        return UploadTileResponse(already_cached=already_cached)

    async def SegmentTiles(
        self,
        request: SegmentTilesRequest,
        context: ServicerContext,
    ) -> SegmentationResponse:
        """Segment uploaded cells that contain foreground points and grow across borders."""
        if len(request.tiles) == 0:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, "SegmentTiles requires at least one tile.")
            return SegmentationResponse()
        if len(request.foreground) == 0:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                "SegmentTiles requires at least one foreground point.",
            )
            return SegmentationResponse()

        identity = request.tiles[0]
        for tile in request.tiles:
            if not _same_tile_identity(tile, identity):
                await context.abort(
                    grpc.StatusCode.INVALID_ARGUMENT,
                    "SegmentTiles tiles must share volume, section, channel, transform, and downsample.",
                )
                return SegmentationResponse()

        self._load.begin()
        start_time = time.perf_counter()
        pinned_ids: List[int] = []
        predictors: dict[Tuple[int, int], Tuple[Any, threading.Lock, int, int]] = {}
        images: dict[Tuple[int, int], NDArray[np.uint8]] = {}
        try:
            foreground = [(point.x, point.y) for point in request.foreground]
            background = [(point.x, point.y) for point in request.background]
            seen: set[TileCacheKey] = set()
            uploaded: List[TileIndex] = []
            for tile in request.tiles:
                key = _tile_cache_key(tile)
                if key in seen:
                    continue
                seen.add(key)
                holds_foreground = _tile_contains_foreground(tile, foreground)
                pinned = await self.image_cache.get_image_by_tile(key)
                if pinned is None:
                    if holds_foreground:
                        await context.abort(
                            grpc.StatusCode.NOT_FOUND,
                            f"TILE_NOT_FOUND row={tile.row} col={tile.col} downsample={tile.downsample}",
                        )
                        return SegmentationResponse()
                    logger.info(
                        "SegmentTiles skip uncached tile row=%s col=%s ds=%s (no foreground)",
                        tile.row,
                        tile.col,
                        tile.downsample,
                    )
                    continue
                image_id, image_bytes, width, height, predictor, predictor_lock = pinned
                pinned_ids.append(image_id)
                _remember_tile_image(images, int(tile.row), int(tile.col), image_bytes)
                if predictor is None:
                    if holds_foreground:
                        await context.abort(
                            grpc.StatusCode.UNAVAILABLE,
                            f"Predictor for tile row={tile.row} col={tile.col} is not ready.",
                        )
                        return SegmentationResponse()
                    continue
                predictors[(tile.row, tile.col)] = (predictor, predictor_lock, height, width)
                uploaded.append(TileIndex(row=int(tile.row), col=int(tile.col)))
            multimask_output = request.multimask_output
            session = prompt_key(
                identity.volume,
                int(identity.section),
                identity.channel,
                identity.transform,
                int(identity.downsample),
                multimask_output,
                foreground,
                background,
            )
            await self._pin_remembered_tiles(identity, session, predictors, pinned_ids, images)

            def predict(
                row: int,
                col: int,
                points: List[Tuple[int, int]],
                labels: List[int],
            ):
                found = predictors.get((row, col))
                if found is None:
                    raise PredictUnavailable(f"row={row} col={col}")
                predictor, predictor_lock, height, width = found
                with predictor_lock:
                    with tile_work(identity.volume, int(identity.section), col, row):
                        return self.model.predict_tile_union(
                            predictor,
                            points,
                            labels,
                            multimask_output,
                            (height, width),
                        )

            def seam_predict(
                src_row: int,
                src_col: int,
                dst_row: int,
                dst_col: int,
                side: str,
                points: List[Tuple[int, int]],
                labels: List[int],
            ):
                source = images.get((src_row, src_col))
                neighbor = images.get((dst_row, dst_col))
                if source is None or neighbor is None or source.shape[:2] != neighbor.shape[:2]:
                    raise PredictUnavailable(
                        f"seam row={src_row} col={src_col} side={side}"
                    )
                stitched = stitch_half_tile(source, neighbor, side)
                height, width = int(stitched.shape[0]), int(stitched.shape[1])
                logger.info(
                    "seam half-tile side=%s from row=%s col=%s to row=%s col=%s",
                    side,
                    src_row,
                    src_col,
                    dst_row,
                    dst_col,
                )
                with tile_work(identity.volume, int(identity.section), src_col, src_row):
                    return self.model.predict_ephemeral(
                        stitched,
                        points,
                        labels,
                        multimask_output,
                        (height, width),
                    )

            stop_growth = threading.Event()
            cancel_watch = asyncio.create_task(self._watch_rpc_cancel(context, stop_growth))
            try:
                outcome = await asyncio.get_running_loop().run_in_executor(
                    self.inference_executor,
                    lambda: grow_remembering(
                        self._growth_memory,
                        session,
                        uploaded,
                        foreground,
                        background,
                        predict,
                        should_stop=stop_growth.is_set,
                        max_requested=self._max_requested_tiles,
                        seam_predict=seam_predict,
                    ),
                )
                result = outcome.result
            except GrowthCancelled:
                logger.info("SegmentTiles stopped because the client cancelled the call")
                return SegmentationResponse()
            except UnrecoverableGpuError as e:
                await self._abort_unrecoverable_gpu(e, context)
                return SegmentationResponse()
            except Exception as e:
                logger.exception("Tile segmentation failed")
                await context.abort(grpc.StatusCode.INTERNAL, f"Error processing segmentation request: {e}")
                return SegmentationResponse()
            finally:
                stop_growth.set()
                cancel_watch.cancel()
                try:
                    await cancel_watch
                except asyncio.CancelledError:
                    pass

            height, width = (result.mask.shape[0], result.mask.shape[1]) if result.mask.ndim == 2 else (0, 0)
            labeled_image, segments = combined_mask_to_segments(
                result.mask,
                result.score,
                empty_shape=(height, width),
            )
            response = self._build_segmentation_response(
                labeled_image,
                segments,
                width,
                height,
                omit_labeled_image=request.omit_labeled_image,
            )
            response.origin_x = result.origin_x
            response.origin_y = result.origin_y
            for tile in result.requested:
                response.requested_tiles.append(
                    TileCoord(
                        volume=identity.volume,
                        section=identity.section,
                        channel=identity.channel,
                        transform=identity.transform,
                        downsample=identity.downsample,
                        row=tile.row,
                        col=tile.col,
                    )
                )
            logger.info(
                "SegmentTiles ok key=vol=%s|sec=%s|ch=%s|xf=%s|ds=%s "
                "tiles=%s fg=%s segments=%s requested=%s reused=%s predicted=%s in %.3fs session=%s",
                identity.volume,
                identity.section,
                identity.channel,
                identity.transform,
                identity.downsample,
                len(uploaded),
                len(foreground),
                len(segments),
                len(result.requested),
                outcome.reused,
                outcome.predicted,
                time.perf_counter() - start_time,
                hashlib.blake2s(repr(session).encode(), digest_size=4).hexdigest(),
            )
            return response
        finally:
            for image_id in pinned_ids:
                await self.image_cache.release_image(image_id)
            self._load.end(time.perf_counter() - start_time)

    async def _watch_rpc_cancel(self, context: ServicerContext, stop_growth: threading.Event) -> None:
        """Sets ``stop_growth`` when the client drops the RPC, so tile growth stops between SAM2 calls."""
        try:
            while not stop_growth.is_set():
                if context.cancelled():
                    stop_growth.set()
                    return
                await asyncio.sleep(0.05)
        except asyncio.CancelledError:
            return

    async def _pin_remembered_tiles(
        self,
        identity: TileCoord,
        session: tuple,
        predictors: dict,
        pinned_ids: List[int],
        images: dict,
    ) -> None:
        """Pin embeddings still cached for cells this prompt set already segmented.

        A remembered mask does not need the predictor. Pinning one lets a cache
        miss run SAM2. If the embedding is gone, the walk asks for the cell again
        and the stored mask stays in the composite.
        """
        for index in self._growth_memory.tile_indexes(session):
            if (index.row, index.col) in predictors:
                continue
            key = (
                identity.volume,
                int(identity.section),
                identity.channel,
                identity.transform,
                int(identity.downsample),
                index.row,
                index.col,
            )
            pinned = await self.image_cache.get_image_by_tile(key)
            if pinned is None:
                continue
            image_id, image_bytes, width, height, predictor, predictor_lock = pinned
            pinned_ids.append(image_id)
            _remember_tile_image(images, index.row, index.col, image_bytes)
            if predictor is None:
                continue
            predictors[(index.row, index.col)] = (predictor, predictor_lock, height, width)

    async def SegmentImage(
        self,
        request: SegmentationRequest,
        context: ServicerContext,
    ) -> SegmentationResponse:
        """Segment from coordinates/labels using image_id or inline image_data."""
        coordinates, labels = self._extract_coordinates_and_labels_from_segment_request(request)
        self._load.begin()
        start = time.perf_counter()
        try:
            return await self._dispatch_segmentation(
                request.image_id,
                request.image_data,
                request.width,
                request.height,
                coordinates,
                labels,
                request.multimask_output,
                context,
                _EMPTY_COORDINATES_MESSAGE,
            )
        finally:
            self._load.end(time.perf_counter() - start)

    async def MultiSegmentImage(
        self,
        request: MultiSegmentationRequest,
        context: ServicerContext,
    ) -> SegmentationResponse:
        """Segment from a point-id map; id 0 is background, other ids are foreground."""
        coordinates, labels = self._extract_coordinates_and_labels_from_multi_request(request)
        self._load.begin()
        start = time.perf_counter()
        try:
            return await self._dispatch_segmentation(
                request.image_id,
                request.image_data,
                request.width,
                request.height,
                coordinates,
                labels,
                request.multimask_output,
                context,
                _EMPTY_FOREGROUND_POINTS_MESSAGE,
            )
        finally:
            self._load.end(time.perf_counter() - start)

    async def SegmentImageSets(self, request_iterator, context: ServicerContext):
        """Predict each streamed point set as it arrives and yield one response per set.

        Auto-segmentation clients Delaunay-decimate one object at a time. Reading the
        stream incrementally lets the GPU run set k while the client prepares set k+1.
        """
        image_id = 0
        async for request in request_iterator:
            if request.image_id:
                if image_id and request.image_id != image_id:
                    await context.abort(grpc.StatusCode.INVALID_ARGUMENT, _MIXED_IMAGE_ID_MESSAGE)
                    return
                image_id = request.image_id
            if not image_id:
                await context.abort(grpc.StatusCode.INVALID_ARGUMENT, _MISSING_IMAGE_ID_MESSAGE)
                return

            coordinates, labels = _points_from_set(request)
            self._load.begin()
            start = time.perf_counter()
            try:
                response = await self._dispatch_segmentation(
                    image_id,
                    b"",
                    0,
                    0,
                    coordinates,
                    labels,
                    request.multimask_output,
                    context,
                    _EMPTY_COORDINATES_MESSAGE,
                )
            finally:
                self._load.end(time.perf_counter() - start)
            yield response


def _tile_contains_foreground(tile: TileCoord, foreground: List[Tuple[int, int]]) -> bool:
    """True when a mosaic foreground point lies in this cell.

    SegmentTiles returns NOT_FOUND for that cell so the client re-uploads the seed.
    A missing cell with no foreground point is left out of the uploaded set; growth
    asks for it only when the mask reaches the shared border.
    """
    target = TileIndex(row=int(tile.row), col=int(tile.col))
    return any(tile_of_point(int(x), int(y)) == target for x, y in foreground)


def _tile_cache_key(coord: TileCoord) -> TileCacheKey:
    return (
        coord.volume,
        int(coord.section),
        coord.channel,
        coord.transform,
        int(coord.downsample),
        int(coord.row),
        int(coord.col),
    )


def _same_tile_identity(left: TileCoord, right: TileCoord) -> bool:
    return (
        left.volume == right.volume
        and left.section == right.section
        and left.channel == right.channel
        and left.transform == right.transform
        and left.downsample == right.downsample
    )


def _points_from_set(request: SegmentImageSetRequest) -> Tuple[List[Tuple[int, int]], List[int]]:
    """Foreground points are label 1 and background points are label 0."""
    coordinates: List[Tuple[int, int]] = [(point.x, point.y) for point in request.foreground]
    labels: List[int] = [1] * len(coordinates)
    coordinates.extend((point.x, point.y) for point in request.background)
    labels.extend(0 for _ in request.background)
    return coordinates, labels


def resolve_tls_pem_paths(
    cert_path: Optional[str] = None,
    key_path: Optional[str] = None,
) -> Optional[Tuple[str, str]]:
    """Return Let's Encrypt PEM paths when both files exist.

    Unset paths or files that certbot has not written yet leave TLS unbound so the
    process can still serve the legacy cleartext port.
    """
    if cert_path is None:
        cert_path = os.environ.get("SSL_CERT_PATH", "")
    if key_path is None:
        key_path = os.environ.get("SSL_KEY_PATH", "")
    cert_path = (cert_path or "").strip()
    key_path = (key_path or "").strip()
    if not cert_path or not key_path:
        logger.info("SSL_CERT_PATH or SSL_KEY_PATH unset; TLS listener disabled")
        return None
    if not os.path.isfile(cert_path) or not os.path.isfile(key_path):
        logger.warning(
            "TLS certificate files not found (cert=%s, key=%s); serving cleartext only until certbot enrolls",
            cert_path,
            key_path,
        )
        return None
    return cert_path, key_path


def load_server_credentials(
    cert_path: Optional[str] = None,
    key_path: Optional[str] = None,
):
    """Load PEM server credentials from SSL_CERT_PATH and SSL_KEY_PATH.

    Returns None when the paths are unset or the files are not on disk yet so the
    process can serve the legacy cleartext port while certbot finishes enrollment.
    """
    pem_paths = resolve_tls_pem_paths(cert_path, key_path)
    if pem_paths is None:
        return None
    cert_file_path, key_file_path = pem_paths
    with open(key_file_path, "rb") as key_file:
        private_key = key_file.read()
    with open(cert_file_path, "rb") as cert_file:
        certificate_chain = cert_file.read()
    return grpc.ssl_server_credentials(((private_key, certificate_chain),))


async def serve(
    port: int = 50051,
    max_workers: int = 10,
    inference_workers: int = 1,
    cache_ttl_seconds: int = DEFAULT_TTL_SECONDS,
    cache_max_memory_bytes: int = DEFAULT_MAX_MEMORY_BYTES,
    cache_max_images: int = DEFAULT_MAX_ENTRIES,
    compile_image_encoder: bool = True,
    demo_site: bool = False,
    demo_port: int = 8443,
) -> None:
    """Listen on cleartext `[::]:port` and, when PEMs exist, TLS `[::]:443`.

    The cleartext port is legacy. It stays until every client uses TLS.
    inference_workers defaults to 1 because concurrent SAM2 predict() calls on one
    GPU typically contend for VRAM rather than increase throughput.
    demo_site binds a separate HTTPS port only when certificate files exist.
    """
    install_tile_work_logging()
    install_sam2_log_filter()
    server_start_time = time.monotonic()
    inference_executor = futures.ThreadPoolExecutor(
        max_workers=inference_workers, thread_name_prefix="sam2-infer"
    )
    grpc_executor = futures.ThreadPoolExecutor(
        max_workers=max_workers, thread_name_prefix="grpc-cb"
    )

    server = grpc.aio.server(
        grpc_executor,
        options=[
            ('grpc.max_send_message_length', 64 * 1024 * 1024),
            ('grpc.max_receive_message_length', 64 * 1024 * 1024),
        ],
    )
    servicer = SegmentationServicer(
        inference_executor=inference_executor,
        server_start_time=server_start_time,
        inference_workers=inference_workers,
        cache_ttl_seconds=cache_ttl_seconds,
        cache_max_memory_bytes=cache_max_memory_bytes,
        cache_max_images=cache_max_images,
        compile_image_encoder=compile_image_encoder,
    )
    add_SegmentationServiceServicer_to_server(servicer, server)

    insecure_address = f'[::]:{port}'
    server.add_insecure_port(insecure_address)
    logger.warning(
        "Cleartext gRPC on %s is legacy and will be removed in a future release "
        "after every client uses TLS on port %s.",
        insecure_address,
        _TLS_PORT,
    )
    bound = [insecure_address]
    pem_paths = resolve_tls_pem_paths()
    credentials = load_server_credentials() if pem_paths is not None else None
    if credentials is not None:
        tls_address = f'[::]:{_TLS_PORT}'
        server.add_secure_port(tls_address, credentials)
        bound.append(tls_address)
    await server.start()
    logger.info("Server started, listening on %s (inference_workers=%s)", ", ".join(bound), inference_workers)

    loop = asyncio.get_running_loop()
    # Import here so demo_site can import resolve_tls_pem_paths without a cycle.
    from segmentation_server.demo_site import start_demo_site

    grace_seconds = 5
    demo_listener = None

    def _request_stop() -> None:
        asyncio.ensure_future(server.stop(grace_seconds))

    try:
        demo_listener = start_demo_site(
            enabled=demo_site,
            port=demo_port,
            servicer=servicer,
            loop=loop,
            pem_paths=pem_paths,
        )
        try:
            loop.add_signal_handler(signal.SIGTERM, _request_stop)
            loop.add_signal_handler(signal.SIGINT, _request_stop)
        except NotImplementedError:
            for sig in (signal.SIGTERM, signal.SIGINT):
                try:
                    signal.signal(sig, lambda s, f: loop.call_soon_threadsafe(_request_stop))
                except (ValueError, OSError):
                    pass
        await server.wait_for_termination()
    except asyncio.CancelledError:
        await server.stop(grace_seconds)
    finally:
        if demo_listener is not None:
            demo_listener.close()
        inference_executor.shutdown(wait=False, cancel_futures=True)
        grpc_executor.shutdown(wait=False, cancel_futures=True)


if __name__ == '__main__':
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    asyncio.run(serve())
