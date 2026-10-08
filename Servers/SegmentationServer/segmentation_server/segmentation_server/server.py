"""gRPC server that exposes SAM2 segmentation over SegmentationService."""

from __future__ import annotations

import asyncio
import functools
import hashlib
import importlib.metadata
import logging
import os
import signal
import threading
import time
from concurrent import futures
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any, Callable, List, Optional, Tuple

import grpc
import numpy as np
from grpc.aio import ServicerContext
from numpy.typing import NDArray

from segmentation_grpc import (
    DeleteImageRequest,
    DeleteImageResponse,
    MultiSegmentationRequest,
    SegmentationRequest,
    SegmentationResponse,
    SegmentationServiceServicer,
    SegmentImageSetRequest,
    SegmentTilesRequest,
    SegmentTilesStreamRequest,
    SegmentTilesStreamResponse,
    ServerStatusRequest,
    ServerStatusResponse,
    TileCoord,
    TilesNeeded,
    UploadImageRequest,
    UploadImageResponse,
    UploadTileRequest,
    UploadTileResponse,
    add_SegmentationServiceServicer_to_server,
)
from segmentation_server.cuda_errors import UnrecoverableGpuError
from segmentation_server.image_cache import (
    CacheFullError,
    DEFAULT_MAX_ENTRIES,
    DEFAULT_MAX_MEMORY_BYTES,
    DEFAULT_TTL_SECONDS,
    ImageCache,
    TileCacheKey,
)
from segmentation_server.debug_dump import dump_enabled, dump_growth
from segmentation_server.circle_boxes import find_circles, outer_ring_box_enabled
from segmentation_server.healthcheck import forget_listening_port, record_listening_port
from segmentation_server.responses import build_segmentation_response
from segmentation_server.prompt_log import describe_prompts
from segmentation_server.mask_utils import (
    DEFAULT_MASK_THRESHOLD,
    NoMatchingMask,
    SegmentInfo,
    combined_mask_to_segments,
    prepare_image_for_sam2,
)
from segmentation_server.tile_requests import TileRequestRegistry
from segmentation_server.tile_work import (
    install_sam2_log_filter,
    install_tile_work_logging,
    tile_work,
)
from segmentation_server.cell_grid import (
    TILE_SIZE,
    Cell,
    TileIndex,
    crop_window,
    is_aligned,
    tile_of_point,
    tiles_for_cell,
)
from segmentation_server.tile_growth import (
    CellPredict,
    GrowthCancelled,
    GrowthWalk,
    PredictUnavailable,
    margin_logit_min_from_env,
    owner_veto_logit_from_env,
    max_requested_tiles_from_env,
)

if TYPE_CHECKING:
    from segmentation_server.segmentation_service import SegmentationModel

logger = logging.getLogger(__name__)

_EMPTY_COORDINATES_MESSAGE = "No coordinates provided. At least one coordinate point is required."
_MISSING_IMAGE_ID_MESSAGE = "image_id is required on the first SegmentImageSets message."
_MIXED_IMAGE_ID_MESSAGE = "SegmentImageSets messages must use one image_id for the whole stream."
_EMPTY_FOREGROUND_POINTS_MESSAGE = "No foreground_points provided. At least one point is required."


class _ProtocolError(Exception):
    """The client sent a SegmentTilesStream message that the conversation does not allow."""


@dataclass(frozen=True)
class RequestLimits:
    """Largest requests the server will work on. Each is an environment variable.

    A client can otherwise send an unbounded list of tiles, prompt points or boxes, or stream
    prompt sets forever, and the server would allocate and loop for as long as it was asked.
    Going over a limit is ``RESOURCE_EXHAUSTED``, named in the message with the limit that was hit.
    """

    max_tiles: int = 64
    max_points: int = 512
    max_boxes: int = 16
    max_sets: int = 5000
    max_answer_tiles: int = 4096
    max_concurrent_streams: int = 32

    @classmethod
    def from_env(cls) -> "RequestLimits":
        def read(name: str, default: int) -> int:
            raw = os.environ.get(name, "").strip()
            if not raw:
                return default
            try:
                return max(1, int(raw))
            except ValueError:
                return default

        defaults = cls()
        return cls(
            max_tiles=read("SEGMENTATION_MAX_TILES", defaults.max_tiles),
            max_points=read("SEGMENTATION_MAX_POINTS", defaults.max_points),
            max_boxes=read("SEGMENTATION_MAX_BOXES", defaults.max_boxes),
            max_sets=read("SEGMENTATION_MAX_SETS", defaults.max_sets),
            max_answer_tiles=read("SEGMENTATION_MAX_ANSWER_TILES", defaults.max_answer_tiles),
            max_concurrent_streams=read(
                "SEGMENTATION_MAX_CONCURRENT_STREAMS", defaults.max_concurrent_streams
            ),
        )


class RpcCancelled(Exception):
    """The client dropped the RPC before queued inference work started."""


def _predictor_state_invalid(error: BaseException) -> bool:
    """True when ``error`` says the cached predictor has no usable image embedding.

    SAM2 raises a RuntimeError about ``set_image`` when predict() runs on a predictor whose
    features are gone. Every later call on that cache entry would fail the same way, so only
    this case justifies dropping the entry. Any other failure (a bad prompt, a transient GPU
    error) leaves a healthy embedding in place; evicting it would force a re-upload and a
    re-encode for nothing.
    """
    return isinstance(error, RuntimeError) and "set_image" in str(error)


class _AnswerTimeout(Exception):
    """The client did not answer a TilesNeeded within the idle limit."""


DEFAULT_TILE_ANSWER_TIMEOUT_SECONDS = 60.0


def tile_answer_timeout_seconds() -> float:
    """Idle limit for a TilesAnswer (SEGMENTATION_TILE_ANSWER_TIMEOUT_SECONDS, default 60).

    While the walk waits for tiles it holds their cache pins and a stream slot. A client that
    asks and then goes quiet without closing the call would hold them forever.
    """
    raw = os.environ.get("SEGMENTATION_TILE_ANSWER_TIMEOUT_SECONDS", "").strip()
    if not raw:
        return DEFAULT_TILE_ANSWER_TIMEOUT_SECONDS
    try:
        return max(0.1, float(raw))
    except ValueError:
        return DEFAULT_TILE_ANSWER_TIMEOUT_SECONDS


DEFAULT_TILE_SHARE_TIMEOUT_SECONDS = 5.0


def tile_share_timeout_seconds() -> float:
    """How long a stream waits for another stream's upload of a tile (SEGMENTATION_TILE_SHARE_TIMEOUT_SECONDS, default 5).

    When one stream has already asked its client for a tile, a second stream that needs the same
    tile waits this long for it to reach the cache before asking its own client. ``0`` turns the
    sharing off, so every stream asks for every tile it needs.
    """
    raw = os.environ.get("SEGMENTATION_TILE_SHARE_TIMEOUT_SECONDS", "").strip()
    if not raw:
        return DEFAULT_TILE_SHARE_TIMEOUT_SECONDS
    try:
        return max(0.0, float(raw))
    except ValueError:
        return DEFAULT_TILE_SHARE_TIMEOUT_SECONDS


DEFAULT_MAX_CONCURRENT_RPCS = 256


def max_concurrent_rpcs_from_env() -> int:
    """Most RPCs the server works on at once (SEGMENTATION_MAX_CONCURRENT_RPCS, default 256)."""
    raw = os.environ.get("SEGMENTATION_MAX_CONCURRENT_RPCS", "").strip()
    if not raw:
        return DEFAULT_MAX_CONCURRENT_RPCS
    try:
        return max(1, int(raw))
    except ValueError:
        return DEFAULT_MAX_CONCURRENT_RPCS


_READ_ENDED = object()


