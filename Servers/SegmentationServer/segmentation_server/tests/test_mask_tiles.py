"""Painted bitmaps sent as tiles: a mask of any size, cut into 1024 tiles on the image-tile grid."""

from __future__ import annotations

import asyncio
from typing import List, Optional, Sequence, Set, Tuple
from unittest.mock import AsyncMock

import cv2
import grpc
import numpy as np
import pytest

import cell_world
from cell_world import World, ellipse
from protocol import (
    all_unavailable,
    cache as _cache,
    cell as _cell,
    context as _context,
    put_tile as _upload,
    result as _result,
    servicer as _servicer,
    start,
)
from segmentation_grpc import segmentation_pb2 as pb
from segmentation_server.cell_grid import CELL_SIZE, LOGIT_SCALE, TILE_SIZE, Canvas, Cell, MaskShape, core_origin
from segmentation_server.mask_tiles import MaskTileCollector, TiledMaskShape, decode_mask_tile
from segmentation_server.prompt import BackgroundPrompt
from segmentation_server.prompt_encodings import EdgeConfidenceEncoder
from segmentation_server.tile_growth import GrowthWalk

FOREGROUND = pb.MASK_KIND_FOREGROUND
BACKGROUND = pb.MASK_KIND_BACKGROUND


def _png(array_down: np.ndarray) -> bytes:
    ok, encoded = cv2.imencode(".png", array_down.astype(np.uint8) * 255)
    assert ok
    return encoded.tobytes()


