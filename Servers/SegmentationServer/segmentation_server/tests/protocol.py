"""Helpers for tests that talk to SegmentationServicer the way a client does: grids, cells, a start message, and a
conversation driver that answers each TilesNeeded."""

from __future__ import annotations

import asyncio
import inspect
from typing import Awaitable, Callable, Iterable, List, Optional, Sequence, Tuple, Union
from unittest.mock import AsyncMock, MagicMock

import cv2
import numpy as np

from segmentation_grpc import (
    Circle,
    CoordinateSpace,
    Feature,
    GridCell,
    MaskTile,
    MaskTiles,
    Polygon,
    Ring,
    SegmentTilesRequest,
    SegmentTilesStreamRequest,
    SegmentTilesStreamResponse,
    Shape,
    TileGrid,
    TilesAnswer,
    TilesNeeded,
    UploadTileRequest,
    Vector2,
)
from segmentation_server.image_cache import ImageCache
from segmentation_server.server import SegmentationServicer

VOLUME_KEY = ("RC2", 1305, "TEM", "SliceToVolume1|", "volume", 1)

AnswerFor = Callable[[TilesNeeded], Union[Optional[TilesAnswer], Awaitable[Optional[TilesAnswer]]]]


def grid(**changes) -> TileGrid:
    """The test grid: RC2 section 1305, TEM, volume space, downsample 1."""
    fields = dict(
        volume="RC2",
        section=1305,
        channel="TEM",
        transform="SliceToVolume1|",
        downsample=1,
        space=CoordinateSpace.COORDINATE_SPACE_VOLUME,
    )
    fields.update(changes)
    return TileGrid(**fields)


def cell(row: int, col: int) -> GridCell:
    return GridCell(row=row, col=col)


def point(x: float, y: float) -> Vector2:
    return Vector2(x=x, y=y)


def circle(x: float, y: float, radius: float) -> Shape:
    return Shape(circle=Circle(center=Vector2(x=x, y=y), radius=radius))


def polygon(points: Sequence[Tuple[float, float]], holes: Sequence[Sequence[Tuple[float, float]]] = ()) -> Shape:
    return Shape(polygon=Polygon(
        exterior=Ring(points=[Vector2(x=x, y=y) for x, y in points]),
        holes=[Ring(points=[Vector2(x=x, y=y) for x, y in hole]) for hole in holes],
    ))


def start(
    points: Iterable[Tuple[float, float]] = (),
    tiles: Iterable[Tuple[int, int]] = ((0, 0),),
    shapes: Iterable[Shape] = (),
    background_points: Iterable[Tuple[float, float]] = (),
    background_shapes: Iterable[Shape] = (),
    **fields,
) -> SegmentTilesRequest:
    """A start message on the test grid. ``fields`` override anything (``grid=...``, ``mask_threshold=...``)."""
    request = SegmentTilesRequest(
        grid=fields.pop("grid", grid()),
        tiles=[cell(row, col) for row, col in tiles],
        foreground_feature=Feature(points=[point(x, y) for x, y in points], shapes=list(shapes)),
        background_feature=Feature(points=[point(x, y) for x, y in background_points], shapes=list(background_shapes)),
        **fields,
    )
    return request


def context() -> AsyncMock:
    """A gRPC context whose cancelled() is a plain bool, as in grpc.aio, not a coroutine."""
    ctx = AsyncMock()
    ctx.cancelled = MagicMock(return_value=False)
    return ctx


def cache(**changes) -> ImageCache:
    fields = dict(max_memory_bytes=100_000_000, ttl_seconds=60, max_entries=8, create_predictor_func=lambda *_args: object())
    fields.update(changes)
    return ImageCache(**fields)


def png(value: int = 90) -> bytes:
    ok, encoded = cv2.imencode(".png", np.full((1024, 1024), value, dtype=np.uint8))
    assert ok
    return encoded.tobytes()