class _StreamReader:
    """Reads a request stream without losing a message when a wait is interrupted.

    While a stream waits for tiles it also waits on other streams' uploads. A read cancelled by
    the other event would drop the message it was about to deliver, so one read is started and
    kept until a caller takes its result.
    """

    def __init__(self, request_iterator) -> None:
        self._iterator = request_iterator
        self._read: Optional["asyncio.Future[Any]"] = None

    async def _next(self):
        try:
            return await self._iterator.__anext__()
        except StopAsyncIteration:
            return _READ_ENDED

    def pending(self) -> "asyncio.Future[Any]":
        """The read in progress, started on first use. Await it with ``asyncio.wait``, then :meth:`take`."""
        if self._read is None:
            self._read = asyncio.ensure_future(self._next())
        return self._read

    def take(self) -> Optional[SegmentTilesStreamRequest]:
        """The finished read's message, or None once the client has ended its half of the stream."""
        read, self._read = self._read, None
        assert read is not None and read.done()
        message = read.result()
        return None if message is _READ_ENDED else message

    def close(self) -> None:
        """Stop a read nobody will collect."""
        read, self._read = self._read, None
        if read is None:
            return
        if not read.done():
            read.cancel()
        elif not read.cancelled():
            read.exception()


@dataclass
class _SharedTile:
    """A tile another stream is already fetching, and when this stream stops waiting for it."""

    tile: TileIndex
    key: TileCacheKey
    future: "asyncio.Future[bool]"
    deadline: float


@dataclass
class _TileWait:
    """Where one round of tile fetching stands: what the client was asked, and what is borrowed.

    ``asked`` holds the tiles this stream's client owes an answer for. ``to_send`` holds the ones
    among them the client has not been told about yet. ``shared`` holds tiles another stream is
    fetching; each moves to ``asked`` if its wait runs out or the other stream gives up.
    """

    asked: set = field(default_factory=set)
    to_send: List[TileIndex] = field(default_factory=list)
    shared: dict = field(default_factory=dict)
    unavailable: List[TileIndex] = field(default_factory=list)
    answer_deadline: float = 0.0

    def ask(self, tile: TileIndex) -> None:
        self.asked.add((tile.row, tile.col))
        self.to_send.append(tile)

    def take_to_send(self) -> List[TileIndex]:
        sending, self.to_send = self.to_send, []
        return sending


async def _next_message(
    request_iterator, timeout_seconds: Optional[float] = None
) -> Optional[SegmentTilesStreamRequest]:
    """The next client message, or None once the client has ended its half of the stream.

    Raises:
        _AnswerTimeout: ``timeout_seconds`` passed with nothing from the client.
    """
    try:
        if timeout_seconds is None:
            return await request_iterator.__anext__()
        return await asyncio.wait_for(request_iterator.__anext__(), timeout_seconds)
    except StopAsyncIteration:
        return None
    except asyncio.TimeoutError as error:
        raise _AnswerTimeout() from error


def _coord_for(identity: TileCoord, tile: TileIndex) -> TileCoord:
    """The TileCoord of ``tile`` in the same volume, section, channel, transform and downsample as ``identity``."""
    return TileCoord(
        volume=identity.volume,
        section=identity.section,
        channel=identity.channel,
        transform=identity.transform,
        downsample=identity.downsample,
        row=tile.row,
        col=tile.col,
    )


class TileImages:
    """The pinned tiles of one request, decoded only when a cell window is cropped from them.

    A walk over aligned cells predicts from the pinned embeddings and never touches pixels, so
    decoding every pinned tile up front (3 MB each) was wasted work. Offset cells are cropped
    from two or four tiles; those are decoded on first use and kept for the request. It behaves
    like the ``(row, col) -> image`` mapping :func:`crop_window` expects. Only the walk thread
    decodes; the event loop adds tiles while the walk is paused.
    """

    def __init__(self) -> None:
        self._encoded: dict[Tuple[int, int], bytes] = {}
        self._decoded: dict[Tuple[int, int], NDArray[np.uint8]] = {}

    def add(self, row: int, col: int, image_bytes: bytes) -> None:
        """Remember a tile's encoded bytes (a tile already held keeps its bytes)."""
        if image_bytes and (row, col) not in self._encoded:
            self._encoded[(row, col)] = image_bytes

    def __contains__(self, key: object) -> bool:
        return key in self._encoded

    def __len__(self) -> int:
        return len(self._encoded)

    def __getitem__(self, key: Tuple[int, int]) -> NDArray[np.uint8]:
        image = self._decoded.get(key)
        if image is not None:
            return image
        encoded = self._encoded.get(key)
        if encoded is None:
            raise KeyError(key)
        try:
            image = prepare_image_for_sam2(encoded)
        except (OSError, ValueError):
            # Forget the tile so growth asks for it again instead of predicting on a partial window.
            logger.exception("Could not decode tile row=%s col=%s for a cell window", key[0], key[1])
            del self._encoded[key]
            raise KeyError(key) from None
        self._decoded[key] = image
        return image

    def get(self, key: Tuple[int, int], default: Any = None) -> Any:
        try:
            return self[key]
        except KeyError:
            return default

    @property
    def decoded_count(self) -> int:
        """How many tiles have actually been decoded (for tests and logs)."""
        return len(self._decoded)


def _remember_tile_image(images: TileImages, row: int, col: int, image_bytes: bytes) -> None:
    """Hand a pinned tile's bytes to the request's :class:`TileImages`."""
    images.add(row, col, image_bytes)


@dataclass
class _TileSession:
    """What one ``SegmentTilesStream`` call holds: its tiles' identity, pins, predictors and pixels."""

    identity: TileCoord
    pinned_ids: List[int] = field(default_factory=list)
    predictors: dict = field(default_factory=dict)
    images: "TileImages" = field(default_factory=lambda: TileImages())
    uploaded: List[TileIndex] = field(default_factory=list)
    # Names this stream to the TileRequestRegistry. The session itself is unhashable.
    token: object = field(default_factory=object)


@dataclass(frozen=True)
class _PredictSettings:
    """The per-request SAM2 options every cell prediction uses."""

    multimask_output: bool
    mask_threshold: float
    use_mask_input: bool

    @classmethod
    def from_request(cls, request: SegmentTilesRequest) -> "_PredictSettings":
        return cls(
            multimask_output=request.multimask_output,
            mask_threshold=(
                float(request.mask_threshold) if request.HasField("mask_threshold") else DEFAULT_MASK_THRESHOLD
            ),
            use_mask_input=bool(request.use_mask_input),
        )


def _resolve_boxes(
    request: SegmentTilesRequest, foreground: List[Tuple[int, int]]
) -> Tuple[List[Tuple[int, int, int, int]], List[Tuple[int, int]], bool]:
    """The boxes the starting cells get, the clicks a boxed prompt leaves out, and who chose the boxes.

    A client that sends boxes chose them and the clicks that go with them, so nothing is inferred
    and no click is left out; the third value is True and the boxes are taken as object. Otherwise
    nine-click circles are recognised and boxed from inside, and the third value is False.
    """
    client_boxes = _client_boxes(request.foreground_boxes)
    if client_boxes:
        logger.info(
            "SegmentTiles client boxes (x0,y0,x1,y1 mosaic, Y up): %s; %d foreground click(s) sent as given",
            client_boxes,
            len(foreground),
        )
        return client_boxes, [], True
    circles = find_circles(foreground) if outer_ring_box_enabled() else []
    boxes = [circle.box for circle in circles]
    omit_with_box = [click for circle in circles for click in circle.ring_clicks]
    if boxes:
        logger.info(
            "SegmentTiles circle boxes (x0,y0,x1,y1 mosaic, Y up): %s; "
            "ring clicks left out of boxed prompts (box + center only): %d",
            boxes,
            len(omit_with_box),
        )
    return boxes, omit_with_box, False


