"""Tiled mask prompts on SegmentTilesStream: MaskTile, foreground_mask_tile_count, TilesNeeded.masks.

The mask of a request arrives as the grid tiles it touches instead of one large ``Mask``. The foreground is announced and
sent after ``start``; the background is asked for window by window. These tests check the tile store against the mask it
stands for, the walk against the same mask held as one array, and the conversation on the stream.
"""

from __future__ import annotations

import asyncio
from typing import Callable, Iterable, List, Optional, Sequence, Set, Tuple
from unittest.mock import AsyncMock, MagicMock

import cv2
import grpc
import numpy as np
import pytest

from cell_world import World, ellipse
from segmentation_grpc import (
    Point,
    SegmentTilesRequest,
    SegmentTilesStreamRequest,
    SegmentTilesStreamResponse,
    ServerStatusRequest,
    TileCoord,
    TilesAnswer,
)
from segmentation_grpc import segmentation_pb2 as pb
from segmentation_server import trainer_prompt_encoding as enc
from segmentation_server.cell_grid import (
    CELL_SIZE,
    LOGIT_SCALE,
    TILE_SIZE,
    Cell,
    MaskShape,
    TileIndex,
    tiles_for_cell,
)
from segmentation_server.image_cache import ImageCache
from segmentation_server.mask_tiles import (
    MaskTileError,
    MaskTileLimitError,
    MaskTileSet,
    TiledMask,
    parse_mask_tile,
)
from segmentation_server.prompt_encodings import EdgeConfidenceEncoder
from segmentation_server.server import RequestLimits, SegmentationServicer
from segmentation_server.tile_growth import PredictUnavailable, grow_segmentation

FOREGROUND = pb.MASK_KIND_FOREGROUND
BACKGROUND = pb.MASK_KIND_BACKGROUND
_VOLUME_KEY = ("RC2", 1305, "TEM", "SliceToVolume1|", 1)


# ---- building masks and tiles -------------------------------------------------------------------------------------------


def _coord(row: int, col: int, downsample: int = 1, volume: str = "RC2") -> TileCoord:
    return TileCoord(
        volume=volume, section=1305, channel="TEM", transform="SliceToVolume1|", downsample=downsample, row=row, col=col
    )


def _png(down: np.ndarray) -> bytes:
    ok, encoded = cv2.imencode(".png", down.astype(np.uint8) * 255)
    assert ok
    return encoded.tobytes()


