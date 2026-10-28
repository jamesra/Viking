"""gRPC server that exposes SAM2 segmentation over SegmentationService."""

from __future__ import annotations

import asyncio
import functools
import hashlib
import importlib.metadata
import logging
import math
import os
import signal
import threading
import time
from collections import deque
from concurrent import futures
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any, Callable, Deque, Dict, List, Optional, Sequence, Set, Tuple

import grpc
import numpy as np
from grpc.aio import ServicerContext
from numpy.typing import NDArray

from segmentation_grpc import (
    GridCell,
    HeldTilesRequest,
    HeldTilesResponse,
    SegmentationResponse,
    SegmentationServiceServicer,
    SegmentResult,
    SegmentTilesRequest,
    SegmentTilesStreamRequest,
    SegmentTilesStreamResponse,
    ServerStatusRequest,
    ServerStatusResponse,
    TilesNeeded,
    UploadTileRequest,
    UploadTileResponse,
    add_SegmentationServiceServicer_to_server,
)
from segmentation_server.cuda_errors import UnrecoverableGpuError
from segmentation_server.image_cache import (
    CacheFullError,
    DEFAULT_GPU_MAX_BYTES,
    DEFAULT_MAX_ENTRIES,
    DEFAULT_MAX_MEMORY_BYTES,
    DEFAULT_TILE_IDLE_SECONDS,
    DEFAULT_TTL_SECONDS,
    ImageCache,
    PredictorCreationError,
    TileCacheKey,
)
from segmentation_server.debug_dump import dump_enabled, dump_growth
from segmentation_server.healthcheck import forget_listening_port, record_listening_port
from segmentation_server.responses import build_island_result
from segmentation_server.prompt_log import describe_prompts
from segmentation_server.model_manifest import UNKNOWN as _UNKNOWN_MODEL
from segmentation_server.model_manifest import ModelManifest, default_manifest
from segmentation_server.prompt import (
    DEFAULT_MAX_ISLANDS,
    BackgroundPrompt,
    Island,
    PromptTooLarge,
    assign_clicks,
    build_islands,
    foreground_parts,
)
from segmentation_server.prompt_encodings import circle_diameter_scale, encoder_for
from segmentation_server import trainer_prompt_encoding as enc
from segmentation_server.mask_utils import (
    DEFAULT_MASK_THRESHOLD,
    NoMatchingMask,
    prepare_image_for_sam2,
)
from segmentation_server.mask_tiles import MaskTileCollector, MaskTileError
from segmentation_server.shapes import Geometry, ShapeError, parse_shape
from segmentation_server.tile_ref import TileRef, space_name, valid_space
from segmentation_server.tile_requests import TileRequestRegistry
from segmentation_server.tile_store import TileStore, open_tile_store
from segmentation_server.tile_source import TileSource, open_tile_source
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
    tiles_for_cell,
)
from segmentation_server.tile_growth import (
    CellPredict,
    GrowthCancelled,
    GrowthResult,
    GrowthWalk,
    PredictUnavailable,
    max_requested_tiles_from_env,
)

if TYPE_CHECKING:
    from segmentation_server.segmentation_service import SegmentationModel

logger = logging.getLogger(__name__)

Click = Tuple[int, int]


class _ProtocolError(Exception):
    """The client sent a SegmentTilesStream message that the conversation does not allow."""


class _OverLimit(Exception):
    """A client message goes over one of the server's limits (RESOURCE_EXHAUSTED)."""

    def __init__(self, what: str, count: int, limit: int, variable: str) -> None:
        super().__init__(f"{what}={count} over {limit}")
        self.what, self.count, self.limit, self.variable = what, count, limit, variable


class _ClientEnded(Exception):
    """The client ended its half of the stream while the server still waited for tiles or an answer."""


@dataclass(frozen=True)
class RequestLimits:
    """Largest requests the server will work on. Each is an environment variable.

    A client can otherwise send an unbounded list of tiles, prompt points or shapes, and the server
    would allocate and loop for as long as it was asked. Going over a limit is ``RESOURCE_EXHAUSTED``,
    named in the message with the limit that was hit.
    """

    max_tiles: int = 64
    max_points: int = 512
    max_shapes: int = 512
    max_answer_tiles: int = 4096
    max_mask_tiles: int = 256
    max_concurrent_streams: int = 32
    max_islands: int = DEFAULT_MAX_ISLANDS

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
            max_shapes=read("SEGMENTATION_MAX_SHAPES", defaults.max_shapes),
            max_answer_tiles=read("SEGMENTATION_MAX_ANSWER_TILES", defaults.max_answer_tiles),
            max_mask_tiles=read("SEGMENTATION_MAX_MASK_TILES", defaults.max_mask_tiles),
            max_concurrent_streams=read(
                "SEGMENTATION_MAX_CONCURRENT_STREAMS", defaults.max_concurrent_streams
            ),
            max_islands=read("SEGMENTATION_MAX_ISLANDS", defaults.max_islands),
        )


class _AnswerTimeout(Exception):
    """The client did not answer a TilesNeeded, or upload a tile it was asked for, within the idle limit."""


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

# Most cells one GetHeldTiles call may name: a prefetch batch, not a section.
MAX_HELD_TILES_CELLS = 256


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

    While a stream waits for tiles it also waits on uploads reaching the cache. A read cancelled by
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
class _Needed:
    """One TilesNeeded sent to the client and not answered yet: its tiles and its background cells."""

    tiles: List[TileIndex]
    background: List[Tuple[int, int]]


@dataclass
class _TileWait:
    """Where tile fetching stands for one stream: what its client was asked, what is borrowed, what is owed.

    ``asked`` holds the tiles this stream's client must upload, each with the future its upload resolves.
    ``to_send`` and ``background_to_send`` hold what the client has not been told about yet. ``shared`` holds tiles
    another stream is fetching; each moves to ``asked`` if its wait runs out or the other stream gives up. ``answers``
    holds every TilesNeeded sent and not yet answered, in order: the client answers each once.
    """

    asked: Dict[Tuple[int, int], "asyncio.Future[bool]"] = field(default_factory=dict)
    to_send: List[TileIndex] = field(default_factory=list)
    background_to_send: List[Tuple[int, int]] = field(default_factory=list)
    shared: Dict[Tuple[int, int], _SharedTile] = field(default_factory=dict)
    unavailable: List[TileIndex] = field(default_factory=list)
    answers: Deque[_Needed] = field(default_factory=deque)
    answer_deadline: float = 0.0

    def ask(self, tile: TileIndex, arrival: "asyncio.Future[bool]") -> None:
        self.asked[(tile.row, tile.col)] = arrival
        self.to_send.append(tile)

    def take_needed(self) -> Optional[_Needed]:
        """What to send in the next TilesNeeded (recorded as owed an answer), or None when there is nothing."""
        if not self.to_send and not self.background_to_send:
            return None
        needed = _Needed(tiles=self.to_send, background=self.background_to_send)
        self.to_send, self.background_to_send = [], []
        self.answers.append(needed)
        return needed

    def take_unavailable(self) -> List[TileIndex]:
        taken, self.unavailable = self.unavailable, []
        return taken

    @property
    def waiting_on_client(self) -> bool:
        """True while the client owes an upload or an answer."""
        return bool(self.asked or self.answers)

    @property
    def idle(self) -> bool:
        """True when nothing is owed, borrowed or waiting to be sent."""
        return not (self.asked or self.shared or self.answers or self.to_send or self.background_to_send)


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

    def add(self, row: int, col: int, image_bytes: bytes, decoded: Optional[NDArray[np.uint8]] = None) -> None:
        """Remember a tile's encoded bytes, and its pixels when the caller already decoded them (a tile already held keeps its own)."""
        if image_bytes and (row, col) not in self._encoded:
            self._encoded[(row, col)] = image_bytes
            if decoded is not None:
                self._decoded[(row, col)] = decoded

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
    """What one ``SegmentTilesStream`` call holds: its grid, pins, predictors, pixels and background."""

    grid: Any
    pinned_ids: List[int] = field(default_factory=list)
    predictors: dict = field(default_factory=dict)
    images: "TileImages" = field(default_factory=lambda: TileImages())
    uploaded: List[TileIndex] = field(default_factory=list)
    # Names this stream to the TileRequestRegistry. The session itself is unhashable.
    token: object = field(default_factory=object)
    # What the object is not: background shapes and bitmap, and which cells' shapes the client has been asked for.
    background: BackgroundPrompt = field(default_factory=BackgroundPrompt)
    # Tiles the client said it cannot supply. Asked again by no walk of this call.
    unavailable: Set[Tuple[int, int]] = field(default_factory=set)
    # The Identity annotation context the client has open (0 for none); it lets the server read the context's tiles.
    annotation_context_id: int = 0

    def ref(self, tile: TileIndex) -> TileRef:
        return TileRef.of(self.grid, tile.row, tile.col)