def _session_tag(
    identity: TileCoord, foreground: List[Tuple[int, int]], background: List[Tuple[int, int]]
) -> str:
    """Short stable tag for one prompt set, used in log lines and debug dump names."""
    return hashlib.blake2s(
        repr((
            identity.volume,
            int(identity.section),
            identity.channel,
            identity.transform,
            int(identity.downsample),
            sorted(foreground),
            sorted(background),
        )).encode(),
        digest_size=4,
    ).hexdigest()


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
            cache_max_memory_bytes: Image-byte cap for the cache (default 16 GiB).
                Shared tiles are kept until this cap, or until the GPU needs room.
            cache_ttl_seconds: Unused-entry lifetime for arbitrary uploads (default 5 minutes).
                Shared tiles are not expired by the TTL.
            cache_max_images: Max arbitrary uploads (default 4096). Shared tiles do not count.
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
            predictor_generation=self._predictor_encoder_generation,
            current_generation=self._current_encoder_generation,
        )
        self._max_requested_tiles = max_requested_tiles_from_env()
        self._tile_answer_timeout = tile_answer_timeout_seconds()
        self._tile_share_timeout = tile_share_timeout_seconds()
        self._tile_requests = TileRequestRegistry()
        self._limits = RequestLimits.from_env()
        self._active_streams = 0
        # Image decoding and response encoding are CPU work. They get their own small pool so a
        # burst of uploads can neither starve the SAM2 pool nor spawn threads without limit.
        self._cpu_executor = futures.ThreadPoolExecutor(
            max_workers=max(2, min(8, os.cpu_count() or 4)), thread_name_prefix="seg-cpu"
        )
        try:
            self._loop: Optional[asyncio.AbstractEventLoop] = asyncio.get_running_loop()
        except RuntimeError:
            self._loop = None
        if hasattr(self.model, "set_on_compiled_ready"):
            self.model.set_on_compiled_ready(self._flush_cache_after_compile)

    def close(self) -> None:
        """Release the servicer's own thread pool. The inference pool belongs to whoever passed it in."""
        self._cpu_executor.shutdown(wait=False, cancel_futures=True)

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

    def _predictor_encoder_generation(self, predictor: Any) -> Optional[str]:
        """The encoder generation ``predictor`` was built with, or None for a model that has none."""
        read = getattr(self.model, "predictor_generation", None)
        if not callable(read):
            return None
        generation = read(predictor)
        return generation if isinstance(generation, str) else None

    def _current_encoder_generation(self) -> Optional[str]:
        """``"eager"`` or ``"compiled"``, or None for a model that does not report one."""
        generation = getattr(self.model, "encoder_generation", None)
        return generation if isinstance(generation, str) else None

    def _flush_cache_after_compile(self) -> None:
        """Drop embeddings made with the eager encoder once the compiled encoder is serving.

        Entries already built with the compiled encoder are kept, so a client that uploaded
        after the swap keeps its image id. Runs on the compile thread, so the flush is handed
        to the event loop that owns the cache.
        """
        if self._loop is None or self._loop.is_closed():
            logger.warning("Compiled encoder ready but no event loop; image cache not flushed")
            return
        future = asyncio.run_coroutine_threadsafe(self.image_cache.flush_stale(), self._loop)
        future.add_done_callback(self._log_flush_result)

    @staticmethod
    def _log_flush_result(future: "futures.Future[int]") -> None:
        error = future.exception()
        if error is not None:
            logger.error("Flushing eager embeddings after the compile swap failed", exc_info=error)

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
        """Encode labeled PNG plus per-segment cropped masks and polygons (see :mod:`responses`)."""
        return build_segmentation_response(labeled_image, segments, width, height, omit_labeled_image)

    async def _build_response_off_loop(
        self,
        labeled_image: NDArray[np.uint16],
        segments: List[SegmentInfo],
        width: int,
        height: int,
        omit_labeled_image: bool = False,
    ) -> SegmentationResponse:
        """:meth:`_build_segmentation_response` on a worker thread.

        Hole filling, bounds, PNG encoding and polygon extraction are CPU work that grows with
        the mask. On the event loop they would delay every other RPC and the cancel watchers.
        The servicer's CPU pool is used so the GPU-bound inference pool is not tied up by it.
        """
        return await asyncio.get_running_loop().run_in_executor(
            self._cpu_executor,
            functools.partial(
                self._build_segmentation_response,
                labeled_image,
                segments,
                width,
                height,
                omit_labeled_image=omit_labeled_image,
            ),
        )

    async def _abort_no_matching_mask(self, error: NoMatchingMask, context: ServicerContext) -> None:
        """Abort with FAILED_PRECONDITION and the stable ``NO_MATCHING_MASK`` prefix.

        The prompt produced no mask that fits it. The client logs the message; there is no
        fallback result.
        """
        logger.error("NO_MATCHING_MASK: %s", error)
        await context.abort(grpc.StatusCode.FAILED_PRECONDITION, f"NO_MATCHING_MASK {error}")

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

    async def _abort_over_limit(self, context: ServicerContext, what: str, count: int, limit: int, variable: str) -> None:
        """Abort with RESOURCE_EXHAUSTED naming the limit that was exceeded and how to raise it."""
        logger.warning("Request refused: %s=%s is over the limit of %s", what, count, limit)
        await context.abort(
            grpc.StatusCode.RESOURCE_EXHAUSTED,
            f"Request has {count} {what}; the server accepts at most {limit} ({variable}).",
        )

    async def _validate_coordinates(
        self,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        context: ServicerContext,
        empty_message: str = _EMPTY_COORDINATES_MESSAGE,
    ) -> bool:
        """Abort with INVALID_ARGUMENT on empty points or label-length mismatch, RESOURCE_EXHAUSTED on too many."""
        if len(coordinates) == 0:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, empty_message)
            return True

        if len(coordinates) > self._limits.max_points:
            await self._abort_over_limit(
                context, "points", len(coordinates), self._limits.max_points, "SEGMENTATION_MAX_POINTS"
            )
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
        # A protobuf map has no defined order; sort by id so the same request is the same prompt.
        for point_id, point in sorted(request.foreground_points.items()):
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
        cancelled: Optional[threading.Event] = None,
    ) -> Tuple[NDArray[np.uint16], List[SegmentInfo]]:
        """Executor entry: serialize predict() on one cached predictor.

        ``cancelled`` is set when the RPC was cancelled. It is checked after the predictor lock is
        won, because a job can wait on that lock behind another request for a long time and its
        caller may be gone by then.
        """
        with predictor_lock:
            if cancelled is not None and cancelled.is_set():
                raise RpcCancelled()
            return self.model.segment_image_with_predictor(
                predictor=predictor,
                coordinates=coordinates,
                labels=labels,
                multimask_output=multimask_output,
                empty_shape=empty_shape,
            )

    def _segment_inline_image(
        self,
        image_np: NDArray[np.uint8],
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        multimask_output: bool,
        cancelled: Optional[threading.Event] = None,
    ) -> Tuple[NDArray[np.uint16], List[SegmentInfo]]:
        """Executor entry for the uncached set_image() + predict() path on an already decoded image."""
        if cancelled is not None and cancelled.is_set():
            raise RpcCancelled()
        return self.model.segment_image(
            image_np=image_np,
            coordinates=coordinates,
            labels=labels,
            multimask_output=multimask_output,
        )

    async def _run_inference(self, cancelled: threading.Event, function: Callable[..., Any], *args: Any) -> Any:
        """Run ``function`` on the inference executor; a cancelled RPC marks ``cancelled`` first.

        grpc.aio cancels the handler coroutine when the client drops the call. That cancels a job
        still queued on the executor, but not one that already started, so the event lets such a
        job notice (see :meth:`_segment_with_locked_predictor`) before it spends GPU time.
        """
        try:
            return await asyncio.get_running_loop().run_in_executor(
                self.inference_executor, function, *args, cancelled
            )
        except asyncio.CancelledError:
            cancelled.set()
            raise

    async def _decode_image_or_abort(
        self,
        image_data: bytes,
        width: int,
        height: int,
        context: ServicerContext,
        missing_message: str,
    ) -> Optional[NDArray[np.uint8]]:
        """Decode off the event loop and check the pixels against the declared size.

        Aborts with INVALID_ARGUMENT and returns None for empty bytes, a non-positive size, an
        unreadable or over-limit image, or a decoded size that is not ``width`` x ``height``. A
        wrong declared size would otherwise give masks in a different frame from the image.
        """
        if not image_data or width <= 0 or height <= 0:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, missing_message)
            return None
        try:
            decoded = await asyncio.get_running_loop().run_in_executor(
                self._cpu_executor, prepare_image_for_sam2, image_data
            )
        except (OSError, ValueError) as e:
            # The decoder's own message names library internals; the client only needs to know
            # the bytes were not an image.
            logger.warning("Could not decode image_data (%s bytes): %s", len(image_data), e)
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, "Could not decode image_data as an image.")
            return None
        actual_height, actual_width = decoded.shape[:2]
        if actual_width != width or actual_height != height:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                f"width/height {width}x{height} do not match decoded image {actual_width}x{actual_height}.",
            )
            return None
        return decoded

    async def _handle_cached_image_segmentation(
        self,
        image_id: int,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        multimask_output: bool,
        context: ServicerContext,
        empty_message: str,
        omit_labeled_image: bool = False,
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
                labeled_image, segments = await self._run_inference(
                    threading.Event(),
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
            except NoMatchingMask as e:
                await self._abort_no_matching_mask(e, context)
                return SegmentationResponse()
            except Exception as e:
                logger.exception("Predictor error for image ID %s", image_id)
                if _predictor_state_invalid(e):
                    # The embedding itself is gone, so every later call would fail the same way.
                    await self.image_cache.delete_image(image_id)
                    message = "Error processing segmentation request. The image has been removed from the cache; please re-upload it."
                else:
                    message = "Error processing segmentation request."
                await context.abort(grpc.StatusCode.INTERNAL, message)
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
            return await self._build_response_off_loop(
                labeled_image, segments, width, height, omit_labeled_image=omit_labeled_image
            )
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
        omit_labeled_image: bool = False,
    ) -> SegmentationResponse:
        """Segment an image sent on the request. Aborts the RPC on input or model errors."""
        start_time = time.perf_counter()
        logger.debug("Using inline image data: %sx%s", width, height)

        if await self._validate_coordinates(coordinates, labels, context, empty_message=empty_message):
            return SegmentationResponse()

        image_np = await self._decode_image_or_abort(
            image_data,
            width,
            height,
            context,
            "Provide image_id from UploadImage, or image_data with positive width and height.",
        )
        if image_np is None:
            return SegmentationResponse()

        try:
            labeled_image, segments = await self._run_inference(
                threading.Event(),
                self._segment_inline_image,
                image_np,
                coordinates,
                labels,
                multimask_output,
            )
        except UnrecoverableGpuError as e:
            await self._abort_unrecoverable_gpu(e, context)
            return SegmentationResponse()
        except NoMatchingMask as e:
            await self._abort_no_matching_mask(e, context)
            return SegmentationResponse()
        except Exception:
            logger.exception("Error during inline segmentation")
            await context.abort(grpc.StatusCode.INTERNAL, "Error processing request.")
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
        return await self._build_response_off_loop(
            labeled_image, segments, width, height, omit_labeled_image=omit_labeled_image
        )

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
        omit_labeled_image: bool = False,
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
                image_id, coordinates, labels, multimask_output, context, empty_message, omit_labeled_image
            )
        return await self._handle_inline_image_segmentation(
            image_data, width, height, coordinates, labels, multimask_output, context, empty_message,
            omit_labeled_image,
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

        decoded = await self._decode_image_or_abort(
            image_data,
            width,
            height,
            context,
            "Upload requires non-empty image_data and positive width and height.",
        )
        if decoded is None:
            return UploadImageResponse()

        logger.info("UploadImage RPC: %sx%s, %s bytes", width, height, len(image_data))
        self._load.begin()
        start_time = time.perf_counter()
        try:
            image_id: int = await self.image_cache.upload_image(
                image_data, width, height, executor=self.inference_executor, decoded=decoded
            )
        except UnrecoverableGpuError as e:
            logger.exception("Error uploading image")
            await self._abort_unrecoverable_gpu(e, context)
            raise
        except CacheFullError as e:
            logger.warning("UploadImage refused: %s", e)
            await context.abort(grpc.StatusCode.RESOURCE_EXHAUSTED, f"Image cache is full: {e}")
            raise
        except Exception:
            logger.exception("Error uploading image")
            await context.abort(grpc.StatusCode.INTERNAL, "Error uploading image.")
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
        except Exception:
            logger.exception("Error deleting image")
            await context.abort(grpc.StatusCode.INTERNAL, "Error deleting image.")
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
        encoder_generation = self._current_encoder_generation() or ""
        in_flight, recent_latency_ms = self._load.snapshot()
        return ServerStatusResponse(
            version=version,
            encoder_generation=encoder_generation,
            compile_status=compile_status if isinstance(compile_status, str) else "off",
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
        decoded = await self._decode_image_or_abort(
            image_data,
            request.width,
            request.height,
            context,
            "UploadTile requires non-empty image_data.",
        )
        if decoded is None:
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
                decoded=decoded,
            )
            if already_cached and hasattr(self.model, "note_disk_embedding_used"):
                try:
                    self.model.note_disk_embedding_used(image_data, decoded)
                except Exception:
                    logger.exception("Embedding disk touch failed")
        except UnrecoverableGpuError as e:
            await self._abort_unrecoverable_gpu(e, context)
            return UploadTileResponse()
        except CacheFullError as e:
            logger.warning("UploadTile refused: %s", e)
            await context.abort(grpc.StatusCode.RESOURCE_EXHAUSTED, f"Image cache is full: {e}")
            return UploadTileResponse()
        except Exception:
            logger.exception("Error uploading tile")
            await context.abort(grpc.StatusCode.INTERNAL, "Error uploading tile.")
            return UploadTileResponse()
        finally:
            self._load.end(time.perf_counter() - start_time)
        woken = self._tile_requests.arrived(tile_key)
        if woken:
            logger.info("UploadTile row=%s col=%s woke %d stream(s) waiting for it", coord.row, coord.col, woken)
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
        return UploadTileResponse(already_cached=already_cached)

    async def SegmentTilesStream(self, request_iterator, context: ServicerContext):
        """Segment from uploaded cells; the server owns the growth and asks for tiles as it needs them.

        The client sends one ``start`` (the old unary request). The walk grows over the cells the
        server holds. When the mask reaches a cell whose tiles are missing, the server yields
        ``needed`` and waits for an ``answer``; it then resumes the same walk, so cells that were
        already predicted are not predicted again and the mask never loses pixels between rounds.
        When no tile is needed any more it yields ``result`` and returns. If the client ends the
        call or drops while a tile is awaited, the walk is abandoned and no result is sent. A
        client that stays connected but sends no answer for ``SEGMENTATION_TILE_ANSWER_TIMEOUT_SECONDS``
        (default 60) gets ``DEADLINE_EXCEEDED`` and its tile pins are released.

        A client cancel stops the walk before its next SAM2 call and ends the stream without a
        result: the call is already gone, so there is no one to send a status to.
        """
        first = await _next_message(request_iterator)
        if first is None or first.WhichOneof("body") != "start":
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                "SegmentTilesStream must begin with a start message.",
            )
            return
        request: SegmentTilesRequest = first.start
        identity = await self._validate_stream_start(request, context)
        if identity is None:
            return

        if self._active_streams >= self._limits.max_concurrent_streams:
            await self._abort_over_limit(
                context,
                "concurrent tile streams",
                self._active_streams + 1,
                self._limits.max_concurrent_streams,
                "SEGMENTATION_MAX_CONCURRENT_STREAMS",
            )
            return

        session = _TileSession(identity=identity)
        reader = _StreamReader(request_iterator)
        self._active_streams += 1
        self._load.begin()
        start_time = time.perf_counter()
        try:
            foreground = [(point.x, point.y) for point in request.foreground]
            background = [(point.x, point.y) for point in request.background]
            if not await self._pin_start_tiles(session, request, foreground, context):
                return
            settings = _PredictSettings.from_request(request)
            boxes, omit_with_box, client_chose_boxes = _resolve_boxes(request, foreground)
            tag = _session_tag(identity, foreground, background)
            logger.info(
                "SegmentTiles mask_threshold=%.3f use_mask_input=%s",
                settings.mask_threshold,
                settings.use_mask_input,
            )

            stop_growth = threading.Event()
            walk = GrowthWalk(
                foreground,
                background,
                self._cell_predictor(session, settings),
                max_requested=self._max_requested_tiles,
                should_stop=stop_growth.is_set,
                boxes=boxes,
                omit_with_box=omit_with_box,
                assume_boxes=client_chose_boxes,
            )
            loop = asyncio.get_running_loop()
            cancel_watch = asyncio.create_task(self._watch_rpc_cancel(context, stop_growth))
            rounds = 0
            try:
                while True:
                    await loop.run_in_executor(self.inference_executor, walk.advance)
                    needed = walk.take_requested()
                    if not needed:
                        break
                    rounds += 1
                    wait = await self._begin_tile_wait(session, needed)
                    while True:
                        if wait.to_send:
                            yield SegmentTilesStreamResponse(
                                needed=TilesNeeded(
                                    tiles=[_coord_for(identity, tile) for tile in wait.take_to_send()]
                                )
                            )
                        finished = await self._receive_tiles(reader, session, wait)
                        if finished is None:
                            logger.info(
                                "SegmentTiles req=%s abandoned: the client ended the stream while %d tiles were awaited",
                                request.request_id,
                                len(needed),
                            )
                            return
                        if finished:
                            break
                    walk.resume(wait.unavailable)
                result = walk.result()
            except GrowthCancelled:
                # The only thing that sets stop_growth is the client dropping the RPC, so there is
                # nobody to send a status to. The stream ends with no result message.
                logger.info("SegmentTiles stopped because the client cancelled the call")
                return
            except _ProtocolError as e:
                await context.abort(grpc.StatusCode.INVALID_ARGUMENT, str(e))
                return
            except _AnswerTimeout:
                logger.warning(
                    "SegmentTiles req=%s abandoned: no TilesAnswer within %.0fs",
                    request.request_id,
                    self._tile_answer_timeout,
                )
                await context.abort(
                    grpc.StatusCode.DEADLINE_EXCEEDED,
                    f"No TilesAnswer within {self._tile_answer_timeout:.0f}s of TilesNeeded.",
                )
                return
            except UnrecoverableGpuError as e:
                await self._abort_unrecoverable_gpu(e, context)
                return
            except NoMatchingMask as e:
                # Only the starting cell can raise this out of the walk; a continuation cell that
                # finds no matching mask is skipped inside it.
                await self._abort_no_matching_mask(e, context)
                return
            except Exception:
                logger.exception("Tile segmentation failed")
                await context.abort(grpc.StatusCode.INTERNAL, "Error processing segmentation request.")
                return
            finally:
                stop_growth.set()
                cancel_watch.cancel()
                try:
                    await cancel_watch
                except asyncio.CancelledError:
                    pass

            response = await self._stream_result(
                request, result, session, foreground, background, tag, walk.predictions, rounds, start_time
            )
            yield SegmentTilesStreamResponse(result=response)
        finally:
            reader.close()
            self._tile_requests.release_all(session.token)
            for image_id in session.pinned_ids:
                await self.image_cache.release_image(image_id)
            self._active_streams -= 1
            self._load.end(time.perf_counter() - start_time)

    async def _validate_stream_start(
        self, request: SegmentTilesRequest, context: ServicerContext
    ) -> Optional[TileCoord]:
        """Check a ``start`` message. Returns the tiles' shared identity, or None after an abort."""
        if len(request.tiles) == 0:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, "SegmentTiles requires at least one tile.")
            return None
        if len(request.foreground) == 0:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                "SegmentTiles requires at least one foreground point.",
            )
            return None
        if len(request.tiles) > self._limits.max_tiles:
            await self._abort_over_limit(
                context, "tiles", len(request.tiles), self._limits.max_tiles, "SEGMENTATION_MAX_TILES"
            )
            return None
        prompt_points = len(request.foreground) + len(request.background)
        if prompt_points > self._limits.max_points:
            await self._abort_over_limit(
                context, "prompt points", prompt_points, self._limits.max_points, "SEGMENTATION_MAX_POINTS"
            )
            return None
        if len(request.foreground_boxes) > self._limits.max_boxes:
            await self._abort_over_limit(
                context, "boxes", len(request.foreground_boxes), self._limits.max_boxes, "SEGMENTATION_MAX_BOXES"
            )
            return None
        identity = request.tiles[0]
        for tile in request.tiles:
            if not _same_tile_identity(tile, identity):
                await context.abort(
                    grpc.StatusCode.INVALID_ARGUMENT,
                    "SegmentTiles tiles must share volume, section, channel, transform, and downsample.",
                )
                return None
        return identity

    async def _pin_start_tiles(
        self,
        session: "_TileSession",
        request: SegmentTilesRequest,
        foreground: List[Tuple[int, int]],
        context: ServicerContext,
    ) -> bool:
        """Pin every uploaded tile the request names, for the whole call.

        A tile that holds a foreground point must be cached and have a predictor, or the call
        aborts (``NOT_FOUND`` / ``UNAVAILABLE``) and False is returned. Other missing tiles are
        skipped; the walk asks for them if it reaches them.
        """
        seen: set[TileCacheKey] = set()
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
                    return False
                logger.info(
                    "SegmentTiles skip uncached tile row=%s col=%s ds=%s (no foreground)",
                    tile.row,
                    tile.col,
                    tile.downsample,
                )
                continue
            image_id, image_bytes, width, height, predictor, predictor_lock = pinned
            session.pinned_ids.append(image_id)
            _remember_tile_image(session.images, int(tile.row), int(tile.col), image_bytes)
            if predictor is None:
                if holds_foreground:
                    await context.abort(
                        grpc.StatusCode.UNAVAILABLE,
                        f"Predictor for tile row={tile.row} col={tile.col} is not ready.",
                    )
                    return False
                continue
            session.predictors[(tile.row, tile.col)] = (predictor, predictor_lock, height, width)
            session.uploaded.append(TileIndex(row=int(tile.row), col=int(tile.col)))
        return True

    def _cell_predictor(self, session: "_TileSession", settings: "_PredictSettings") -> CellPredict:
        """The ``predict(row, col, points, labels, box=None)`` the growth walk calls for each cell."""
        identity = session.identity

        def predict_cell(
            row: int,
            col: int,
            points: List[Tuple[int, int]],
            labels: List[int],
            box: Optional[Tuple[int, int, int, int]] = None,
        ):
            """One 1024 window. An aligned cell reuses its pinned tile embedding."""
            cell = Cell(row=row, col=col)
            if is_aligned(cell):
                found = session.predictors.get((tiles_for_cell(cell)[0].row, tiles_for_cell(cell)[0].col))
                if found is not None:
                    predictor, predictor_lock, height, width = found
                    with predictor_lock:
                        with tile_work(identity.volume, int(identity.section), col, row):
                            return self.model.predict_tile(
                                predictor,
                                points,
                                labels,
                                settings.multimask_output,
                                (height, width),
                                box,
                                settings.mask_threshold,
                                settings.use_mask_input,
                            )

            window = crop_window(cell, session.images)
            if window is None:
                missing = [
                    tile for tile in tiles_for_cell(cell) if (tile.row, tile.col) not in session.images
                ]
                raise PredictUnavailable(f"cell row={row} col={col}", tiles=missing)
            with tile_work(identity.volume, int(identity.section), col, row):
                return self.model.predict_ephemeral(
                    window,
                    points,
                    labels,
                    settings.multimask_output,
                    (int(window.shape[0]), int(window.shape[1])),
                    box,
                    settings.mask_threshold,
                    settings.use_mask_input,
                )

        return predict_cell

    async def _stream_result(
        self,
        request: SegmentTilesRequest,
        result: Any,
        session: "_TileSession",
        foreground: List[Tuple[int, int]],
        background: List[Tuple[int, int]],
        tag: str,
        predictions: int,
        rounds: int,
        start_time: float,
    ) -> SegmentationResponse:
        """Encode a finished walk as the response, write the debug dump, and log the summary line."""
        identity = session.identity
        height, width = (result.mask.shape[0], result.mask.shape[1]) if result.mask.ndim == 2 else (0, 0)
        labeled_image, segments = combined_mask_to_segments(
            result.mask,
            result.score,
            empty_shape=(height, width),
        )
        response = await self._build_response_off_loop(
            labeled_image,
            segments,
            width,
            height,
            omit_labeled_image=request.omit_labeled_image,
        )
        response.origin_x = result.origin_x
        response.origin_y = result.origin_y
        response.request_id = request.request_id
        if dump_enabled():
            dump_growth(
                tag,
                fused_mask=result.mask,
                origin=(result.origin_x, result.origin_y),
                cells=result.cells,
                uploaded=session.uploaded,
                requested=result.requested,
                foreground=foreground,
                background=background,
                score=result.score,
                margin_logit_min=margin_logit_min_from_env(),
                owner_veto_logit=owner_veto_logit_from_env(),
                request_id=request.request_id,
                seams=result.seams,
                edges=result.edges,
            )
        logger.info(
            "SegmentTiles ok req=%s key=vol=%s|sec=%s|ch=%s|xf=%s|ds=%s "
            "tiles=%s fg=%s segments=%s requested=%s rounds=%s predicted=%s in %.3fs session=%s "
            "mask=%sx%s origin=(%s,%s) requested_tiles=%s %s",
            request.request_id,
            identity.volume,
            identity.section,
            identity.channel,
            identity.transform,
            identity.downsample,
            len(session.uploaded),
            len(foreground),
            len(segments),
            len(result.requested),
            rounds,
            predictions,
            time.perf_counter() - start_time,
            tag,
            width,
            height,
            result.origin_x,
            result.origin_y,
            [(tile.row, tile.col) for tile in result.requested],
            describe_prompts(foreground, background),
        )
        return response

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

    async def _begin_tile_wait(self, session: "_TileSession", needed: List[TileIndex]) -> "_TileWait":
        """Decide, tile by tile, whether to ask this stream's client or wait for another stream's upload.

        A tile some other stream has already asked its client for is subscribed to, not asked
        again. Every other tile is asked of this client, and is recorded as in flight so a stream
        that needs it later can subscribe in turn.
        """
        wait = _TileWait()
        loop = asyncio.get_running_loop()
        now = loop.time()
        for tile in needed:
            key = _tile_cache_key(_coord_for(session.identity, tile))
            if self._tile_share_timeout <= 0:
                self._tile_requests.join(key, session.token)
                subscription = None
            else:
                subscription = self._tile_requests.claim(key, session.token)
            if subscription is None:
                wait.ask(tile)
                continue
            wait.shared[(tile.row, tile.col)] = _SharedTile(
                tile=tile, key=key, future=subscription, deadline=now + self._tile_share_timeout
            )
            logger.info(
                "SegmentTiles tile row=%s col=%s is already being fetched for another stream; waiting up to %.1fs",
                tile.row,
                tile.col,
                self._tile_share_timeout,
            )
        wait.answer_deadline = now + self._tile_answer_timeout
        return wait

    async def _receive_tiles(
        self,
        reader: "_StreamReader",
        session: "_TileSession",
        wait: "_TileWait",
    ) -> Optional[bool]:
        """Wait until every tile in ``wait`` is pinned or known unusable, or there is something to send.

        Two things can supply a tile: this stream's client (an ``answer`` after it uploads) and
        another stream's upload reaching the cache. A borrowed tile whose wait runs out, or whose
        other stream gives up, is asked of this client instead; that puts it in ``wait.to_send``
        and this returns False so the caller sends it, then calls again.

        Returns True when nothing is outstanding (``wait.unavailable`` lists the tiles that cannot
        be used), False when ``wait.to_send`` has tiles to send, and None when the client ends its
        half of the stream first. The walk is paused while this runs, so ``session.predictors`` and
        ``session.images`` are not read by the inference thread.

        Raises:
            _ProtocolError: A message other than an ``answer`` arrived.
            _AnswerTimeout: The client answered nothing new for the idle limit.
        """
        loop = asyncio.get_running_loop()
        while True:
            if wait.to_send:
                return False
            if not wait.asked and not wait.shared:
                return True
            deadlines = [shared.deadline for shared in wait.shared.values()]
            if wait.asked:
                deadlines.append(wait.answer_deadline)
            read = reader.pending()
            watched = {read, *(shared.future for shared in wait.shared.values())}
            done, _pending = await asyncio.wait(
                watched,
                timeout=max(0.0, min(deadlines) - loop.time()),
                return_when=asyncio.FIRST_COMPLETED,
            )
            await self._settle_shared_tiles(session, wait, loop.time())
            progressed = False
            if read in done:
                message = reader.take()
                if message is None:
                    return None
                progressed = await self._apply_answer(message, session, wait)
            now = loop.time()
            if progressed:
                wait.answer_deadline = now + self._tile_answer_timeout
            elif wait.asked and not wait.to_send and now >= wait.answer_deadline:
                raise _AnswerTimeout()

    async def _settle_shared_tiles(self, session: "_TileSession", wait: "_TileWait", now: float) -> None:
        """Pin borrowed tiles that have arrived; ask this client for the ones that did not."""
        for coord, shared in list(wait.shared.items()):
            expired = not shared.future.done() and now >= shared.deadline
            if not shared.future.done() and not expired:
                continue
            del wait.shared[coord]
            if expired:
                self._tile_requests.abandon(shared.key, shared.future)
                reason = f"no upload within {self._tile_share_timeout:.1f}s"
            elif shared.future.cancelled() or not shared.future.result():
                reason = "the stream fetching it gave up"
            else:
                if await self._pin_arrived_tile(
                    session.identity, shared.tile, session.predictors, session.images, session.pinned_ids
                ):
                    logger.info(
                        "SegmentTiles tile row=%s col=%s arrived from another stream's upload",
                        shared.tile.row,
                        shared.tile.col,
                    )
                    continue
                reason = "it arrived but is no longer cached"
            logger.info(
                "SegmentTiles tile row=%s col=%s: %s; asking this stream's client",
                shared.tile.row,
                shared.tile.col,
                reason,
            )
            self._tile_requests.join(shared.key, session.token)
            wait.ask(shared.tile)
            wait.answer_deadline = now + self._tile_answer_timeout

    async def _apply_answer(self, message: SegmentTilesStreamRequest, session: "_TileSession", wait: "_TileWait") -> bool:
        """Apply one ``answer`` to the tiles this client owes. True when it settled at least one.

        An answer for a tile that is not owed (already settled, or never asked) is ignored.

        Raises:
            _ProtocolError: The message is not an ``answer``, or lists more tiles than the limit.
        """
        if message.WhichOneof("body") != "answer":
            raise _ProtocolError("SegmentTilesStream expected an answer while tiles were requested.")
        listed = len(message.answer.ready) + len(message.answer.unavailable)
        if listed > self._limits.max_answer_tiles:
            raise _ProtocolError(
                f"A TilesAnswer lists {listed} tiles; the server accepts at most "
                f"{self._limits.max_answer_tiles} (SEGMENTATION_MAX_ANSWER_TILES)."
            )
        progressed = False
        for coord in message.answer.unavailable:
            index = TileIndex(row=int(coord.row), col=int(coord.col))
            if (index.row, index.col) not in wait.asked:
                continue
            wait.asked.discard((index.row, index.col))
            wait.unavailable.append(index)
            self._tile_requests.release(_tile_cache_key(_coord_for(session.identity, index)), session.token)
            progressed = True
        for coord in message.answer.ready:
            index = TileIndex(row=int(coord.row), col=int(coord.col))
            if (index.row, index.col) not in wait.asked:
                continue
            wait.asked.discard((index.row, index.col))
            progressed = True
            key = _tile_cache_key(_coord_for(session.identity, index))
            if await self._pin_arrived_tile(
                session.identity, index, session.predictors, session.images, session.pinned_ids
            ):
                self._tile_requests.arrived(key)
                continue
            logger.info("SegmentTiles tile row=%s col=%s was reported ready but is not cached", index.row, index.col)
            wait.unavailable.append(index)
            self._tile_requests.release(key, session.token)
        return progressed

    async def _pin_arrived_tile(
        self,
        identity: TileCoord,
        index: TileIndex,
        predictors: dict,
        images: TileImages,
        pinned_ids: List[int],
    ) -> bool:
        """Pin one uploaded tile for the rest of the request. False when it cannot be used."""
        pinned = await self.image_cache.get_image_by_tile(_tile_cache_key(_coord_for(identity, index)))
        if pinned is None:
            return False
        image_id, image_bytes, width, height, predictor, predictor_lock = pinned
        pinned_ids.append(image_id)
        _remember_tile_image(images, index.row, index.col, image_bytes)
        if (index.row, index.col) not in images:
            return False
        if predictor is not None:
            predictors[(index.row, index.col)] = (predictor, predictor_lock, height, width)
        return True

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
                request.omit_labeled_image,
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
                request.omit_labeled_image,
            )
        finally:
            self._load.end(time.perf_counter() - start)

    async def SegmentImageSets(self, request_iterator, context: ServicerContext):
        """Predict each streamed point set as it arrives and yield one response per set.

        Auto-segmentation clients Delaunay-decimate one object at a time. Reading the
        stream incrementally lets the GPU run set k while the client prepares set k+1.
        """
        image_id = 0
        sets_seen = 0
        async for request in request_iterator:
            sets_seen += 1
            if sets_seen > self._limits.max_sets:
                await self._abort_over_limit(
                    context, "prompt sets on one stream", sets_seen, self._limits.max_sets, "SEGMENTATION_MAX_SETS"
                )
                return
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
                    request.omit_labeled_image,
                )
            finally:
                self._load.end(time.perf_counter() - start)
            yield response