def _tile_from_down(
    row: int,
    col: int,
    kind: int,
    down: Optional[np.ndarray],
    scale: int = 1,
    force_png: bool = False,
    downsample: int = 1,
) -> pb.MaskTile:
    """One ``MaskTile`` for a Y-down 1024 x 1024 boolean tile, the way a client builds it (a block is set when any pixel is)."""
    coord = _coord(row, col, downsample)
    if down is None or not down.any():
        return pb.MaskTile(coord=coord, kind=kind)
    if down.all() and not force_png:
        return pb.MaskTile(coord=coord, kind=kind, full=True)
    if scale == 4:
        blocks = down.reshape(TILE_SIZE // 4, 4, TILE_SIZE // 4, 4).any(axis=(1, 3))
        return pb.MaskTile(coord=coord, kind=kind, png=_png(blocks), scale=4)
    return pb.MaskTile(coord=coord, kind=kind, png=_png(down))


def _tiles_from_up(
    mask_up: np.ndarray, kind: int, scale: int = 1, force_png: bool = False, include_empty: bool = False
) -> List[pb.MaskTile]:
    """The tiles of a Y-up mask array whose origin is mosaic (0, 0), skipping tiles with nothing in them."""
    tiles: List[pb.MaskTile] = []
    for row in range((mask_up.shape[0] + TILE_SIZE - 1) // TILE_SIZE):
        for col in range((mask_up.shape[1] + TILE_SIZE - 1) // TILE_SIZE):
            part = mask_up[row * TILE_SIZE:(row + 1) * TILE_SIZE, col * TILE_SIZE:(col + 1) * TILE_SIZE]
            padded = np.zeros((TILE_SIZE, TILE_SIZE), dtype=bool)
            padded[:part.shape[0], :part.shape[1]] = part
            if not padded.any() and not include_empty:
                continue
            tiles.append(_tile_from_down(row, col, kind, np.flipud(padded), scale, force_png))
    return tiles


def _predicate_tile(row: int, col: int, kind: int, inside: Callable[[np.ndarray, np.ndarray], np.ndarray]) -> pb.MaskTile:
    """A tile of any mask given as ``inside(x, y)`` over mosaic coordinates, so the mask may reach past the 4096 test domain."""
    ys = (row * TILE_SIZE + TILE_SIZE - 1 - np.arange(TILE_SIZE))[:, None]
    xs = (col * TILE_SIZE + np.arange(TILE_SIZE))[None, :]
    return _tile_from_down(row, col, kind, inside(xs, ys))


def _tile_set(tiles: Sequence[pb.MaskTile], role: str = "seed", count: Optional[int] = None) -> MaskTileSet:
    foreground = [tile for tile in tiles if tile.kind == FOREGROUND]
    tile_set = MaskTileSet(_coord(0, 0), len(foreground) if count is None else count, role=role)
    tile_set.ingest(tiles)
    return tile_set


def _dense(mask_up: np.ndarray, role: str = "seed") -> MaskShape:
    """The same mask held as one bitmap over its tight bounding box, as a ``Feature.mask`` arrives."""
    ys, xs = np.nonzero(mask_up)
    x0, x1, y0, y1 = int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())
    return MaskShape((x0, y0, x1, y1), mask_up[y0:y1 + 1, x0:x1 + 1], role)


def _equal_or_both_none(left: Optional[np.ndarray], right: Optional[np.ndarray]) -> bool:
    return (left is None and right is None) or (left is not None and right is not None and np.array_equal(left, right))


# ---- the tile store ------------------------------------------------------------------------------------------------------


def test_a_tiled_mask_is_the_same_mask_as_one_bitmap_for_every_window_the_walk_reads() -> None:
    mask_up = ellipse(2560, 2560, 1450, 1300, 0.0)
    tiled = _tile_set(_tiles_from_up(mask_up, FOREGROUND)).foreground
    dense = _dense(mask_up)

    assert tiled.box == dense.box
    assert tiled.interior_point() == dense.interior_point()
    for row in range(0, 9):
        for col in range(0, 9):
            cell = Cell(row, col)
            assert _equal_or_both_none(tiled.window_full(cell), dense.window_full(cell)), cell
            assert _equal_or_both_none(tiled.window_samples(cell), dense.window_samples(cell)), cell
            assert tiled.covers_core(cell) == dense.covers_core(cell), cell


def test_a_window_is_cut_from_up_to_four_tiles() -> None:
    mask_up = np.zeros((2048, 2048), dtype=bool)
    mask_up[900:1200, 900:1200] = True  # straddles the corner shared by tiles (0,0), (0,1), (1,0) and (1,1)
    tiled = _tile_set(_tiles_from_up(mask_up, FOREGROUND)).foreground
    cell = Cell(1, 1)  # window origin (512, 512): reaches all four tiles

    assert len(tiles_for_cell(cell)) == 4
    assert np.array_equal(tiled.window_full(cell), _dense(mask_up).window_full(cell))


def test_an_empty_tile_is_received_and_a_tile_never_sent_is_missing() -> None:
    background = TiledMask()
    _kind, index, content = parse_mask_tile(pb.MaskTile(coord=_coord(2, 3), kind=BACKGROUND))
    background.put(index, content)

    cell = Cell(3, 5)  # window origin (2560, 1536): tiles (1,2), (1,3), (2,2) and (2,3)

    assert background.received(index)
    assert background.window_full(cell) is None, "an empty tile holds nothing"
    assert [(t.row, t.col) for t in background.missing_for_cell(cell)] == [(1, 2), (1, 3), (2, 2)]
    background.mark_unavailable(TileIndex(row=1, col=3))
    assert [(t.row, t.col) for t in background.missing_for_cell(cell)] == [(1, 2), (2, 2)]


def test_full_tiles_and_png_tiles_hold_the_same_mask() -> None:
    mask_up = np.zeros((4096, 4096), dtype=bool)
    mask_up[500:3500, 500:3500] = True  # tiles in the middle are entirely mask
    as_full = _tiles_from_up(mask_up, FOREGROUND)
    as_png = _tiles_from_up(mask_up, FOREGROUND, force_png=True)

    assert any(tile.full for tile in as_full) and not any(tile.full for tile in as_png)
    full_mask = _tile_set(as_full).foreground
    png_mask = _tile_set(as_png).foreground
    assert full_mask.box == png_mask.box == _dense(mask_up).box
    for cell in (Cell(1, 1), Cell(3, 3), Cell(5, 5), Cell(7, 7), Cell(2, 5)):
        assert _equal_or_both_none(full_mask.window_full(cell), png_mask.window_full(cell))
        assert full_mask.covers_core(cell) == png_mask.covers_core(cell)


def test_scale_four_matches_full_resolution_to_within_one_four_pixel_sample_at_the_edge() -> None:
    mask_up = ellipse(2048, 2048, 1300, 1100, 0.3)
    exact = _tile_set(_tiles_from_up(mask_up, FOREGROUND)).foreground
    blocky = _tile_set(_tiles_from_up(mask_up, FOREGROUND, scale=4)).foreground

    for row in range(0, 8):
        for col in range(0, 8):
            cell = Cell(row, col)
            exact_full, blocky_full = exact.window_full(cell), blocky.window_full(cell)
            assert _equal_or_both_none(exact.window_samples(cell), blocky.window_samples(cell)), \
                "a 4 x 4 block is one sample, so SAM2's sample grid is the same"
            if exact_full is None:
                assert blocky_full is None
                continue
            assert not (exact_full & ~blocky_full).any(), "a block is set when any of its pixels is, so nothing is lost"
            extra = blocky_full & ~exact_full
            grown = cv2.dilate(exact_full.astype(np.uint8), np.ones((2 * LOGIT_SCALE - 1, 2 * LOGIT_SCALE - 1), np.uint8)) > 0
            assert not (extra & ~grown).any(), "the difference is at most the edge of one block"


def test_foreground_and_background_tiles_that_overlap_give_zero_on_the_overlap() -> None:
    foreground = np.zeros((4096, 4096), dtype=bool)
    foreground[1200:2800, 1200:2800] = True
    background = np.zeros((4096, 4096), dtype=bool)
    background[2000:3400, 1200:2800] = True  # overlaps the foreground between y = 2000 and 2800
    tiles = _tiles_from_up(foreground, FOREGROUND) + _tiles_from_up(background, BACKGROUND)
    tile_set = _tile_set(tiles)
    cell = Cell(3, 3)  # window origin (1536, 1536), reaching 2559

    prompt = EdgeConfidenceEncoder({}).start_window(cell, tile_set.foreground, [tile_set.background])

    def sample_at(x: int, y: int) -> float:
        return float(prompt[(1536 + CELL_SIZE - 1 - y) // LOGIT_SCALE, (x - 1536) // LOGIT_SCALE])

    assert sample_at(2400, 2300) == 0.0, "foreground and background both claim it: unknown"
    assert sample_at(1700, 1700) > 0.0, "foreground only"
    assert enc.SEED_LOGIT >= sample_at(1700, 1700) > 0.0
    deeper = EdgeConfidenceEncoder({}).start_window(Cell(4, 3), tile_set.foreground, [tile_set.background])
    assert deeper.min() < 0.0, "background only, further up"


def test_a_walk_over_a_tiled_seed_prompts_every_start_window_like_the_same_mask_as_one_array() -> None:
    seed_up = ellipse(2560, 2560, 1450, 1300, 0.0)
    truth = ellipse(2560, 2560, 1500, 1350, 0.0)
    tiles = _tiles_from_up(seed_up, FOREGROUND)
    assert len(tiles) == 9, "the seed covers 3 x 3 tiles"
    tiled = _tile_set(tiles).foreground
    dense = _dense(seed_up)
    encoder = EdgeConfidenceEncoder({})

    def walk(shape):
        world = World(truth)
        result = grow_segmentation(
            [shape.interior_point()], [], world.predict, masks=[shape], prompt_encoder=encoder, max_requested=100
        )
        return world, result

    tiled_world, tiled_result = walk(tiled)
    dense_world, dense_result = walk(dense)

    assert tiled_world.calls == dense_world.calls
    assert len(tiled_world.mask_prompts) == len(dense_world.mask_prompts) > 0
    for ours, theirs in zip(tiled_world.mask_prompts, dense_world.mask_prompts):
        assert _equal_or_both_none(ours, theirs)
    assert np.array_equal(tiled_world.to_truth_frame(tiled_result), dense_world.to_truth_frame(dense_result))
    pinned = tiled_world.to_truth_frame(tiled_result)
    assert pinned[seed_up].all(), "the foreground is pinned in the result"


# ---- validation of the messages --------------------------------------------------------------------------------------------


def _fg(row: int = 1, col: int = 1, **fields) -> pb.MaskTile:
    return pb.MaskTile(coord=fields.pop("coord", _coord(row, col)), kind=fields.pop("kind", FOREGROUND), **fields)


@pytest.mark.parametrize(
    "tile",
    [
        pytest.param(pb.MaskTile(coord=_coord(1, 1)), id="kind unspecified"),
        pytest.param(_fg(png=_png(np.ones((512, 512), bool))), id="png of the wrong size"),
        pytest.param(_fg(png=_png(np.ones((1024, 1024), bool)), scale=4), id="full-size png at scale 4"),
        pytest.param(_fg(png=_png(np.ones((256, 256), bool))), id="scaled png at scale 1"),
        pytest.param(_fg(png=_png(np.ones((512, 512), bool)), scale=2), id="scale 2"),
        pytest.param(_fg(png=_png(np.ones((1024, 1024), bool)), scale=8), id="scale 8"),
        pytest.param(_fg(full=True, png=_png(np.ones((1024, 1024), bool))), id="full with a png"),
        pytest.param(_fg(png=b"not a png at all"), id="undecodable png"),
    ],
)
def test_a_malformed_tile_is_refused(tile) -> None:
    with pytest.raises(MaskTileError):
        parse_mask_tile(tile)


def test_a_tile_names_the_requests_downsample_and_identity() -> None:
    tile_set = MaskTileSet(_coord(0, 0), 3)

    with pytest.raises(MaskTileError, match="downsample"):
        tile_set.ingest([_fg(coord=_coord(1, 1, downsample=2), full=True)])
    with pytest.raises(MaskTileError):
        tile_set.ingest([_fg(coord=_coord(1, 1, volume="another"), full=True)])


def test_a_coord_and_kind_may_appear_once_but_the_same_coord_may_be_both_kinds() -> None:
    tile_set = MaskTileSet(_coord(0, 0), 3)
    tile_set.ingest([_fg(1, 1, full=True), _fg(1, 1, kind=BACKGROUND, full=True)])

    with pytest.raises(MaskTileError, match="twice"):
        tile_set.ingest([_fg(1, 1, full=True)])
    with pytest.raises(MaskTileError, match="twice"):
        tile_set.ingest([_fg(1, 1, kind=BACKGROUND, full=True)])
    with pytest.raises(MaskTileError, match="twice"):
        MaskTileSet(_coord(0, 0), 3).ingest([_fg(2, 2, full=True), _fg(2, 2, full=True)])


def test_more_foreground_tiles_than_announced_are_refused_and_too_many_to_hold_are_a_limit() -> None:
    with pytest.raises(MaskTileError, match="announced"):
        MaskTileSet(_coord(0, 0), 1).ingest([_fg(1, 1, full=True), _fg(1, 2, full=True)])
    with pytest.raises(MaskTileLimitError):
        MaskTileSet(_coord(0, 0), 5, max_foreground=2).ingest([_fg(0, c, full=True) for c in range(3)])
    with pytest.raises(MaskTileLimitError):
        MaskTileSet(_coord(0, 0), 0, max_background=1).ingest(
            [_fg(0, c, kind=BACKGROUND, full=True) for c in range(2)]
        )


# ---- the conversation on the stream ----------------------------------------------------------------------------------------


def _cache() -> ImageCache:
    return ImageCache(
        max_memory_bytes=500_000_000, ttl_seconds=60, max_entries=32, create_predictor_func=lambda _data: object()
    )


def _context() -> AsyncMock:
    context = AsyncMock()
    context.cancelled = MagicMock(return_value=False)
    return context


def _tile(row: int, col: int) -> TileCoord:
    return _coord(row, col)


async def _upload_all(cache: ImageCache, rows: int = 4, cols: int = 4) -> None:
    for row in range(rows):
        for col in range(cols):
            await cache.upload_tile((*_VOLUME_KEY, row, col), b"tile", 1024, 1024)


def _world_predictor(world: World):
    """Stand in for SAM2: predict from the world's truth, needing the window's image tiles pinned like the real one."""

    def factory(session, _settings):
        def predict(row, col, points, labels, box=None, mask_logits=None):
            cell = Cell(row, col)
            missing = [tile for tile in tiles_for_cell(cell) if (tile.row, tile.col) not in session.images]
            if missing:
                raise PredictUnavailable(f"cell row={row} col={col}", tiles=missing)
            extra = {} if box is None else {"box": box}
            return world.predict(row, col, points, labels, mask_logits=mask_logits, **extra)

        return predict

    return factory


def _servicer(cache: ImageCache, world: Optional[World] = None) -> SegmentationServicer:
    model = MagicMock()
    servicer = SegmentationServicer(model=model, image_cache=cache, server_start_time=0.0)
    if world is not None:
        servicer._cell_predictor = _world_predictor(world)  # type: ignore[method-assign]
    return servicer


def _mask_message(tiles: Iterable[pb.MaskTile]) -> SegmentTilesStreamRequest:
    return SegmentTilesStreamRequest(mask_tiles=pb.MaskTiles(tiles=list(tiles)))


async def _converse(
    servicer: SegmentationServicer,
    request: SegmentTilesRequest,
    first_messages: Sequence[SegmentTilesStreamRequest] = (),
    background_for: Optional[Callable[[int, int], Optional[pb.MaskTile]]] = None,
    context: Optional[AsyncMock] = None,
) -> Tuple[List[SegmentTilesStreamResponse], List[pb.TilesNeeded]]:
    """Send ``start`` and ``first_messages``; answer each TilesNeeded with the image tiles ready and the background it asks for.

    ``background_for(row, col)`` returns the BACKGROUND tile to send, or None to list the tile as unavailable.
    """
    inbox: asyncio.Queue = asyncio.Queue()
    await inbox.put(SegmentTilesStreamRequest(start=request))
    for message in first_messages:
        await inbox.put(message)

    async def incoming():
        while True:
            item = await inbox.get()
            if item is None:
                return
            yield item

    updates: List[SegmentTilesStreamResponse] = []
    asked: List[pb.TilesNeeded] = []
    async for update in servicer.SegmentTilesStream(incoming(), context or _context()):
        updates.append(update)
        if update.WhichOneof("update") != "needed":
            continue
        needed = update.needed
        asked.append(needed)
        sent: List[pb.MaskTile] = []
        unavailable: List[TileCoord] = []
        for coord in needed.masks:
            tile = None if background_for is None else background_for(coord.row, coord.col)
            if tile is None:
                unavailable.append(coord)
            else:
                sent.append(tile)
        if sent:
            await inbox.put(_mask_message(sent))
        await inbox.put(SegmentTilesStreamRequest(answer=TilesAnswer(ready=list(needed.tiles), masks_unavailable=unavailable)))
    return updates, asked


def _result(updates: List[SegmentTilesStreamResponse]):
    assert updates and updates[-1].WhichOneof("update") == "result", "the stream ended without a result"
    return updates[-1].result


def _tiles_of(needed: Iterable[pb.TilesNeeded], field: str) -> List[Tuple[int, int]]:
    return [(coord.row, coord.col) for message in needed for coord in getattr(message, field)]


def _status(context: AsyncMock) -> Optional[grpc.StatusCode]:
    return context.abort.await_args.args[0] if context.abort.await_args is not None else None


@pytest.mark.asyncio
async def test_a_neighbour_mask_over_20_tiles_is_asked_for_only_where_the_walk_goes() -> None:
    cache = _cache()
    await _upload_all(cache)
    world = World(ellipse(2048, 2048, 700, 500, 0.0))
    servicer = _servicer(cache, world)
    # Every neighbour tile of a 5 x 4 block of tiles (20 of them), with a hole where the object is.
    hole = lambda x, y: (x - 2048) ** 2 + (y - 2048) ** 2 <= 900**2  # noqa: E731
    background_for = lambda row, col: _predicate_tile(  # noqa: E731
        row, col, BACKGROUND, lambda x, y: ((x < 5120) & (y < 4096)) & ~hole(x, y)
    )
    request = SegmentTilesRequest(tiles=[_tile(2, 2)], foreground=[Point(x=2048, y=2048)], foreground_mask_tile_count=0)

    updates, asked = await _converse(servicer, request, background_for=background_for)

    _result(updates)
    asked_masks = _tiles_of(asked, "masks")
    entered = {(tile.row, tile.col) for cell, _points, _labels in world.calls for tile in tiles_for_cell(cell)}
    assert set(asked_masks) == entered, "the server asks for the tiles the windows it predicts cover, and no others"
    assert len(asked_masks) == len(set(asked_masks)) < 20, "each tile once, and not the whole 20"
    assert asked[0].masks and not asked[0].tiles, "the first ask is for background tiles alone"
    assert (2, 2) in {(coord.row, coord.col) for coord in asked[0].masks}, \
        "asked for although the image of tile (2, 2) is already pinned"
    assert _tiles_of(asked[1:], "tiles"), "the image tiles are asked for afterwards"


@pytest.mark.asyncio
async def test_a_background_tile_the_client_cannot_supply_means_no_neighbours() -> None:
    cache = _cache()
    await _upload_all(cache)
    world = World(ellipse(2048, 2048, 400, 400, 0.0))
    servicer = _servicer(cache, world)
    request = SegmentTilesRequest(tiles=[_tile(2, 2)], foreground=[Point(x=2048, y=2048)], foreground_mask_tile_count=0)

    updates, asked = await _converse(servicer, request, background_for=None)

    _result(updates)
    assert world.calls, "growth was not blocked"
    assert _tiles_of(asked, "masks")


@pytest.mark.asyncio
async def test_background_tiles_sent_with_the_foreground_are_not_asked_for_again() -> None:
    cache = _cache()
    await _upload_all(cache)
    world = World(ellipse(2048, 2048, 200, 200, 0.0))
    servicer = _servicer(cache, world)
    request = SegmentTilesRequest(tiles=[_tile(2, 2)], foreground=[Point(x=2048, y=2048)], foreground_mask_tile_count=1)
    foreground = pb.MaskTile(coord=_coord(2, 2), kind=FOREGROUND, full=True)
    early = [pb.MaskTile(coord=_coord(r, c), kind=BACKGROUND) for r in (1, 2) for c in (1, 2)]

    updates, asked = await _converse(servicer, request, [_mask_message([foreground, *early])])

    _result(updates)
    already_sent = {(1, 1), (1, 2), (2, 1), (2, 2)}
    assert not already_sent & set(_tiles_of(asked, "masks")), "tiles the client sent with the foreground are not asked for"
    assert _tiles_of(asked, "masks"), "windows the walk goes on to need tiles the client did not send"


@pytest.mark.asyncio
async def test_background_tiles_already_waiting_when_the_server_would_ask_are_not_asked_for() -> None:
    cache = _cache()
    await _upload_all(cache)
    world = World(ellipse(2048, 2048, 200, 200, 0.0))
    servicer = _servicer(cache, world)
    request = SegmentTilesRequest(tiles=[_tile(2, 2)], foreground=[Point(x=2048, y=2048)], foreground_mask_tile_count=0)
    early = [pb.MaskTile(coord=_coord(r, c), kind=BACKGROUND) for r in (1, 2) for c in (1, 2)]

    updates, asked = await _converse(servicer, request, [_mask_message(early)])

    _result(updates)
    assert _tiles_of(asked, "masks") == []


@pytest.mark.asyncio
async def test_a_client_that_did_not_opt_in_is_never_asked_for_mask_tiles() -> None:
    cache = _cache()
    await _upload_all(cache)
    world = World(ellipse(2048, 2048, 700, 500, 0.0))
    servicer = _servicer(cache, world)
    request = SegmentTilesRequest(tiles=[_tile(2, 2)], foreground=[Point(x=2048, y=2048)])

    updates, asked = await _converse(servicer, request)

    _result(updates)
    assert asked and _tiles_of(asked, "masks") == []


@pytest.mark.asyncio
async def test_a_soma_sized_correction_mask_completes_end_to_end() -> None:
    cache = _cache()
    await _upload_all(cache)
    mask_up = ellipse(2048, 2048, 2000, 1950, 0.0)  # about 4000 px across, 16 tiles
    world = World(ellipse(2048, 2048, 2040, 1990, 0.0))
    servicer = _servicer(cache, world)
    tiles = _tiles_from_up(mask_up, FOREGROUND)
    assert len(tiles) == 16 and any(tile.full for tile in tiles) and not all(tile.full for tile in tiles)
    request = SegmentTilesRequest(
        tiles=[_tile(2, 2)],
        foreground_feature=pb.Feature(mask_role=pb.MASK_ROLE_CORRECTION),
        foreground_mask_tile_count=len(tiles),
    )
    context = _context()

    updates, asked = await _converse(servicer, request, [_mask_message(tiles[:5]), _mask_message(tiles[5:])], context=context)

    response = _result(updates)
    assert context.abort.await_args is None
    segment = response.segments[0]
    decoded = cv2.imdecode(np.frombuffer(segment.mask, dtype=np.uint8), cv2.IMREAD_GRAYSCALE) > 0
    assert int(decoded.sum()) >= 0.999 * int(mask_up.sum()), "the correction mask is pinned in the result"
    assert world.calls, "windows around the click were predicted"


@pytest.mark.asyncio
async def test_a_correction_role_reaches_the_encoder_through_the_stream() -> None:
    cache = _cache()
    await _upload_all(cache)
    mask_up = ellipse(2048, 2048, 300, 300, 0.0)
    world = World(ellipse(2048, 2048, 320, 320, 0.0))
    servicer = _servicer(cache, world)
    servicer._prompt_encoder = EdgeConfidenceEncoder({})  # type: ignore[assignment]
    tiles = _tiles_from_up(mask_up, FOREGROUND)
    request = SegmentTilesRequest(
        tiles=[_tile(2, 2)],
        foreground_feature=pb.Feature(mask_role=pb.MASK_ROLE_CORRECTION),
        foreground_mask_tile_count=len(tiles),
    )

    updates, _asked = await _converse(servicer, request, [_mask_message(tiles)])

    _result(updates)
    first = next(prompt for prompt in world.mask_prompts if prompt is not None)
    assert float(first.max()) == pytest.approx(enc.CORRECTION_LOGIT, abs=0.2)


# ---- what the stream refuses ---------------------------------------------------------------------------------------------


async def _refused(
    request: SegmentTilesRequest,
    messages: Sequence[SegmentTilesStreamRequest],
    limits: Optional[RequestLimits] = None,
    answer_timeout: Optional[float] = None,
):
    cache = _cache()
    await _upload_all(cache)
    servicer = _servicer(cache, World(ellipse(2048, 2048, 200, 200, 0.0)))
    if limits is not None:
        servicer._limits = limits
    if answer_timeout is not None:
        servicer._tile_answer_timeout = answer_timeout
    context = _context()
    updates, _asked = await asyncio.wait_for(
        _converse(servicer, request, messages, context=context), timeout=10
    )
    return updates, context


def _opt_in(count: int, **fields) -> SegmentTilesRequest:
    return SegmentTilesRequest(
        tiles=[_tile(2, 2)], foreground=[Point(x=2048, y=2048)], foreground_mask_tile_count=count, **fields
    )


@pytest.mark.asyncio
async def test_an_announced_foreground_count_that_is_never_reached_times_out() -> None:
    updates, context = await _refused(
        _opt_in(2),
        [_mask_message([pb.MaskTile(coord=_coord(2, 2), kind=FOREGROUND, full=True)])],
        answer_timeout=0.3,
    )

    assert not updates
    assert _status(context) == grpc.StatusCode.DEADLINE_EXCEEDED


@pytest.mark.asyncio
async def test_the_walk_does_not_start_before_the_whole_foreground_has_arrived() -> None:
    cache = _cache()
    await _upload_all(cache)
    world = World(ellipse(2048, 2048, 200, 200, 0.0))
    servicer = _servicer(cache, world)
    servicer._tile_answer_timeout = 0.3
    context = _context()

    await asyncio.wait_for(
        _converse(
            servicer,
            _opt_in(2),
            [_mask_message([pb.MaskTile(coord=_coord(2, 2), kind=FOREGROUND, full=True)])],
            context=context,
        ),
        timeout=10,
    )

    assert world.calls == [], "no prediction was made with half of the foreground"


@pytest.mark.asyncio
async def test_a_duplicate_tile_is_refused() -> None:
    tile = pb.MaskTile(coord=_coord(2, 2), kind=FOREGROUND, full=True)

    in_one_message = await _refused(_opt_in(2), [_mask_message([tile, tile])])
    in_two_messages = await _refused(_opt_in(2), [_mask_message([tile]), _mask_message([tile])])
    background = pb.MaskTile(coord=_coord(3, 3), kind=BACKGROUND, full=True)
    background_twice = await _refused(_opt_in(1), [_mask_message([tile, background, background])])

    for updates, context in (in_one_message, in_two_messages, background_twice):
        assert not updates
        assert _status(context) == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "bad",
    [
        pytest.param(pb.MaskTile(coord=_coord(2, 2)), id="kind unspecified"),
        pytest.param(pb.MaskTile(coord=_coord(2, 2), kind=FOREGROUND, png=_png(np.ones((300, 300), bool))), id="wrong size"),
        pytest.param(pb.MaskTile(coord=_coord(2, 2), kind=FOREGROUND, full=True, scale=2), id="scale 2"),
        pytest.param(pb.MaskTile(coord=_coord(2, 2, downsample=2), kind=FOREGROUND, full=True), id="another downsample"),
    ],
)
async def test_a_malformed_foreground_tile_is_invalid_argument(bad) -> None:
    updates, context = await _refused(_opt_in(1), [_mask_message([bad])])

    assert not updates
    assert _status(context) == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
async def test_more_foreground_tiles_than_announced_are_invalid_argument() -> None:
    tiles = [pb.MaskTile(coord=_coord(2, c), kind=FOREGROUND, full=True) for c in (1, 2)]

    updates, context = await _refused(_opt_in(1), [_mask_message(tiles)])

    assert not updates
    assert _status(context) == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
async def test_a_foreground_count_over_the_limit_is_resource_exhausted() -> None:
    updates, context = await _refused(_opt_in(3), [], limits=RequestLimits(max_foreground_mask_tiles=2))

    assert not updates
    assert _status(context) == grpc.StatusCode.RESOURCE_EXHAUSTED


def test_the_foreground_tile_limit_defaults_to_256_and_reads_its_environment_variable(monkeypatch) -> None:
    assert RequestLimits().max_foreground_mask_tiles == 256
    monkeypatch.setenv("SEGMENTATION_MAX_FOREGROUND_MASK_TILES", "7")
    assert RequestLimits.from_env().max_foreground_mask_tiles == 7


@pytest.mark.asyncio
async def test_a_request_may_not_send_a_mask_both_ways_for_the_same_kind() -> None:
    bounds = pb.BoundingBox(x_min=2000, y_min=2000, x_max=2100, y_max=2100)
    foreground_both = _opt_in(0, foreground_feature=pb.Feature(mask=pb.Mask(bounds=bounds)))
    background_both = _opt_in(0, background_feature=pb.Feature(mask=pb.Mask(bounds=bounds)))

    for request in (foreground_both, background_both):
        updates, context = await _refused(request, [])
        assert not updates
        assert _status(context) == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
async def test_mask_tiles_from_a_request_that_did_not_opt_in_are_invalid_argument() -> None:
    cache = _cache()
    await _upload_all(cache)
    world = World(ellipse(2048, 2048, 700, 500, 0.0))
    servicer = _servicer(cache, world)
    request = SegmentTilesRequest(tiles=[_tile(2, 2)], foreground=[Point(x=2048, y=2048)])
    stray = _mask_message([pb.MaskTile(coord=_coord(2, 2), kind=BACKGROUND, full=True)])
    context = _context()

    await asyncio.wait_for(_converse(servicer, request, [stray], context=context), timeout=10)

    assert _status(context) == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
async def test_the_server_says_it_accepts_mask_tiles() -> None:
    servicer = _servicer(_cache())
    servicer.model.capabilities = pb.ModelCapabilities()

    status = await servicer.GetServerStatus(ServerStatusRequest(), AsyncMock())

    assert status.mask_tiles is True