async def put_tile(image_cache: ImageCache, row: int, col: int, data: bytes = b"seed") -> None:
    """Put a tile straight in the image cache, as an earlier session would have left it."""
    await image_cache.upload_tile((*VOLUME_KEY, row, col), data, 1024, 1024)


def servicer(image_cache: Optional[ImageCache] = None, mask=None, logits=None, **kwargs) -> SegmentationServicer:
    """A servicer over a MagicMock model whose every prediction is ``(mask, logits, 0.8)``."""
    model = kwargs.pop("model", None)
    if model is None:
        model = MagicMock(spec=[
            "predict_tile", "predict_ephemeral", "create_initialized_predictor", "release_predictor",
        ])
        if mask is None:
            mask = np.zeros((4, 4), dtype=np.bool_)
            mask[1, 1] = True
        model.predict_tile.return_value = (mask, logits, 0.8)
        model.predict_ephemeral.return_value = (mask, logits, 0.8)
    return SegmentationServicer(
        model=model, image_cache=image_cache if image_cache is not None else cache(), server_start_time=0.0, **kwargs
    )


def all_unavailable(needed: TilesNeeded) -> TilesAnswer:
    """The client cannot supply any tile, and has no neighbouring annotations."""
    return TilesAnswer(unavailable=list(needed.tiles))


def uploader(target: SegmentationServicer, data: Optional[bytes] = None) -> AnswerFor:
    """A client that uploads every tile asked for through UploadTile, then answers with nothing to list."""
    image = data if data is not None else png()

    async def answer(needed: TilesNeeded) -> TilesAnswer:
        for asked in needed.tiles:
            await target.UploadTile(UploadTileRequest(grid=grid(), cell=asked, image_data=image), context())
        return TilesAnswer()

    return answer


async def converse(
    target: SegmentationServicer,
    request: SegmentTilesRequest,
    ctx: Optional[AsyncMock] = None,
    answer_for: AnswerFor = all_unavailable,
    mask_tiles: Sequence[MaskTile] = (),
) -> List[SegmentTilesStreamResponse]:
    """Send ``start`` and ``mask_tiles``, answer each ``needed`` with ``answer_for`` (None ends the client's stream)."""
    if mask_tiles and not request.mask_tile_count:
        request.mask_tile_count = len(mask_tiles)
    inbox: asyncio.Queue = asyncio.Queue()
    await inbox.put(SegmentTilesStreamRequest(start=request))
    if mask_tiles:
        await inbox.put(SegmentTilesStreamRequest(mask_tiles=MaskTiles(tiles=list(mask_tiles))))

    async def incoming():
        while True:
            item = await inbox.get()
            if item is None:
                return
            yield item

    updates: List[SegmentTilesStreamResponse] = []
    async for update in target.SegmentTilesStream(incoming(), ctx or context()):
        updates.append(update)
        if update.WhichOneof("update") == "needed":
            answer = answer_for(update.needed)
            if inspect.isawaitable(answer):
                answer = await answer
            await inbox.put(None if answer is None else SegmentTilesStreamRequest(answer=answer))
    return updates


def result(updates: List[SegmentTilesStreamResponse]):
    assert updates, "the stream produced nothing"
    assert updates[-1].WhichOneof("update") == "result"
    return updates[-1].result


def needed_tiles(updates: List[SegmentTilesStreamResponse]) -> List[set]:
    """The image tiles of each TilesNeeded that asked for any (a round that asked only for background is left out)."""
    return [
        {(tile.row, tile.col) for tile in update.needed.tiles}
        for update in updates
        if update.WhichOneof("update") == "needed" and update.needed.tiles
    ]


def background_cells(updates: List[SegmentTilesStreamResponse]) -> List[set]:
    """The background cells of each TilesNeeded that asked for any."""
    return [
        {(tile.row, tile.col) for tile in update.needed.background_cells}
        for update in updates
        if update.WhichOneof("update") == "needed" and update.needed.background_cells
    ]


def released(image_cache: ImageCache) -> bool:
    """True when no cache entry is pinned any more."""
    return all(entry.in_use == 0 for entry in image_cache._cache.values())