def mask_tiles_for(
    truth_up: np.ndarray, x0: int, y0: int, kind: int = FOREGROUND, scale: int = 1, keep_empty: bool = False
) -> List[pb.MaskTile]:
    """What a client sends for ``truth_up`` (Y-up, low corner at mosaic ``(x0, y0)``): one MaskTile per tile touched.

    A tile that is wholly mask goes as ``full``, a tile with no mask is left out (or sent empty with ``keep_empty``),
    the rest as 1-bit PNGs.
    """
    height, width = truth_up.shape
    tiles: List[pb.MaskTile] = []
    for row in range(y0 // TILE_SIZE, (y0 + height - 1) // TILE_SIZE + 1):
        for col in range(x0 // TILE_SIZE, (x0 + width - 1) // TILE_SIZE + 1):
            tiles.append(_one_tile(truth_up, x0, y0, row, col, kind, scale))
    return tiles if keep_empty else [t for t in tiles if t.full or t.png]


def _one_tile(truth_up: np.ndarray, x0: int, y0: int, row: int, col: int, kind: int, scale: int = 1) -> pb.MaskTile:
    height, width = truth_up.shape
    tile_up = np.zeros((TILE_SIZE, TILE_SIZE), dtype=bool)
    ya, yb = max(y0, row * TILE_SIZE), min(y0 + height, (row + 1) * TILE_SIZE)
    xa, xb = max(x0, col * TILE_SIZE), min(x0 + width, (col + 1) * TILE_SIZE)
    if ya < yb and xa < xb:
        tile_up[ya - row * TILE_SIZE:yb - row * TILE_SIZE, xa - col * TILE_SIZE:xb - col * TILE_SIZE] = (
            truth_up[ya - y0:yb - y0, xa - x0:xb - x0]
        )
    cell = _cell(row, col)
    if not tile_up.any():
        return pb.MaskTile(cell=cell, kind=kind)
    if tile_up.all():
        return pb.MaskTile(cell=cell, kind=kind, full=True)
    tile_down = np.flipud(tile_up)
    if scale == LOGIT_SCALE:
        tile_down = tile_down.reshape(TILE_SIZE // 4, 4, TILE_SIZE // 4, 4).any(axis=(1, 3))
    return pb.MaskTile(cell=cell, kind=kind, png=_png(tile_down), scale=scale)


def collector_for(tiles: Sequence[pb.MaskTile], announced: Optional[int] = None) -> MaskTileCollector:
    collector = MaskTileCollector(len(tiles) if announced is None else announced)
    collector.add(pb.MaskTiles(tiles=list(tiles)))
    return collector


def shape_from(tiles: Sequence[pb.MaskTile]) -> TiledMaskShape:
    shape = collector_for(tiles).foreground_shape()
    assert shape is not None
    return shape


def background_from(tiles: Sequence[pb.MaskTile]) -> TiledMaskShape:
    """A complete background bitmap holding exactly ``tiles``."""
    shape = TiledMaskShape()
    for tile in tiles:
        shape.settle((tile.cell.row, tile.cell.col), decode_mask_tile(tile))
    shape.mark_complete()
    return shape


def disc_truth(cx: int, cy: int, radius: int):
    ys, xs = np.mgrid[-radius:radius + 1, -radius:radius + 1]
    return xs**2 + ys**2 <= radius**2, cx - radius, cy - radius


def single_array_shape(truth_up: np.ndarray, x0: int, y0: int) -> MaskShape:
    height, width = truth_up.shape
    return MaskShape((x0, y0, x0 + width - 1, y0 + height - 1), truth_up)


def cells_over(shape: MaskShape):
    x0, y0, x1, y1 = shape.box
    return [Cell(row, col) for row in range(y0 // 512 - 2, y1 // 512 + 2) for col in range(x0 // 512 - 2, x1 // 512 + 2)]


def window_value(prompt: np.ndarray, cell: Cell, x: int, y: int) -> float:
    wx, wy = cell.col * 512, cell.row * 512
    return float(prompt[(wy + CELL_SIZE - 1 - y) // LOGIT_SCALE, (x - wx) // LOGIT_SCALE])


# ---- the shape ---------------------------------------------------------------------------------------------------

def test_a_seed_covering_3x3_tiles_gives_every_start_window_the_prompt_a_single_array_gives() -> None:
    truth, x0, y0 = disc_truth(2560, 2560, 1400)  # 1160..3960: tiles 1..3 in both axes
    tiles = mask_tiles_for(truth, x0, y0)
    assert {(t.cell.row, t.cell.col) for t in tiles} == {(r, c) for r in (1, 2, 3) for c in (1, 2, 3)}
    assert any(t.full for t in tiles) and any(t.png for t in tiles)
    tiled, single = shape_from(tiles), single_array_shape(truth, x0, y0)
    assert tiled.box == single.box
    encoder = EdgeConfidenceEncoder({})

    seen = 0
    for cell in cells_over(single):
        full_a, full_b = single.window_full(cell), tiled.window_full(cell)
        assert (full_a is None) == (full_b is None)
        if full_a is None:
            continue
        seen += 1
        assert np.array_equal(full_a, full_b)
        assert np.array_equal(single.window_samples(cell), tiled.window_samples(cell))
        assert single.covers_core(cell) == tiled.covers_core(cell)
        assert np.array_equal(encoder.start_window(cell, single, []), encoder.start_window(cell, tiled, []))
    assert seen >= 9


def test_tiled_and_single_array_masks_pin_and_clear_the_same_pixels_in_a_canvas() -> None:
    truth, x0, y0 = disc_truth(2560, 2560, 1400)
    tiled, single = shape_from(mask_tiles_for(truth, x0, y0)), single_array_shape(truth, x0, y0)
    canvas_a, canvas_b = Canvas(), Canvas()

    added_a, added_b = canvas_a.assume_mask(single), canvas_b.assume_mask(tiled)

    assert added_a == added_b
    for cell in added_a:
        assert np.array_equal(canvas_a.decided_core(cell), canvas_b.decided_core(cell))
    canvas_a.deny_mask(single)
    canvas_b.deny_mask(tiled)
    assert all(not canvas_a.decided_core(cell).any() for cell in added_a)
    assert all(not canvas_b.decided_core(cell).any() for cell in added_b)


def test_the_interior_point_is_on_the_mask_even_for_a_ring_spanning_tiles() -> None:
    ys, xs = np.mgrid[-1500:1501, -1500:1501]
    ring = (xs**2 + ys**2 <= 1500**2) & (xs**2 + ys**2 >= 1300**2)
    tiled = shape_from(mask_tiles_for(ring, 2048 - 1500, 2048 - 1500))

    x, y = tiled.interior_point()

    assert tiled.region(x, y, x, y)[0, 0], "the click lands on the ring, not in its hole"


def test_a_wholly_covered_core_is_taken_whole_and_a_mask_with_no_pixel_is_none() -> None:
    full = TiledMaskShape()
    for row in (1, 2):
        for col in (1, 2):
            full.settle((row, col), True)
    cell = Cell(row=3, col=3)  # core 1792..2303 in both axes: wholly inside tiles 1 and 2
    assert core_origin(cell) == (1792, 1792) and full.covers_core(cell)
    assert not full.covers_core(Cell(row=1, col=1))
    empty = collector_for([pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND)]).foreground_shape()
    assert empty is None


def test_full_tiles_and_png_tiles_give_the_same_result() -> None:
    square = np.ones((2048, 2048), dtype=bool)  # tiles (1,1)..(2,2), each wholly mask
    full_tiles = mask_tiles_for(square, 1024, 1024)
    assert all(t.full for t in full_tiles)
    as_full = shape_from(full_tiles)
    as_png = shape_from([
        pb.MaskTile(cell=t.cell, kind=FOREGROUND, png=_png(np.ones((TILE_SIZE, TILE_SIZE), dtype=bool)))
        for t in full_tiles
    ])
    encoder = EdgeConfidenceEncoder({})

    for cell in cells_over(as_full):
        a, b = encoder.start_window(cell, as_full, []), encoder.start_window(cell, as_png, [])
        assert (a is None) == (b is None)
        if a is not None:
            assert np.array_equal(a, b)


def test_scale_4_tiles_match_full_resolution_within_one_sample_at_the_edge() -> None:
    truth, x0, y0 = disc_truth(2560, 2560, 1400)
    full_res = shape_from(mask_tiles_for(truth, x0, y0))
    quarter = shape_from(mask_tiles_for(truth, x0, y0, scale=LOGIT_SCALE))
    grow = cv2.getStructuringElement(cv2.MORPH_RECT, (2 * (LOGIT_SCALE - 1) + 1, 2 * (LOGIT_SCALE - 1) + 1))

    compared = 0
    for cell in cells_over(full_res):
        a, b = full_res.window_full(cell), quarter.window_full(cell)
        assert (a is None) == (b is None)
        if a is None:
            continue
        compared += 1
        assert not (a & ~b).any(), "scale 4 never loses a mask pixel"
        assert not (b & ~cv2.dilate(a.astype(np.uint8), grow).astype(bool)).any(), "and adds at most 3 px at the edge"
        assert np.array_equal(full_res.window_samples(cell), quarter.window_samples(cell)), "SAM2's own grid is unchanged"
    assert compared >= 9


def test_foreground_and_background_tiles_overlapping_give_zero_on_the_overlap() -> None:
    seed, sx, sy = disc_truth(2560, 2560, 150)
    foreground = shape_from(mask_tiles_for(seed, sx, sy))
    neighbour = np.ones((301, 201), dtype=bool)  # x 2600..2800, y 2400..2700: overlaps the seed's right side
    background = background_from(mask_tiles_for(neighbour, 2600, 2400, kind=BACKGROUND))
    cell = Cell(row=4, col=4)

    prompt = EdgeConfidenceEncoder({}).start_window(cell, foreground, [background])
    single = EdgeConfidenceEncoder({}).start_window(
        cell, single_array_shape(seed, sx, sy), [MaskShape((2600, 2400, 2800, 2700))]
    )

    assert window_value(prompt, cell, 2740, 2550) < -5.0, "background only"
    assert window_value(prompt, cell, 2640, 2560) == 0.0, "in both: unknown"
    assert window_value(prompt, cell, 2480, 2560) > 5.0, "foreground only"
    assert np.array_equal(prompt, single)


def test_a_mask_that_is_not_complete_only_knows_the_tiles_it_was_told_about() -> None:
    background = TiledMaskShape()
    cell = Cell(row=1, col=1)  # window 512..1535: tiles (0,0) (0,1) (1,0) (1,1)
    assert {(t.row, t.col) for t in background.unknown_tiles(cell)} == {(0, 0), (0, 1), (1, 0), (1, 1)}

    background.settle((0, 0), None)  # "no mask here" is knowledge too
    background.settle((1, 1), True)

    assert {(t.row, t.col) for t in background.unknown_tiles(cell)} == {(0, 1), (1, 0)}
    assert not background.empty and background.box == (1024, 1024, 2047, 2047)
    background.mark_complete()
    assert background.unknown_tiles(cell) == []


# ---- the wire: decoding and validation ---------------------------------------------------------------------------

def _add(collector: MaskTileCollector, *tiles: pb.MaskTile) -> None:
    collector.add(pb.MaskTiles(tiles=list(tiles)))


def test_a_duplicate_cell_and_kind_is_refused_but_the_same_cell_in_each_kind_is_not() -> None:
    fg = pb.MaskTile(cell=_cell(1, 1), kind=FOREGROUND, full=True)
    bg = pb.MaskTile(cell=_cell(1, 1), kind=BACKGROUND, full=True)
    _add(MaskTileCollector(2), fg, bg)
    with pytest.raises(ValueError, match="twice"):
        _add(MaskTileCollector(2), fg, fg)
    collector = MaskTileCollector(2)
    _add(collector, bg)
    with pytest.raises(ValueError, match="twice"):
        _add(collector, bg)


@pytest.mark.parametrize("bad", [
    pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND, png=_png(np.ones((512, 512), bool))),
    pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND, png=_png(np.ones((256, 256), bool))),
    pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND, png=_png(np.ones((1024, 1024), bool)), scale=4),
    pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND, png=_png(np.ones((1024, 1024), bool)), scale=2),
    pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND, png=b"not a png"),
    pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND, full=True, png=_png(np.ones((1024, 1024), bool))),
])
def test_a_tile_with_the_wrong_png_size_scale_or_both_full_and_png_is_refused(bad) -> None:
    with pytest.raises(ValueError):
        _add(MaskTileCollector(1), bad)