def _client_boxes(boxes: Any) -> List[Tuple[int, int, int, int]]:
    """Box prompts the client sent as ``(x_min, y_min, x_max, y_max)``, corners ordered.

    A box with no width or height is dropped. Sorted so the same boxes in another order
    share a growth-memory session.
    """
    result: List[Tuple[int, int, int, int]] = []
    for box in boxes:
        x0, x1 = sorted((int(box.x_min), int(box.x_max)))
        y0, y1 = sorted((int(box.y_min), int(box.y_max)))
        if x1 > x0 and y1 > y0:
            result.append((x0, y0, x1, y1))
    return sorted(result)


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


DEFAULT_TLS_PORT = 443
DEFAULT_TLS_WAIT_SECONDS = 120.0
SHUTDOWN_DRAIN_SECONDS = 30.0


class TlsConfigurationError(RuntimeError):
    """TLS cannot be set up, and the server has no other listener to fall back on."""


class PortBindError(RuntimeError):
    """The TLS gRPC port could not be bound, usually because another process holds it."""


def resolve_tls_pem_paths(
    cert_path: Optional[str] = None,
    key_path: Optional[str] = None,
) -> Optional[Tuple[str, str]]:
    """Return the PEM paths when both are set and both files exist, else None.

    Falls back to SSL_CERT_PATH and SSL_KEY_PATH. Use :func:`require_tls_pem_paths` when a
    missing file must stop the process.
    """
    if cert_path is None:
        cert_path = os.environ.get("SSL_CERT_PATH", "")
    if key_path is None:
        key_path = os.environ.get("SSL_KEY_PATH", "")
    cert_path = (cert_path or "").strip()
    key_path = (key_path or "").strip()
    if not cert_path or not key_path:
        return None
    if not os.path.isfile(cert_path) or not os.path.isfile(key_path):
        return None
    return cert_path, key_path