@dataclass(frozen=True)
class _PredictSettings:
    """The per-request SAM2 options every cell prediction uses."""

    mask_threshold: float
    use_mask_input: bool
    # One mask per window: the walk follows one object, never SAM2's alternatives.
    multimask_output: bool = False

    @classmethod
    def from_request(cls, request: SegmentTilesRequest) -> "_PredictSettings":
        return cls(
            mask_threshold=(
                float(request.mask_threshold) if request.HasField("mask_threshold") else DEFAULT_MASK_THRESHOLD
            ),
            use_mask_input=bool(request.use_mask_input),
        )


@dataclass
class _Start:
    """A checked ``start`` message: its prompt in the server's terms."""

    foreground: List[Click]
    background: List[Click]
    foreground_shapes: List[Geometry]
    background_shapes: List[Geometry]


@dataclass
class _IslandOutcome:
    """One island's finished walk."""

    island: int
    result: GrowthResult
    predictions: int
    clicks: List[Click]


def _session_tag(grid: Any, foreground: List[Click], background: List[Click]) -> str:
    """Short stable tag for one prompt set, used in log lines and debug dump names."""
    return hashlib.blake2s(
        repr((
            grid.volume,
            int(grid.section),
            grid.channel,
            grid.transform,
            int(grid.downsample),
            sorted(foreground),
            sorted(background),
        )).encode(),
        digest_size=4,
    ).hexdigest()


def _click(point: Any, where: str) -> Click:
    """A ``Vector2`` click as the mosaic pixel it falls in.

    Raises:
        ShapeError: A coordinate is not a finite number.
    """
    x, y = float(point.x), float(point.y)
    if not (math.isfinite(x) and math.isfinite(y)):
        raise ShapeError(f"{where} has a coordinate that is not a finite number.")
    return int(math.floor(x)), int(math.floor(y))