def test_an_unset_kind_or_more_tiles_than_announced_is_refused() -> None:
    with pytest.raises(ValueError, match="no kind"):
        _add(MaskTileCollector(1), pb.MaskTile(cell=_cell(0, 0), full=True))
    with pytest.raises(ValueError, match="announced"):
        _add(MaskTileCollector(1), pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND, full=True),
             pb.MaskTile(cell=_cell(0, 1), kind=BACKGROUND, full=True))


def test_the_count_covers_both_kinds_and_both_bitmaps_are_complete_once_it_is_reached() -> None:
    collector = MaskTileCollector(2)
    assert not collector.done
    _add(collector, pb.MaskTile(cell=_cell(0, 0), kind=BACKGROUND), pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND, full=True))

    assert collector.done
    assert collector.background_shape() is None, "an explicitly empty tile holds no pixel"
    foreground = collector.foreground_shape()
    assert foreground is not None and foreground.unknown_tiles(Cell(0, 1)) == [], "a tile not sent is empty"


def test_the_collector_is_done_at_once_for_a_zero_count() -> None:
    assert MaskTileCollector(0).done and not MaskTileCollector(3).done


# ---- on the stream -----------------------------------------------------------------------------------------------

async def _stream(servicer, request: pb.SegmentTilesRequest, after_start: Sequence[pb.MaskTiles] = (), context=None,
                  close: bool = False) -> List[pb.SegmentTilesStreamResponse]:
    """Send ``start`` then ``after_start``; answer each ``needed`` with every tile unavailable. Open unless ``close``."""
    inbox: asyncio.Queue = asyncio.Queue()
    await inbox.put(pb.SegmentTilesStreamRequest(start=request))
    for message in after_start:
        await inbox.put(pb.SegmentTilesStreamRequest(mask_tiles=message))
    if close:
        await inbox.put(None)

    async def incoming():
        while True:
            item = await inbox.get()
            if item is None:
                return
            yield item

    updates = []
    async for update in servicer.SegmentTilesStream(incoming(), context or _context()):
        updates.append(update)
        if update.WhichOneof("update") == "needed":
            await inbox.put(pb.SegmentTilesStreamRequest(answer=all_unavailable(update.needed)))
    return updates