def tls_wait_seconds() -> float:
    """Seconds to wait for certificate files at startup (SEGMENTATION_TLS_WAIT_SECONDS)."""
    raw = os.environ.get("SEGMENTATION_TLS_WAIT_SECONDS", "").strip()
    if not raw:
        return DEFAULT_TLS_WAIT_SECONDS
    try:
        return max(0.0, float(raw))
    except ValueError:
        return DEFAULT_TLS_WAIT_SECONDS


def require_tls_pem_paths(
    cert_path: Optional[str] = None,
    key_path: Optional[str] = None,
    wait_seconds: Optional[float] = None,
    poll_seconds: float = 2.0,
    sleep: Callable[[float], None] = time.sleep,
    monotonic: Callable[[], float] = time.monotonic,
) -> Tuple[str, str]:
    """Return the PEM paths, waiting up to ``wait_seconds`` for certbot to write the files.

    There is no cleartext listener to fall back on, so a server without certificates is of no
    use. Unset paths fail at once. Paths that name files which do not exist yet are retried
    until the wait runs out, which covers the first start before the certificate renewer has
    enrolled. The failure carries a message an operator can act on; the container's restart
    policy then retries the whole start.

    Raises:
        TlsConfigurationError: Paths unset, or the files are still absent after the wait.
    """
    if cert_path is None:
        cert_path = os.environ.get("SSL_CERT_PATH", "")
    if key_path is None:
        key_path = os.environ.get("SSL_KEY_PATH", "")
    cert_path = (cert_path or "").strip()
    key_path = (key_path or "").strip()
    if not cert_path or not key_path:
        raise TlsConfigurationError(
            "SSL_CERT_PATH and SSL_KEY_PATH must name the PEM certificate chain and private key. "
            "The server only listens with TLS."
        )
    limit = tls_wait_seconds() if wait_seconds is None else max(0.0, wait_seconds)
    deadline = monotonic() + limit
    warned = False
    while True:
        if os.path.isfile(cert_path) and os.path.isfile(key_path):
            return cert_path, key_path
        if monotonic() >= deadline:
            raise TlsConfigurationError(
                f"TLS certificate files not found after {limit:.0f}s "
                f"(cert={cert_path}, key={key_path}). Enroll the certificate "
                "(docker compose --profile letsencrypt) or generate a development one with "
                "'python -m segmentation_server.dev_cert'."
            )
        if not warned:
            logger.warning(
                "Waiting up to %.0fs for TLS certificate files (cert=%s, key=%s)",
                limit,
                cert_path,
                key_path,
            )
            warned = True
        sleep(poll_seconds)