def _cells(tiles: Sequence[Any]) -> List[GridCell]:
    return [GridCell(row=int(tile[0]), col=int(tile[1])) if isinstance(tile, tuple) else GridCell(row=tile.row, col=tile.col)
            for tile in tiles]


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
    """gRPC handlers for tile upload, tile-grid segmentation, held tiles and status.

    Every uploaded tile owns a predictor built at upload, shared by every client that names the same tile.
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
        tile_store: Optional[TileStore] = None,
        tile_source: Optional[TileSource] = None,
        status_cache_seconds: float = 1.0,
        clock: Callable[[], float] = time.monotonic,
        cache_gpu_max_bytes: int = DEFAULT_GPU_MAX_BYTES,
        cache_tile_idle_seconds: float = DEFAULT_TILE_IDLE_SECONDS,
    ) -> None:
        """
        Args:
            cache_max_memory_bytes: Host memory cap for the cache (default 8 GiB): encoded tiles plus the
                embeddings moved off the GPU.
            cache_gpu_max_bytes: GPU memory cap for cached embeddings (default 512 MiB); past it the least
                recently used move to host memory.
            cache_tile_idle_seconds: Shared tiles unused this long leave the cache (default one hour).
            cache_ttl_seconds: Unused-entry lifetime for the cache's non-tile entries (default 5 minutes).
                Shared tiles are not expired by the TTL.
            cache_max_images: Max non-tile cache entries (default 4096). Shared tiles do not count.
            inference_executor: Thread pool for SAM2 work; None uses the default executor.
            server_start_time: time.monotonic() at process start, for uptime.
            model: Injected SAM2 wrapper; constructed here if omitted.
            image_cache: Injected cache; constructed here if omitted.
            inference_workers: Size of the inference thread pool, reported in GetServerStatus.
            exit_process: Called after an unrecoverable GPU error (inject in tests).
            compile_image_encoder: torch.compile the SAM2 Hiera encoder (CUDA only).
            tile_store: Uploaded tiles kept after the image cache drops them; read from the environment if omitted.
            tile_source: Reads cells of the annotation contexts this server is local to, so their clients need not
                upload them; read from the environment if omitted (None there when Identity is not configured).
            status_cache_seconds: How long one GetServerStatus answer is reused (default 1 s; 0 turns it off). Clients
                poll status every second while the service picker is open, so this keeps the cost per second fixed
                however many clients are watching.
            clock: Monotonic clock for the status cache (inject in tests).
        """
        if model is None:
            from segmentation_server.segmentation_service import SegmentationModel as _SegmentationModel
            model = _SegmentationModel(compile_image_encoder=compile_image_encoder)
        self.model = model
        # What the model's manifest says it is and how it is fed. A model without one (a test fake, or weights
        # with no manifest file) is the legacy tiled model, as before manifests existed.
        manifest = getattr(model, "manifest", None)
        self._manifest: ModelManifest = manifest if isinstance(manifest, ModelManifest) else default_manifest()
        self._prompt_encoder = encoder_for(self._manifest)
        # The factor a softened circle's radius is scaled by: the encoding's own, else the manifest's client contract.
        self._circle_scale = (
            float(self._prompt_encoder.circle_scale)
            if self._prompt_encoder is not None
            else circle_diameter_scale(self._manifest)
        )
        self._warned_missing_model_id = False
        self.inference_executor = inference_executor
        self._inference_workers = max(1, inference_workers)
        self._exit_process = exit_process
        self._server_start_time = server_start_time if server_start_time is not None else 0.0
        self._load = RequestLoadTracker()
        self._status_cache_seconds = max(0.0, float(status_cache_seconds))
        self._clock = clock
        # (time built, compile state it was built under, answer). See GetServerStatus.
        self._status_cache: Optional[Tuple[float, Tuple[str, str], ServerStatusResponse]] = None
        self.image_cache = image_cache if image_cache is not None else ImageCache(
            max_memory_bytes=cache_max_memory_bytes,
            ttl_seconds=cache_ttl_seconds,
            max_entries=cache_max_images,
            create_predictor_func=self.model.create_initialized_predictor,
            release_predictor_func=self.model.release_predictor,
            gpu_under_pressure=self._gpu_under_pressure,
            predictor_generation=self._predictor_encoder_generation,
            current_generation=self._current_encoder_generation,
            # The SAM2 model moves embeddings between the GPU and host memory; a test fake without those methods
            # keeps them on its device.
            embedding_tiers=self.model if all(
                callable(getattr(self.model, name, None)) for name in ("gpu_bytes", "export", "drop_gpu", "restore")
            ) else None,
            gpu_max_bytes=cache_gpu_max_bytes,
            tile_idle_seconds=cache_tile_idle_seconds,
        )
        self._max_requested_tiles = max_requested_tiles_from_env()
        self._tile_answer_timeout = tile_answer_timeout_seconds()
        self._tile_share_timeout = tile_share_timeout_seconds()
        self._tile_requests = TileRequestRegistry()
        self._tile_store = tile_store if tile_store is not None else open_tile_store()
        self._tile_source = tile_source if tile_source is not None else open_tile_source()
        self._limits = RequestLimits.from_env()
        self._active_streams = 0
        # Image decoding, prompt rasterization and response encoding are CPU work. They get their own small pool so a
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
        """Release the servicer's own thread pools. The inference pool belongs to whoever passed it in."""
        self._cpu_executor.shutdown(wait=False, cancel_futures=True)
        close_source = getattr(self._tile_source, "close", None)
        if callable(close_source):
            close_source()

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

        Entries already built with the compiled encoder are kept. Runs on the compile thread, so the flush is handed
        to the event loop that owns the cache.
        """
        if self._loop is None or self._loop.is_closed():
            logger.warning("Compiled encoder ready but no event loop; image cache not flushed")
            self._release_replaced_encoder()
            return
        future = asyncio.run_coroutine_threadsafe(self.image_cache.flush_stale(), self._loop)
        future.add_done_callback(self._log_flush_result)

    def _log_flush_result(self, future: "futures.Future[int]") -> None:
        error = future.exception()
        if error is not None:
            logger.error("Flushing eager embeddings after the compile swap failed", exc_info=error)
        # The flushed eager embeddings were the last holders of the eager model, so its memory can go back now.
        self._release_replaced_encoder()

    def _release_replaced_encoder(self) -> None:
        release = getattr(self.model, "release_replaced_encoder_memory", None)
        if callable(release):
            release()

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

    async def _abort_no_matching_mask(self, error: NoMatchingMask, context: ServicerContext) -> None:
        """Abort with FAILED_PRECONDITION and the stable ``NO_MATCHING_MASK`` prefix.

        The prompt produced no mask that fits it. The client logs the message; there is no
        fallback result.
        """
        logger.error("NO_MATCHING_MASK: %s", error)
        await context.abort(grpc.StatusCode.FAILED_PRECONDITION, f"NO_MATCHING_MASK {error}")

    async def _abort_over_limit(self, context: ServicerContext, what: str, count: int, limit: int, variable: str) -> None:
        """Abort with RESOURCE_EXHAUSTED naming the limit that was exceeded and how to raise it."""
        logger.warning("Request refused: %s=%s is over the limit of %s", what, count, limit)
        await context.abort(
            grpc.StatusCode.RESOURCE_EXHAUSTED,
            f"Request has {count} {what}; the server accepts at most {limit} ({variable}).",
        )

    async def _decode_tile_or_abort(
        self, image_data: bytes, context: ServicerContext, rpc: str
    ) -> Optional[NDArray[np.uint8]]:
        """Decode a tile off the event loop and check it is ``TILE_SIZE`` square. None after an abort.

        A tile of another size would put the masks in a different frame from the grid, so it is INVALID_ARGUMENT, as
        are empty or unreadable bytes.
        """
        if not image_data:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, f"{rpc} requires non-empty image_data.")
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
        height, width = decoded.shape[:2]
        if width != TILE_SIZE or height != TILE_SIZE:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                f"{rpc} requires a {TILE_SIZE}x{TILE_SIZE} image, got {width}x{height}.",
            )
            return None
        return decoded

    async def _grid_ok(self, request: Any, context: ServicerContext, rpc: str) -> bool:
        """Check a request's ``grid``: a MOSAIC or VOLUME space and a positive downsample. False after an abort."""
        grid = request.grid
        if not request.HasField("grid") or not valid_space(grid.space):
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, f"{rpc} requires a grid in a MOSAIC or VOLUME space.")
            return False
        if grid.downsample <= 0:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, f"{rpc} requires a grid with a positive downsample.")
            return False
        return True

    def _model_id_required(self) -> bool:
        """True when a request must name the model: ``SEGMENTATION_REQUIRE_MODEL_ID`` (1 or 0), else the manifest."""
        flag = os.environ.get("SEGMENTATION_REQUIRE_MODEL_ID", "").strip()
        if flag in ("0", "1"):
            return flag == "1"
        return self._manifest.requires_model_id

    async def _model_id_ok(self, request: Any, context: ServicerContext) -> bool:
        """Check the model a request says it was built for against the one this server serves.

        A different ``model_id`` is refused with ``FAILED_PRECONDITION``: its prompt was built for another model, and
        the answer would be quietly worse. An unset one is refused only when the model requires it. A server whose model
        has no manifest does not know its own id, so it has nothing to compare and accepts any.
        """
        manifest = self._manifest
        has_field = getattr(request, "HasField", None)
        sent = getattr(request, "model_id", None) if callable(has_field) and has_field("model_id") else None
        if not isinstance(sent, str):
            sent = None
        if sent is None:
            if self._model_id_required():
                await context.abort(
                    grpc.StatusCode.FAILED_PRECONDITION,
                    f"This server serves model {manifest.model_id!r} and requires model_id on every request.",
                )
                return False
            if manifest.model_id != _UNKNOWN_MODEL and not self._warned_missing_model_id:
                self._warned_missing_model_id = True
                logger.warning(
                    "A request did not name its model_id; this server serves %s. Accepted, since it is not required.",
                    manifest.model_id,
                )
            return True
        if manifest.model_id != _UNKNOWN_MODEL and sent != manifest.model_id:
            await context.abort(
                grpc.StatusCode.FAILED_PRECONDITION,
                f"This server serves model {manifest.model_id!r} ({manifest.model_version}); "
                f"the request was built for {sent!r}.",
            )
            return False
        return True

    async def GetServerStatus(
        self,
        request: ServerStatusRequest,
        context: ServicerContext,
    ) -> ServerStatusResponse:
        """Version, uptime, device, and cache occupancy for health/selection UI.

        One answer is reused for status_cache_seconds, so clients polling every second cost one cache-lock
        snapshot per second in total. A change in compile state (the encoder finishing warm-up) is never hidden
        behind the cache: it is checked on every call, and a stale answer is rebuilt at once.
        """
        compile_status = getattr(self.model, "compile_status", "off")
        compile_status = compile_status if isinstance(compile_status, str) else "off"
        encoder_generation = self._current_encoder_generation() or ""
        state = (compile_status, encoder_generation)
        now = self._clock()
        cached = self._status_cache
        if (
            cached is not None
            and self._status_cache_seconds > 0
            and cached[1] == state
            and 0.0 <= now - cached[0] < self._status_cache_seconds
        ):
            return cached[2]
        response = await self._build_server_status(compile_status, encoder_generation)
        if self._status_cache_seconds > 0:
            self._status_cache = (now, state, response)
        return response

    async def _build_server_status(self, compile_status: str, encoder_generation: str) -> ServerStatusResponse:
        """A fresh GetServerStatus answer."""
        try:
            version = importlib.metadata.version("segmentation_server")
        except importlib.metadata.PackageNotFoundError:
            version = "0.1.0"
        uptime_seconds = time.monotonic() - self._server_start_time if self._server_start_time else 0.0
        cache_stats = await self.image_cache.get_stats()
        device = getattr(self.model, "device", "unknown")
        in_flight, recent_latency_ms = self._load.snapshot()
        return ServerStatusResponse(
            version=version,
            encoder_generation=encoder_generation,
            compile_status=compile_status,
            uptime_seconds=uptime_seconds,
            message=(
                f"device={device}; "
                f"compile={compile_status}; "
                f"cache={cache_stats['total_images']} images, "
                f"{cache_stats['total_memory_mb']:.1f} MiB host, "
                f"{cache_stats.get('gpu_embedding_bytes', 0) / 1024**2:.1f}"
                f"/{cache_stats.get('gpu_max_bytes', 0) / 1024**2:.0f} MiB GPU embeddings; "
                f"pulls from host={cache_stats.get('embedding_pulls', 0)}"
            ),
            in_flight_requests=in_flight,
            cached_images=int(cache_stats["total_images"]),
            cache_memory_bytes=int(cache_stats["total_memory_bytes"]),
            recent_latency_ms=recent_latency_ms,
            inference_workers=self._inference_workers,
            model=self._manifest.to_model_info(),
        )

    async def UploadTile(
        self,
        request: UploadTileRequest,
        context: ServicerContext,
    ) -> UploadTileResponse:
        """Cache one grid cell. A cell already cached keeps its entry and skips set_image().

        A newly cached cell also goes in the tile store, so the server can rebuild it without asking a client
        after the image cache drops it. Every upload, cached before or not, wakes the streams waiting for the cell.
        """
        if not await self._grid_ok(request, context, "UploadTile"):
            return UploadTileResponse()
        image_data: bytes = request.image_data
        decoded = await self._decode_tile_or_abort(image_data, context, "UploadTile")
        if decoded is None:
            return UploadTileResponse()

        # Public reusable identity is the grid and the cell. cache_id is an internal sequential handle for predictors.
        ref = TileRef.of(request.grid, request.cell.row, request.cell.col)
        tile_key = ref.key
        self._load.begin()
        start_time = time.perf_counter()
        try:
            cache_id, already_cached = await self.image_cache.upload_tile(
                tile_key,
                image_data,
                TILE_SIZE,
                TILE_SIZE,
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
        if not already_cached:
            self._keep_tile(tile_key, image_data)
        woken = self._tile_requests.arrived(tile_key)
        if woken:
            logger.info("UploadTile row=%s col=%s woke %d stream(s) waiting for it", ref.row, ref.col, woken)
        logger.info(
            "UploadTile ok key=vol=%s|sec=%s|ch=%s|xf=%s|space=%s|ds=%s|row=%s|col=%s "
            "cache_id=%s %s bytes already_cached=%s in %.3fs",
            ref.volume,
            ref.section,
            ref.channel,
            ref.transform,
            space_name(ref.space),
            ref.downsample,
            ref.row,
            ref.col,
            cache_id,
            len(image_data),
            already_cached,
            time.perf_counter() - start_time,
        )
        return UploadTileResponse(already_cached=already_cached)

    async def GetHeldTiles(
        self,
        request: HeldTilesRequest,
        context: ServicerContext,
    ) -> HeldTilesResponse:
        """Which cells a SegmentTilesStream would not ask the client for, so a prefetch skips them.

        A cell is held when the image cache has it, the tile store has it in memory or on disk, or the tile source
        reads it for the request's annotation context. Nothing is pinned or refreshed: the answer is a hint, and a cell
        dropped since is asked for during the request as usual.
        """
        grid = request.grid
        if not valid_space(grid.space):
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, "GetHeldTiles requires a MOSAIC or VOLUME space.")
            return HeldTilesResponse()
        if grid.downsample <= 0:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, "GetHeldTiles requires a positive downsample.")
            return HeldTilesResponse()
        if len(request.cells) > MAX_HELD_TILES_CELLS:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                f"GetHeldTiles takes at most {MAX_HELD_TILES_CELLS} cells, got {len(request.cells)}.",
            )
            return HeldTilesResponse()

        context_id = int(request.annotation_context_id)
        hot = warm = readable = 0
        held: List[bool] = []
        for cell in request.cells:
            ref = TileRef.of(grid, cell.row, cell.col)
            key = ref.key
            if await self.image_cache.has_tile(key):
                hot += 1
            elif self._tile_store.contains(key):
                warm += 1
            elif self._tile_source is not None and await self._tile_source.can_read(context_id, ref):
                readable += 1
            else:
                held.append(False)
                continue
            held.append(True)
        logger.info(
            "GetHeldTiles vol=%s sec=%s ch=%s xf=%s space=%s ds=%s context=%s cells=%d cached=%d stored=%d readable=%d missing=%d",
            grid.volume,
            grid.section,
            grid.channel,
            grid.transform,
            space_name(grid.space),
            grid.downsample,
            context_id,
            len(request.cells),
            hot,
            warm,
            readable,
            len(request.cells) - hot - warm - readable,
        )
        return HeldTilesResponse(held=held)

    async def SegmentTilesStream(self, request_iterator, context: ServicerContext):
        """Segment from the prompt in ``start``; the server owns the growth and asks for tiles as it needs them.

        The client sends ``start`` and the mask tiles it announced. The prompt is split into islands
        (:mod:`prompt`); each is grown on its own walk over the cells the server holds. When a walk reaches a cell
        whose tiles are missing, or whose background the client has not sent, the server yields ``needed`` and uses
        each tile as its upload lands; each TilesAnswer marks tiles unavailable and brings background shapes. When no
        walk needs anything more it yields ``result``, one SegmentResult per island, and returns. If the client ends
        the call or drops while a tile is awaited, the walks are abandoned and no result is sent. A client that stays
        connected but uploads and answers nothing for ``SEGMENTATION_TILE_ANSWER_TIMEOUT_SECONDS`` (default 60) gets
        ``DEADLINE_EXCEEDED`` and its tile pins are released.

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
        start = await self._validate_stream_start(request, context)
        if start is None:
            return
        mask_tiles = MaskTileCollector(request.mask_tile_count)
        if not await self._read_mask_tiles(request, request_iterator, mask_tiles, context):
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

        islands = await self._islands_or_abort(start, mask_tiles, context)
        if islands is None:
            return
        if not start.foreground and not islands:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                "SegmentTiles requires a foreground click, a foreground shape or a FOREGROUND mask tile.",
            )
            return

        background = BackgroundPrompt(
            covered=[(int(tile.row), int(tile.col)) for tile in request.tiles],
            bitmap=mask_tiles.background_shape(),
            trust=getattr(self._prompt_encoder, "circle_trust", None) or enc.DEFAULT_CIRCLE_TRUST,
        )
        background.add_shapes(start.background_shapes)
        session = _TileSession(
            grid=request.grid, background=background, annotation_context_id=int(request.annotation_context_id)
        )
        reader = _StreamReader(request_iterator)
        self._active_streams += 1
        self._load.begin()
        start_time = time.perf_counter()
        try:
            if not await self._pin_start_tiles(session, request, context):
                return
            settings = _PredictSettings.from_request(request)
            tag = _session_tag(request.grid, start.foreground, start.background)
            logger.info(
                "SegmentTiles req=%s mask_threshold=%.3f use_mask_input=%s islands=%d background_shapes=%d encoding=%s",
                request.request_id,
                settings.mask_threshold,
                settings.use_mask_input,
                len(islands),
                background.shape_count,
                self._manifest.prompt_encoding,
            )

            stop_growth = threading.Event()
            wait = _TileWait()
            outcomes: List[_IslandOutcome] = []
            rejections: List[NoMatchingMask] = []
            progress = {"rounds": 0}
            cancel_watch = asyncio.create_task(self._watch_rpc_cancel(context, stop_growth))
            try:
                plan: List[Tuple[Optional[Island], List[Click]]]
                if islands:
                    plan = list(zip(islands, assign_clicks(islands, start.foreground)))
                else:
                    plan = [(None, list(start.foreground))]
                for island, clicks in plan:
                    if not clicks and island is not None:
                        clicks = [island.interior_point()]
                    walk = GrowthWalk(
                        clicks,
                        start.background,
                        self._cell_predictor(session, settings),
                        max_requested=self._max_requested_tiles,
                        should_stop=stop_growth.is_set,
                        logit_iso=settings.mask_threshold,
                        masks=[island] if island is not None else None,
                        background_masks=background.regions,
                        background_coverage=background.unknown_tiles,
                        prompt_encoder=self._prompt_encoder,
                        holds_image=lambda tile: (tile.row, tile.col) in session.images,
                    )
                    async for update in self._grow(walk, session, wait, reader, progress):
                        yield update
                    number = island.number if island is not None else 1
                    try:
                        result = walk.result()
                    except NoMatchingMask as e:
                        logger.warning("SegmentTiles req=%s island %d: %s", request.request_id, number, e)
                        rejections.append(e)
                        continue
                    outcomes.append(_IslandOutcome(number, result, walk.predictions, clicks))
            except _ClientEnded:
                logger.info(
                    "SegmentTiles req=%s abandoned: the client ended the stream while tiles or an answer were awaited",
                    request.request_id,
                )
                return
            except GrowthCancelled:
                # The only thing that sets stop_growth is the client dropping the RPC, so there is
                # nobody to send a status to. The stream ends with no result message.
                logger.info("SegmentTiles stopped because the client cancelled the call")
                return
            except _ProtocolError as e:
                await context.abort(grpc.StatusCode.INVALID_ARGUMENT, str(e))
                return
            except _OverLimit as e:
                await self._abort_over_limit(context, e.what, e.count, e.limit, e.variable)
                return
            except _AnswerTimeout:
                logger.warning(
                    "SegmentTiles req=%s abandoned: no upload or TilesAnswer within %.0fs",
                    request.request_id,
                    self._tile_answer_timeout,
                )
                await context.abort(
                    grpc.StatusCode.DEADLINE_EXCEEDED,
                    f"No upload or TilesAnswer within {self._tile_answer_timeout:.0f}s of TilesNeeded.",
                )
                return
            except UnrecoverableGpuError as e:
                await self._abort_unrecoverable_gpu(e, context)
                return
            except NoMatchingMask as e:
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

            if not outcomes and rejections:
                # Every island's start was rejected and nothing was found: there is no honest answer.
                await self._abort_no_matching_mask(rejections[0], context)
                return
            response = await self._stream_result(
                request, outcomes, session, start, tag, progress["rounds"], start_time, settings.mask_threshold
            )
            yield SegmentTilesStreamResponse(result=response)
        finally:
            reader.close()
            self._tile_requests.release_all(session.token)
            for image_id in session.pinned_ids:
                await self.image_cache.release_image(image_id)
            self._active_streams -= 1
            self._load.end(time.perf_counter() - start_time)

    async def _validate_stream_start(self, request: SegmentTilesRequest, context: ServicerContext) -> Optional[_Start]:
        """Check a ``start`` message and read its prompt. Returns None after an abort.

        INVALID_ARGUMENT for a missing or UNSPECIFIED grid, no tiles, a click that is not a finite number, or a shape
        the server cannot use (a ``Shape.point`` among them); RESOURCE_EXHAUSTED over the limits on tiles, mask tiles,
        and points and shapes per Feature; FAILED_PRECONDITION for another model_id.
        """
        if not await self._model_id_ok(request, context):
            return None
        if not await self._grid_ok(request, context, "SegmentTiles"):
            return None
        if len(request.tiles) == 0:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, "SegmentTiles requires at least one tile.")
            return None
        if len(request.tiles) > self._limits.max_tiles:
            await self._abort_over_limit(
                context, "tiles", len(request.tiles), self._limits.max_tiles, "SEGMENTATION_MAX_TILES"
            )
            return None
        if request.mask_tile_count > self._limits.max_mask_tiles:
            await self._abort_over_limit(
                context, "mask tiles", request.mask_tile_count, self._limits.max_mask_tiles, "SEGMENTATION_MAX_MASK_TILES"
            )
            return None
        features = (("foreground", request.foreground_feature), ("background", request.background_feature))
        for side, feature in features:
            if len(feature.points) > self._limits.max_points:
                await self._abort_over_limit(
                    context, f"{side} points", len(feature.points), self._limits.max_points, "SEGMENTATION_MAX_POINTS"
                )
                return None
            if len(feature.shapes) > self._limits.max_shapes:
                await self._abort_over_limit(
                    context, f"{side} shapes", len(feature.shapes), self._limits.max_shapes, "SEGMENTATION_MAX_SHAPES"
                )
                return None
        try:
            return _Start(
                foreground=[
                    _click(point, f"foreground point {i}") for i, point in enumerate(request.foreground_feature.points)
                ],
                background=[
                    _click(point, f"background point {i}") for i, point in enumerate(request.background_feature.points)
                ],
                foreground_shapes=[
                    parse_shape(shape, f"foreground shape {i}") for i, shape in enumerate(request.foreground_feature.shapes)
                ],
                background_shapes=[
                    parse_shape(shape, f"background shape {i}") for i, shape in enumerate(request.background_feature.shapes)
                ],
            )
        except ShapeError as e:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, str(e))
            return None

    async def _read_mask_tiles(
        self,
        request: SegmentTilesRequest,
        request_iterator,
        collected: MaskTileCollector,
        context: ServicerContext,
    ) -> bool:
        """Read the ``mask_tiles`` messages that carry the announced tiles into ``collected``. False after an abort.

        Each message is awaited for at most the idle limit used for a TilesAnswer, so a client that announces tiles and
        stops sending gets ``DEADLINE_EXCEEDED``.
        """
        announced = collected.announced
        try:
            while not collected.done:
                message = await _next_message(request_iterator, self._tile_answer_timeout)
                if message is None:
                    raise MaskTileError(
                        f"The stream ended after {collected.received} of {announced} announced mask tiles."
                    )
                if message.WhichOneof("body") != "mask_tiles":
                    raise MaskTileError(
                        f"Expected {announced - collected.received} more mask tiles before anything else."
                    )
                if len(message.mask_tiles.tiles) > self._limits.max_answer_tiles:
                    raise MaskTileError(
                        f"A MaskTiles message lists {len(message.mask_tiles.tiles)} tiles; the server accepts at most "
                        f"{self._limits.max_answer_tiles} (SEGMENTATION_MAX_ANSWER_TILES)."
                    )
                collected.add(message.mask_tiles)
        except MaskTileError as e:
            await context.abort(grpc.StatusCode.INVALID_ARGUMENT, str(e))
            return False
        except _AnswerTimeout:
            logger.warning(
                "SegmentTiles req=%s abandoned: %d of %d mask tiles within %.0fs",
                request.request_id,
                collected.received,
                announced,
                self._tile_answer_timeout,
            )
            await context.abort(
                grpc.StatusCode.DEADLINE_EXCEEDED,
                f"Only {collected.received} of {announced} announced mask tiles arrived "
                f"within {self._tile_answer_timeout:.0f}s.",
            )
            return False
        return True

    async def _islands_or_abort(
        self, start: _Start, mask_tiles: MaskTileCollector, context: ServicerContext
    ) -> Optional[List[Island]]:
        """The object prompt as islands (empty for clicks alone). None after an abort.

        Rasterizing shapes and labelling the union is CPU work that grows with the prompt, so it runs on the CPU pool.
        """
        parts = foreground_parts(start.foreground_shapes, mask_tiles.foreground_shape(), self._circle_scale)
        if not parts:
            return []
        encoder = self._prompt_encoder
        limits = self._limits

        def build() -> List[Island]:
            if encoder is not None:
                return encoder.islands(parts, limits.max_mask_tiles, limits.max_islands)
            return build_islands(parts, limits.max_mask_tiles, limits.max_islands)

        try:
            return await asyncio.get_running_loop().run_in_executor(self._cpu_executor, build)
        except PromptTooLarge as e:
            await self._abort_over_limit(
                context, "tiles under the foreground prompt", e.tiles, e.limit, "SEGMENTATION_MAX_MASK_TILES"
            )
            return None

    async def _grow(self, walk: GrowthWalk, session: "_TileSession", wait: "_TileWait", reader: "_StreamReader",
                    progress: Dict[str, int]):
        """Run one island's walk to the end, yielding a ``needed`` message whenever the client must be asked.

        The walk is resumed as soon as anything it waits for settles (a tile is pinned or known unavailable, or an
        answer brings background), not once per batch. It ends when the walk asks for nothing more and nothing is
        owed: every TilesNeeded sent has been answered.

        Raises:
            _ClientEnded: The client ended its half of the stream while something was awaited.
            _ProtocolError, _OverLimit, _AnswerTimeout: see :meth:`_receive_tiles`.
        """
        loop = asyncio.get_running_loop()
        while True:
            await loop.run_in_executor(self.inference_executor, walk.advance)
            needed = walk.take_requested()
            background = walk.take_requested_background()
            settled_now = False
            if needed or background:
                progress["rounds"] += 1
                settled_now = await self._begin_tile_wait(session, wait, needed, background)
            if settled_now:
                walk.resume(wait.take_unavailable())
                continue
            if wait.idle:
                return
            while True:
                sending = wait.take_needed()
                if sending is not None:
                    yield SegmentTilesStreamResponse(
                        needed=TilesNeeded(tiles=_cells(sending.tiles), background_cells=_cells(sending.background))
                    )
                settled = await self._receive_tiles(reader, session, wait)
                if settled is None:
                    raise _ClientEnded()
                if settled:
                    break
            walk.resume(wait.take_unavailable())

    async def _pin_start_tiles(
        self,
        session: "_TileSession",
        request: SegmentTilesRequest,
        context: ServicerContext,
    ) -> bool:
        """Pin every tile the request names that the server holds, for the whole call. False after an abort.

        A tile the server does not hold is not an error, even one under a click: the walk asks the client for it in
        its first round. So a client names the cells under its prompt without uploading them first, and renders
        only the ones the server turns out not to have.
        """
        seen: set[Tuple[int, int]] = set()
        try:
            for tile in request.tiles:
                index = TileIndex(row=int(tile.row), col=int(tile.col))
                if (index.row, index.col) in seen:
                    continue
                seen.add((index.row, index.col))
                if await self._pin_tile(session, index):
                    session.uploaded.append(index)
                    continue
                logger.info(
                    "SegmentTiles start tile row=%s col=%s ds=%s is not held; the walk asks for it",
                    tile.row,
                    tile.col,
                    session.grid.downsample,
                )
        except UnrecoverableGpuError as e:
            await self._abort_unrecoverable_gpu(e, context)
            return False
        return True

    def _cell_predictor(self, session: "_TileSession", settings: "_PredictSettings") -> CellPredict:
        """The ``predict(row, col, points, labels, box=None)`` the growth walk calls for each cell."""
        grid = session.grid

        def predict_cell(
            row: int,
            col: int,
            points: List[Tuple[int, int]],
            labels: List[int],
            box: Optional[Tuple[int, int, int, int]] = None,
            mask_logits: Optional[NDArray] = None,
        ):
            """One 1024 window. An aligned cell reuses its pinned tile embedding.

            ``mask_logits`` is the window's mask prompt: the encoded object prompt for a start window, what the
            neighboring windows found for one the walk grew into.
            """
            cell = Cell(row=row, col=col)
            if is_aligned(cell):
                found = session.predictors.get((tiles_for_cell(cell)[0].row, tiles_for_cell(cell)[0].col))
                if found is not None:
                    predictor, predictor_lock, height, width = found
                    with predictor_lock:
                        with tile_work(grid.volume, int(grid.section), col, row):
                            return self.model.predict_tile(
                                predictor,
                                points,
                                labels,
                                settings.multimask_output,
                                (height, width),
                                box,
                                settings.mask_threshold,
                                settings.use_mask_input,
                                mask_logits=mask_logits,
                            )

            window = crop_window(cell, session.images)
            if window is None:
                missing = [
                    tile for tile in tiles_for_cell(cell) if (tile.row, tile.col) not in session.images
                ]
                raise PredictUnavailable(f"cell row={row} col={col}", tiles=missing)
            with tile_work(grid.volume, int(grid.section), col, row):
                return self.model.predict_ephemeral(
                    window,
                    points,
                    labels,
                    settings.multimask_output,
                    (int(window.shape[0]), int(window.shape[1])),
                    box,
                    settings.mask_threshold,
                    settings.use_mask_input,
                    mask_logits=mask_logits,
                )

        return predict_cell

    def _build_results(self, outcomes: Sequence[_IslandOutcome], logit_iso: float) -> List[SegmentResult]:
        """One SegmentResult per island that found something, in island order (CPU work: hole filling and cropping)."""
        results: List[SegmentResult] = []
        for outcome in sorted(outcomes, key=lambda item: item.island):
            result = outcome.result
            segment = build_island_result(
                outcome.island,
                result.score,
                result.mask,
                result.logits,
                result.origin_x,
                result.origin_y,
                logit_iso,
            )
            if segment is not None:
                results.append(segment)
        return results

    async def _stream_result(
        self,
        request: SegmentTilesRequest,
        outcomes: Sequence[_IslandOutcome],
        session: "_TileSession",
        start: _Start,
        tag: str,
        rounds: int,
        start_time: float,
        logit_iso: float = DEFAULT_MASK_THRESHOLD,
    ) -> SegmentationResponse:
        """Encode the finished walks as the response, write the debug dumps, and log the summary line.

        Each island's result is its fused logits at SAM2's native resolution (``LOGIT_SCALE`` pixels per sample),
        cropped to what is above ``logit_iso`` plus a border; an island whose walk found nothing has no result.
        """
        segments = await asyncio.get_running_loop().run_in_executor(
            self._cpu_executor, functools.partial(self._build_results, outcomes, logit_iso)
        )
        response = SegmentationResponse(segments=segments, request_id=request.request_id)
        grid = session.grid
        for outcome in outcomes:
            result = outcome.result
            if dump_enabled():
                dump_growth(
                    f"{tag}-i{outcome.island}",
                    fused_mask=result.mask,
                    fused_logits=result.logits,
                    origin=(result.origin_x, result.origin_y),
                    cells=result.cells,
                    uploaded=session.uploaded,
                    requested=result.requested,
                    foreground=outcome.clicks,
                    background=start.background,
                    score=result.score,
                    logit_iso=logit_iso,
                    request_id=request.request_id,
                    seams=result.seams,
                    edges=result.edges,
                )
        logger.info(
            "SegmentTiles ok req=%s key=vol=%s|sec=%s|ch=%s|xf=%s|space=%s|ds=%s "
            "tiles=%s islands=%s segments=%s rounds=%s predicted=%s in %.3fs session=%s "
            "requested_tiles=%s background_shapes=%s %s",
            request.request_id,
            grid.volume,
            grid.section,
            grid.channel,
            grid.transform,
            space_name(grid.space),
            grid.downsample,
            len(session.uploaded),
            len(outcomes),
            [(segment.island, segment.logits_size.width, segment.logits_size.height) for segment in segments],
            rounds,
            sum(outcome.predictions for outcome in outcomes),
            time.perf_counter() - start_time,
            tag,
            sorted({(tile.row, tile.col) for outcome in outcomes for tile in outcome.result.requested}),
            session.background.shape_count,
            describe_prompts(start.foreground, start.background),
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

    async def _begin_tile_wait(
        self,
        session: "_TileSession",
        wait: "_TileWait",
        needed: List[TileIndex],
        background: Sequence[TileIndex] = (),
    ) -> bool:
        """Decide, tile by tile, where each needed tile comes from. True when one was settled at once.

        The server's own caches come first (:meth:`_pin_tile`: the image cache, then the tile
        store), so a tile another user or an earlier session left there is not rendered and
        uploaded again. A tile some other stream has already asked its client for is subscribed
        to, not asked again. Every other tile is claimed, recorded as in flight so a stream that
        needs it later can subscribe in turn, then read from the server's tile source when the
        stream's annotation context is local here (:meth:`_source_tiles`); what that cannot read
        is asked of this client, with a future its upload resolves. Background cells not asked for
        before are added to the next TilesNeeded.

        Raises:
            UnrecoverableGpuError: Rebuilding a stored tile found the CUDA context dead.
        """
        wait.background_to_send.extend(session.background.ask((tile.row, tile.col) for tile in background))
        settled = False
        missing: List[TileIndex] = []
        # One at a time: a pin taken by a lookup still running after the stream ended would never be released.
        for tile in needed:
            coord = (tile.row, tile.col)
            if coord in session.unavailable:
                wait.unavailable.append(tile)
                settled = True
            elif coord in wait.asked or coord in wait.shared:
                continue
            elif await self._pin_tile(session, tile):
                settled = True
            else:
                missing.append(tile)
        if len(missing) < len(needed):
            logger.info(
                "SegmentTiles %d of %d needed tiles were held by the server; the client is not asked for them",
                len(needed) - len(missing),
                len(needed),
            )
        now = asyncio.get_running_loop().time()
        claimed: List[TileIndex] = []
        for tile in missing:
            key = session.ref(tile).key
            if self._tile_share_timeout <= 0:
                claimed.append(tile)
                continue
            subscription = self._tile_requests.claim(key, session.token)
            if subscription is None:
                claimed.append(tile)
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
        unsourced = await self._source_tiles(session, claimed)
        if len(unsourced) < len(claimed):
            settled = True
        for tile in unsourced:
            wait.ask(tile, self._tile_requests.watch(session.ref(tile).key, session.token))
        wait.answer_deadline = asyncio.get_running_loop().time() + self._tile_answer_timeout
        return settled

    async def _source_tiles(self, session: "_TileSession", claimed: List[TileIndex]) -> List[TileIndex]:
        """Read the tiles this stream just claimed from the server's own tile source. Returns the ones it could not.

        Only tiles this stream claimed are read, so two streams never fetch the same cell: a stream that needs a
        tile another is reading subscribes to that claim like any other. A read cell goes in the tile store and is
        pinned from there (:meth:`_pin_tile`), the same path a cell rebuilt after a restart takes.

        Raises:
            UnrecoverableGpuError: Encoding a read tile found the CUDA context dead.
        """
        if self._tile_source is None or session.annotation_context_id <= 0 or not claimed:
            return claimed
        refs = [session.ref(tile) for tile in claimed]
        results = await asyncio.gather(
            *(self._tile_source.fetch(session.annotation_context_id, ref) for ref in refs),
            return_exceptions=True,
        )
        unsourced: List[TileIndex] = []
        sourced = 0
        for tile, ref, data in zip(claimed, refs, results):
            if isinstance(data, BaseException):
                logger.warning("Tile source failed row=%s col=%s: %s", tile.row, tile.col, data)
                data = None
            if not data:
                unsourced.append(tile)
                continue
            key = ref.key
            self._keep_tile(key, data)
            if await self._pin_tile(session, tile):
                self._tile_requests.arrived(key)
                sourced += 1
            else:
                unsourced.append(tile)
        if sourced:
            logger.info(
                "SegmentTiles read %d of %d claimed tiles from context %s; the client is not asked for them",
                sourced,
                len(claimed),
                session.annotation_context_id,
            )
        return unsourced

    async def _receive_tiles(
        self,
        reader: "_StreamReader",
        session: "_TileSession",
        wait: "_TileWait",
    ) -> Optional[bool]:
        """Wait until something the walk waits for settles, or there is something to send.

        Three things settle a tile: its upload reaching the cache (this client's, or another stream's for a borrowed
        tile), and this client listing it in a TilesAnswer's ``unavailable``. An answer also brings the background
        shapes for the cells its TilesNeeded asked about. A borrowed tile whose wait runs out, or whose other stream
        gives up, is asked of this client instead; that puts it in ``wait.to_send`` and this returns False so the
        caller sends it, then calls again.

        Returns True when something settled (or nothing is outstanding), False when there is something to send, and
        None when the client ends its half of the stream first. The walk is paused while this runs, so
        ``session.predictors`` and ``session.images`` are not read by the inference thread.

        Raises:
            _ProtocolError: A message other than an answer arrived, or an answer is not usable.
            _OverLimit: An answer lists more than the server accepts.
            _AnswerTimeout: The client uploaded and answered nothing for the idle limit.
        """
        loop = asyncio.get_running_loop()
        while True:
            if wait.to_send or wait.background_to_send:
                return False
            if wait.idle:
                return True
            deadlines = [shared.deadline for shared in wait.shared.values()]
            if wait.waiting_on_client:
                deadlines.append(wait.answer_deadline)
            read = reader.pending()
            watched = {read, *(shared.future for shared in wait.shared.values()), *wait.asked.values()}
            done, _pending = await asyncio.wait(
                watched,
                timeout=max(0.0, min(deadlines) - loop.time()),
                return_when=asyncio.FIRST_COMPLETED,
            )
            progressed = await self._settle_shared_tiles(session, wait, loop.time())
            progressed = await self._settle_arrivals(session, wait) or progressed
            if read in done:
                message = reader.take()
                if message is None:
                    return None
                progressed = await self._apply_answer(message, session, wait) or progressed
            now = loop.time()
            if progressed:
                wait.answer_deadline = now + self._tile_answer_timeout
                return True
            if wait.waiting_on_client and not wait.to_send and now >= wait.answer_deadline:
                raise _AnswerTimeout()

    async def _settle_shared_tiles(self, session: "_TileSession", wait: "_TileWait", now: float) -> bool:
        """Pin borrowed tiles that have arrived; ask this client for the ones that did not. True when one was pinned."""
        pinned = False
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
                if await self._pin_tile(session, shared.tile):
                    logger.info(
                        "SegmentTiles tile row=%s col=%s arrived from another stream's upload",
                        shared.tile.row,
                        shared.tile.col,
                    )
                    pinned = True
                    continue
                reason = "it arrived but is no longer cached"
            logger.info(
                "SegmentTiles tile row=%s col=%s: %s; asking this stream's client",
                shared.tile.row,
                shared.tile.col,
                reason,
            )
            wait.ask(shared.tile, self._tile_requests.watch(shared.key, session.token))
            wait.answer_deadline = now + self._tile_answer_timeout
        return pinned

    async def _settle_arrivals(self, session: "_TileSession", wait: "_TileWait") -> bool:
        """Pin the asked tiles whose upload has landed. True when one settled.

        An upload that landed but is no longer in the server's caches (evicted at once under memory pressure) leaves
        the tile unavailable for this call, as a client listing it would.
        """
        settled = False
        for coord, arrival in list(wait.asked.items()):
            if not arrival.done():
                continue
            del wait.asked[coord]
            tile = TileIndex(row=coord[0], col=coord[1])
            settled = True
            if not arrival.cancelled() and arrival.result() and await self._pin_tile(session, tile):
                continue
            logger.info("SegmentTiles tile row=%s col=%s was uploaded but is not cached; using it as unavailable", *coord)
            self._settle_unavailable(session, wait, tile)
        return settled

    def _settle_unavailable(self, session: "_TileSession", wait: "_TileWait", tile: TileIndex) -> None:
        """Record a tile this client cannot supply: growth does not enter it, and no walk of this call asks again."""
        wait.unavailable.append(tile)
        session.unavailable.add((tile.row, tile.col))
        self._tile_requests.release(session.ref(tile).key, session.token)

    async def _apply_answer(self, message: SegmentTilesStreamRequest, session: "_TileSession", wait: "_TileWait") -> bool:
        """Apply one ``answer``: the oldest TilesNeeded is answered. Always progress (its background cells settle).

        Its ``unavailable`` tiles that are still owed settle as unavailable; a listed tile that is not owed (uploaded
        already, or never asked) is ignored. Its ``background_shapes`` join the background, and the cells the
        TilesNeeded asked about count as known. A tile of that TilesNeeded that was neither listed nor seen uploading is
        looked up in the cache once, in case its upload's wake was missed; otherwise it is still awaited.

        Raises:
            _ProtocolError: The message is not an ``answer``, no TilesNeeded is waiting for one, or a background shape
                is not usable.
            _OverLimit: The answer lists more tiles or shapes than the server accepts.
        """
        if message.WhichOneof("body") != "answer":
            raise _ProtocolError("SegmentTilesStream expected an answer while tiles were requested.")
        answer = message.answer
        if not wait.answers:
            raise _ProtocolError("A TilesAnswer arrived with no TilesNeeded waiting for one.")
        if len(answer.unavailable) > self._limits.max_answer_tiles:
            raise _OverLimit(
                "unavailable tiles in one TilesAnswer", len(answer.unavailable), self._limits.max_answer_tiles,
                "SEGMENTATION_MAX_ANSWER_TILES",
            )
        if len(answer.background_shapes) > self._limits.max_shapes:
            raise _OverLimit(
                "background shapes in one TilesAnswer", len(answer.background_shapes), self._limits.max_shapes,
                "SEGMENTATION_MAX_SHAPES",
            )
        try:
            shapes = [parse_shape(shape, f"answer background shape {i}") for i, shape in enumerate(answer.background_shapes)]
        except ShapeError as e:
            raise _ProtocolError(str(e)) from e
        needed = wait.answers.popleft()
        added = session.background.add_shapes(shapes)
        session.background.cover(needed.background)
        if needed.background:
            logger.info(
                "SegmentTiles background for %d cell(s): %d shape(s)", len(needed.background), added
            )
        for cell in answer.unavailable:
            coord = (int(cell.row), int(cell.col))
            arrival = wait.asked.pop(coord, None)
            if arrival is None:
                continue
            arrival.cancel()
            self._settle_unavailable(session, wait, TileIndex(row=coord[0], col=coord[1]))
        for tile in needed.tiles:
            arrival = wait.asked.get((tile.row, tile.col))
            if arrival is None or arrival.done():
                continue
            if await self._pin_tile(session, tile):
                wait.asked.pop((tile.row, tile.col)).cancel()
                self._tile_requests.arrived(session.ref(tile).key)
        return True

    async def _pin_tile(self, session: "_TileSession", index: TileIndex) -> bool:
        """Pin one tile for the rest of the request from the server's own caches. False when none holds it.

        The image cache comes first. Then the tile store, whose bytes go back into the image cache
        (the disk embedding store usually spares the encoder) and wake any stream waiting for that
        tile. A stored tile the image cache cannot take back is still used from its pixels: its
        windows are predicted on throwaway predictors.

        Raises:
            UnrecoverableGpuError: Rebuilding the tile's embedding found the CUDA context dead.
        """
        key = session.ref(index).key
        pinned = await self.image_cache.get_image_by_tile(key)
        if pinned is not None:
            self._note_tile_used(key, pinned[4])
        else:
            data = await self._stored_tile(key)
            if data is None:
                return False
            decoded = await self._decode_stored_tile(key, data)
            if decoded is None:
                return False
            try:
                await self.image_cache.upload_tile(
                    key, data, TILE_SIZE, TILE_SIZE, executor=self.inference_executor, decoded=decoded
                )
                pinned = await self.image_cache.get_image_by_tile(key)
            except (CacheFullError, PredictorCreationError) as e:
                logger.warning("Stored tile key=%s could not go back in the image cache (%s); using its pixels", key, e)
            if pinned is None:
                session.images.add(index.row, index.col, data, decoded)
                return True
            logger.info("SegmentTiles tile row=%s col=%s rebuilt from the tile store", index.row, index.col)
            self._tile_requests.arrived(key)
        image_id, image_bytes, width, height, predictor, predictor_lock = pinned
        session.pinned_ids.append(image_id)
        _remember_tile_image(session.images, index.row, index.col, image_bytes)
        if (index.row, index.col) not in session.images:
            return False
        if predictor is not None:
            session.predictors[(index.row, index.col)] = (predictor, predictor_lock, height, width)
        return True

    async def _stored_tile(self, key: TileCacheKey) -> Optional[bytes]:
        """The tile store's bytes for ``key``: from memory on the loop, from disk on a worker thread."""
        data = self._tile_store.recall(key)
        if data is None and self._tile_store.has_disk:
            data = await asyncio.get_running_loop().run_in_executor(self._cpu_executor, self._tile_store.load, key)
        return data

    async def _decode_stored_tile(self, key: TileCacheKey, data: bytes) -> Optional[NDArray[np.uint8]]:
        """The stored tile's pixels, or None (and the tile forgotten) when they are not a whole tile."""
        loop = asyncio.get_running_loop()
        try:
            decoded = await loop.run_in_executor(self._cpu_executor, prepare_image_for_sam2, data)
        except (OSError, ValueError):
            decoded = None
        if decoded is not None and decoded.shape[:2] == (TILE_SIZE, TILE_SIZE):
            return decoded
        logger.warning("Stored tile key=%s is not a %sx%s image; forgetting it", key, TILE_SIZE, TILE_SIZE)
        await loop.run_in_executor(self._cpu_executor, self._tile_store.forget, key)
        return None

    def _keep_tile(self, key: TileCacheKey, data: bytes) -> None:
        """Put a newly cached tile in the tile store: memory now, disk on a worker thread the upload does not wait for."""
        self._tile_store.remember(key, data)
        if self._tile_store.has_disk:
            self._in_background(self._tile_store.persist, key, data)

    def _note_tile_used(self, key: TileCacheKey, predictor: Any) -> None:
        """Refresh the disk files of a tile found in the image cache, so a tile that stays hot is not evicted from disk.

        Without an upload nothing else touches them: the walk no longer asks a client for a tile the cache holds.
        """
        note = getattr(self.model, "note_predictor_used", None)
        if not callable(note):
            note = None
        if not self._tile_store.has_disk and (note is None or predictor is None):
            return

        def touch() -> None:
            self._tile_store.touch(key)
            if note is not None and predictor is not None:
                note(predictor)

        self._in_background(touch)

    def _in_background(self, work: Callable[..., None], *args: Any) -> None:
        """Run ``work`` on the CPU pool without waiting for it. Failures are logged."""

        def run() -> None:
            try:
                work(*args)
            except Exception:
                logger.exception("Background tile store work failed")

        try:
            self._cpu_executor.submit(run)
        except RuntimeError:
            logger.debug("CPU pool is shut down; background tile store work skipped")



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
    cache_gpu_max_bytes: int = DEFAULT_GPU_MAX_BYTES,
    cache_tile_idle_seconds: float = DEFAULT_TILE_IDLE_SECONDS,
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
            cache_gpu_max_bytes=cache_gpu_max_bytes,
            cache_tile_idle_seconds=cache_tile_idle_seconds,
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
    demo_plaintext: Optional[bool] = None,
    cache_gpu_max_bytes: int = DEFAULT_GPU_MAX_BYTES,
    cache_tile_idle_seconds: float = DEFAULT_TILE_IDLE_SECONDS,
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
        cache_gpu_max_bytes=cache_gpu_max_bytes,
        cache_tile_idle_seconds=cache_tile_idle_seconds,
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
            plaintext=demo_plaintext,
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