def _aborted_with(context: AsyncMock) -> Optional[grpc.StatusCode]:
    return context.abort.await_args.args[0] if context.abort.await_args else None


def _announce(count: int, **fields) -> pb.SegmentTilesRequest:
    return start(mask_tile_count=count, **fields)


def _fg(row: int, col: int) -> pb.MaskTiles:
    return pb.MaskTiles(tiles=[pb.MaskTile(cell=_cell(row, col), kind=FOREGROUND, full=True)])


@pytest.mark.asyncio
async def test_an_announced_count_that_is_never_completed_times_out() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    servicer._tile_answer_timeout = 0.2
    context = _context()

    updates = await asyncio.wait_for(_stream(servicer, _announce(2), [_fg(0, 0)], context), timeout=10)

    assert updates == [] and _aborted_with(context) == grpc.StatusCode.DEADLINE_EXCEEDED
    assert "1 of 2" in context.abort.await_args.args[1]


@pytest.mark.asyncio
async def test_a_duplicate_tile_more_tiles_than_announced_and_a_stream_that_ends_early_are_refused() -> None:
    tile = pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND, full=True)
    cases = [
        (_announce(2), [pb.MaskTiles(tiles=[tile, tile])], False),
        (_announce(1), [pb.MaskTiles(tiles=[tile, pb.MaskTile(cell=_cell(0, 1), kind=FOREGROUND, full=True)])], False),
        (_announce(2), [pb.MaskTiles(tiles=[tile])], True),
        (_announce(1), [pb.MaskTiles(tiles=[pb.MaskTile(cell=_cell(0, 0), kind=FOREGROUND, png=b"junk")])], False),
    ]
    for request, messages, close in cases:
        cache = _cache()
        await _upload(cache, 0, 0)
        context = _context()
        updates = await asyncio.wait_for(_stream(_servicer(cache), request, messages, context, close=close), timeout=10)
        assert updates == [] and _aborted_with(context) == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