def load_server_credentials(
    cert_path: Optional[str] = None,
    key_path: Optional[str] = None,
):
    """Load PEM server credentials from SSL_CERT_PATH and SSL_KEY_PATH.

    The certificate file is sent as the whole chain, so point SSL_CERT_PATH at the full chain
    (Let's Encrypt ``fullchain.pem``) for clients that do not already hold the intermediates.

    Raises:
        TlsConfigurationError: A path is unset or a file is missing. There is no cleartext
            fallback.
    """
    pem_paths = resolve_tls_pem_paths(cert_path, key_path)
    if pem_paths is None:
        raise TlsConfigurationError(
            "TLS certificate or key file is unset or missing "
            "(SSL_CERT_PATH / SSL_KEY_PATH). The server only listens with TLS."
        )
    cert_file_path, key_file_path = pem_paths
    with open(key_file_path, "rb") as key_file:
        private_key = key_file.read()
    with open(cert_file_path, "rb") as cert_file:
        certificate_chain = cert_file.read()
    return grpc.ssl_server_credentials(((private_key, certificate_chain),))


def drain_executor(executor: futures.ThreadPoolExecutor, timeout_seconds: float) -> bool:
    """Let queued and running work finish, up to ``timeout_seconds``; then cancel what is left.

    SAM2 work that is cancelled mid-call can leave the CUDA context in a state the process
    cannot recover from, so shutdown waits for it first. Returns True when everything finished
    in time.
    """
    done = threading.Event()

    def _wait() -> None:
        executor.shutdown(wait=True, cancel_futures=False)
        done.set()

    # On a timeout the second shutdown below runs while the thread above is still joining. That
    # is safe: ThreadPoolExecutor.shutdown takes an internal lock only to flip its flag and
    # empty the queue, and joins the workers outside it.

    threading.Thread(target=_wait, name="executor-drain", daemon=True).start()
    if done.wait(timeout_seconds):
        return True
    logger.warning(
        "Inference work did not finish within %.0fs of shutdown; cancelling what is queued",
        timeout_seconds,
    )
    executor.shutdown(wait=False, cancel_futures=True)
    return False