async def test_more_mask_tiles_than_the_limit_are_refused_before_any_is_read() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()

    updates = await asyncio.wait_for(_stream(servicer, _announce(servicer._limits.max_mask_tiles + 1), [], context), timeout=10)

    assert updates == [] and _aborted_with(context) == grpc.StatusCode.RESOURCE_EXHAUSTED
    assert "SEGMENTATION_MAX_MASK_TILES" in context.abort.await_args.args[1]


@pytest.mark.asyncio
async def test_a_mask_tile_after_the_announced_count_is_a_protocol_error() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache, mask=np.ones((4, 4), dtype=np.bool_))
    context = _context()

    # The count is reached with the first message; the second arrives while the server waits for an answer.
    await asyncio.wait_for(_stream(servicer, _announce(1, points=[(10, 10)]), [_fg(0, 0), _fg(5, 5)], context), timeout=30)

    assert _aborted_with(context) == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
async def test_a_bitmap_only_request_starts_from_a_pixel_of_the_bitmap() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()
    seed, x0, y0 = disc_truth(512, 512, 100)

    updates = await asyncio.wait_for(
        _stream(servicer, _announce(1), [pb.MaskTiles(tiles=mask_tiles_for(seed, x0, y0))], context), timeout=30
    )

    context.abort.assert_not_awaited()
    assert updates[-1].WhichOneof("update") == "result"
    points = servicer.model.predict_tile.call_args.args[1]
    assert len(points) == 1 and abs(points[0][0] - 512) <= 1 and abs(points[0][1] - (1023 - 512)) <= 1


def _clicked(**fields) -> pb.SegmentTilesRequest:
    """A click-only request inside tile (0, 0): its model answers a 256 x 256 block of window (0, 0)."""
    return start([(300, 600)], **fields)


def _result_pixels(updates: List[pb.SegmentTilesStreamResponse]) -> int:
    """Pixels above the default threshold in every segment, at the logit grid's resolution."""
    response = _result(updates)
    total = 0
    for segment in response.segments:
        logits = np.frombuffer(segment.logits, dtype="<f2").reshape(segment.logits_size.height, segment.logits_size.width)
        total += int(np.count_nonzero(logits > 1.0)) * int(segment.logits_scale) ** 2
    return total


@pytest.mark.asyncio
async def test_a_background_bitmap_sent_with_start_clears_the_neighbours_pixels() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)  # its model answers a 256 x 256 block in the window: x 256..511, y 512..767
    plain = _result_pixels(await asyncio.wait_for(_stream(servicer, _clicked(), [], _context()), timeout=30))
    assert plain == 256 * 256

    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    neighbour = np.ones((TILE_SIZE, 128), dtype=bool)  # x 256..383 over the whole tile: the left half of the answer
    bitmap = pb.MaskTiles(tiles=[_one_tile(neighbour, 256, 0, 0, 0, BACKGROUND)])
    context = _context()

    updates = await asyncio.wait_for(_stream(servicer, _clicked(mask_tile_count=1), [bitmap], context), timeout=30)

    context.abort.assert_not_awaited()
    asked = {(c.row, c.col) for u in updates if u.WhichOneof("update") == "needed" for c in u.needed.background_cells}
    assert (0, 0) not in asked, "the request's own tile is never asked about"
    assert _result_pixels(updates) == 256 * 128, "the neighbour's pixels are cleared from the result"


# ---- a soma-sized mask, end to end through the walk with a fake predictor ----------------------------------------

def test_a_soma_sized_bitmap_about_4000_px_across_completes_with_every_mask_pixel_returned(monkeypatch) -> None:
    monkeypatch.setattr(cell_world, "DOMAIN", 6144)
    radius = 1950
    cx = cy = 3072
    yy, xx = np.mgrid[-radius:radius + 1, -radius:radius + 1]
    painted = xx**2 + yy**2 <= (radius - 40) ** 2  # the user's bitmap is a little smaller than the truth
    tiles = mask_tiles_for(painted, cx - radius, cy - radius, scale=LOGIT_SCALE)
    assert len(tiles) >= 12  # spans the 4x4 grid apart from perhaps its corners
    shape = shape_from(tiles)
    assert shape.box[2] - shape.box[0] > 3800
    background = BackgroundPrompt(covered=[(2, 2)])

    world = World(ellipse(cx, cy, radius, radius, 0.0))
    walk = GrowthWalk(
        [shape.interior_point()], [], world.predict, masks=[shape],
        background_masks=background.regions, background_coverage=background.unknown_tiles,
    )
    asked: Set[Tuple[int, int]] = set()
    while True:
        walk.advance()
        wanted = walk.take_requested_background()
        if not wanted:
            break
        for tile in wanted:
            assert (tile.row, tile.col) not in asked, "a cell's background is asked for once"
            asked.add((tile.row, tile.col))
        background.cover(asked)  # no neighbours anywhere
        walk.resume()
    answer = world.to_truth_frame(walk.result())

    covered = shape.region(*shape.box)
    x0, y0, x1, y1 = shape.box
    assert answer[y0:y1 + 1, x0:x1 + 1][covered].all(), "every pixel of the mask is object"
    assert answer[cy, cx] and asked, "and the walk asked for background as it entered windows"


# ---- a budget that stops the walk ----------------------------------------------------------------------------------

def test_a_walk_stopped_by_max_cells_logs_the_mask_boundary_cores_it_never_predicted(caplog) -> None:
    import logging

    radius = 700
    painted, x0, y0 = disc_truth(2048, 2048, radius)
    shape = shape_from(mask_tiles_for(painted, x0, y0))
    background = background_from([])
    world = World(ellipse(2048, 2048, radius + 150, radius + 150, 0.0))
    walk = GrowthWalk([shape.interior_point()], [], world.predict, masks=[shape], background_masks=[background], max_cells=2)

    with caplog.at_level(logging.WARNING, logger="segmentation_server.tile_growth"):
        walk.advance()
        walk.result()

    assert any("mask-boundary cores unpredicted" in record.getMessage() and "max_cells=2" in record.getMessage()
               for record in caplog.records)