@dataclass
class ServerParts:
    """A built, bound, not yet started gRPC server and everything it must shut down with."""

    server: Any
    servicer: SegmentationServicer
    inference_executor: futures.ThreadPoolExecutor
    grpc_executor: futures.ThreadPoolExecutor
    address: str


def create_server(
    *,
    port: int,
    credentials: Any,
    max_workers: int = 10,
    inference_workers: int = 1,
    cache_ttl_seconds: int = DEFAULT_TTL_SECONDS,
    cache_max_memory_bytes: int = DEFAULT_MAX_MEMORY_BYTES,
    cache_max_images: int = DEFAULT_MAX_ENTRIES,
    compile_image_encoder: bool = True,
) -> ServerParts:
    """Build the executors, the servicer and a grpc.aio server bound to the TLS port.

    Does not start serving. Loading the model happens here (inside ``SegmentationServicer``), so
    callers resolve certificates first and fail fast before that cost.

    Raises:
        PortBindError: The TLS port could not be bound. The executors are shut down first, as
            they are for any other failure while building.
    """
    server_start_time = time.monotonic()
    inference_executor = futures.ThreadPoolExecutor(
        max_workers=inference_workers, thread_name_prefix="sam2-infer"
    )
    grpc_executor = futures.ThreadPoolExecutor(
        max_workers=max_workers, thread_name_prefix="grpc-cb"
    )
    servicer: Optional[SegmentationServicer] = None
    try:
        server = grpc.aio.server(
            grpc_executor,
            # Bounds in-flight RPCs, and with them queued predictor builds and held cache pins.
            # Past it a call fails with RESOURCE_EXHAUSTED before it reaches a handler.
            maximum_concurrent_rpcs=max_concurrent_rpcs_from_env(),
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

        address = f'[::]:{port}'
        if server.add_secure_port(address, credentials) == 0:
            raise PortBindError(f"Could not bind the TLS gRPC port {address}")
    except BaseException:
        # Loading the model or binding the port failed. The caller never gets the pools, so
        # nothing else would stop their threads.
        if servicer is not None:
            servicer.close()
        inference_executor.shutdown(wait=False, cancel_futures=True)
        grpc_executor.shutdown(wait=False, cancel_futures=True)
        raise
    return ServerParts(server, servicer, inference_executor, grpc_executor, address)


def install_stop_handlers(loop: asyncio.AbstractEventLoop, request_stop: Callable[[], None]) -> None:
    """Call ``request_stop`` on SIGTERM and SIGINT, on loops that support signal handlers and on those that do not."""
    try:
        loop.add_signal_handler(signal.SIGTERM, request_stop)
        loop.add_signal_handler(signal.SIGINT, request_stop)
    except NotImplementedError:
        for sig in (signal.SIGTERM, signal.SIGINT):
            try:
                signal.signal(sig, lambda s, f: loop.call_soon_threadsafe(request_stop))
            except (ValueError, OSError):
                pass


async def serve(
    port: int = DEFAULT_TLS_PORT,
    max_workers: int = 10,
    inference_workers: int = 1,
    cache_ttl_seconds: int = DEFAULT_TTL_SECONDS,
    cache_max_memory_bytes: int = DEFAULT_MAX_MEMORY_BYTES,
    cache_max_images: int = DEFAULT_MAX_ENTRIES,
    compile_image_encoder: bool = True,
    demo_site: bool = False,
    demo_port: int = 8443,
    demo_bind: Optional[str] = None,
) -> None:
    """Listen for gRPC over TLS on ``[::]:port``. There is no cleartext listener.

    Certificates are located before the model is loaded, so a misconfigured start fails in
    seconds, not after the weights are in GPU memory. inference_workers defaults to 1 because
    concurrent SAM2 predict() calls on one GPU contend for VRAM rather than increase
    throughput. demo_site binds a separate HTTPS port with the same certificate.

    Raises:
        TlsConfigurationError: No usable certificate and key.
        RuntimeError: The TLS port could not be bound.
    """
    install_tile_work_logging()
    install_sam2_log_filter()
    forget_listening_port()
    pem_paths = await asyncio.to_thread(require_tls_pem_paths)
    credentials = load_server_credentials(*pem_paths)

    loop = asyncio.get_running_loop()
    # Import here so demo_site can import resolve_tls_pem_paths without a cycle.
    from segmentation_server.demo_site import start_demo_site

    grace_seconds = 5
    server: Any = None
    stop_requested = False

    def _request_stop() -> None:
        nonlocal stop_requested
        stop_requested = True
        if server is not None:
            loop.create_task(server.stop(grace_seconds))

    # Installed before the model loads. Loading blocks the loop, so a signal that arrives then is
    # handled as soon as it returns, and the server is never started.
    install_stop_handlers(loop, _request_stop)

    parts = create_server(
        port=port,
        credentials=credentials,
        max_workers=max_workers,
        inference_workers=inference_workers,
        cache_ttl_seconds=cache_ttl_seconds,
        cache_max_memory_bytes=cache_max_memory_bytes,
        cache_max_images=cache_max_images,
        compile_image_encoder=compile_image_encoder,
    )
    demo_listener = None
    try:
        await asyncio.sleep(0)
        if stop_requested:
            logger.info("Stop requested while the model was loading; not starting the server")
            return
        server = parts.server
        await server.start()
        record_listening_port(port)
        logger.info(
            "Server started, TLS gRPC on %s (inference_workers=%s)", parts.address, inference_workers
        )
        demo_listener = start_demo_site(
            enabled=demo_site,
            port=demo_port,
            servicer=parts.servicer,
            loop=loop,
            pem_paths=pem_paths,
            bind=demo_bind,
        )
        await server.wait_for_termination()
    except asyncio.CancelledError:
        await parts.server.stop(grace_seconds)
    finally:
        if demo_listener is not None:
            demo_listener.close()
        await asyncio.to_thread(drain_executor, parts.inference_executor, SHUTDOWN_DRAIN_SECONDS)
        parts.grpc_executor.shutdown(wait=False, cancel_futures=True)
        parts.servicer.close()
        forget_listening_port()


if __name__ == '__main__':
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    asyncio.run(serve())
