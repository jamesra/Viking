"""Mask prompts sent as tiles: a mask of any size, cut into 1024 tiles on the image-tile grid."""

from __future__ import annotations

import asyncio
from typing import Callable, Dict, List, Optional, Sequence, Set, Tuple
from unittest.mock import AsyncMock

import cv2
import grpc
import numpy as np
import pytest

import cell_world
from cell_world import World, ellipse
from segmentation_grpc import segmentation_pb2 as pb
from segmentation_server.cell_grid import CELL_SIZE, LOGIT_SCALE, TILE_SIZE, Canvas, Cell, MaskShape, core_origin
from segmentation_server.mask_tiles import MaskTileCollector, TiledMaskShape
from segmentation_server.prompt_encodings import EdgeConfidenceEncoder
from segmentation_server.server import _prompts_from, _same_tile_identity
from segmentation_server.tile_growth import GrowthWalk
from test_segment_tiles import _cache, _context, _servicer, _tile, _upload

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
    coord = _tile(row, col)
    if not tile_up.any():
        return pb.MaskTile(coord=coord, kind=kind)
    if tile_up.all():
        return pb.MaskTile(coord=coord, kind=kind, full=True)
    tile_down = np.flipud(tile_up)
    if scale == LOGIT_SCALE:
        tile_down = tile_down.reshape(TILE_SIZE // 4, 4, TILE_SIZE // 4, 4).any(axis=(1, 3))
    return pb.MaskTile(coord=coord, kind=kind, png=_png(tile_down), scale=scale)


def collector_for(tiles: Sequence[pb.MaskTile], announced: Optional[int] = None) -> MaskTileCollector:
    foreground = [t for t in tiles if t.kind == FOREGROUND]
    collector = MaskTileCollector(len(foreground) if announced is None else announced)
    collector.add(pb.MaskTiles(tiles=list(tiles)), _tile(0, 0), _same_tile_identity)
    return collector


def shape_from(tiles: Sequence[pb.MaskTile], role: str = "seed") -> TiledMaskShape:
    shape = collector_for(tiles).foreground_shape()
    assert shape is not None
    return shape.with_role(role)


def background_from(tiles: Sequence[pb.MaskTile]) -> TiledMaskShape:
    """A background mask holding exactly ``tiles`` (as if the client had answered for them), and complete."""
    shape = TiledMaskShape()
    from segmentation_server.mask_tiles import decode_mask_tile

    for tile in tiles:
        shape.settle((tile.coord.row, tile.coord.col), decode_mask_tile(tile))
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
    assert {(t.coord.row, t.coord.col) for t in tiles} == {(r, c) for r in (1, 2, 3) for c in (1, 2, 3)}
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
    empty = collector_for([pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND)]).foreground_shape()
    assert empty is None


def test_full_tiles_and_png_tiles_give_the_same_result() -> None:
    square = np.ones((2048, 2048), dtype=bool)  # tiles (1,1)..(2,2), each wholly mask
    full_tiles = mask_tiles_for(square, 1024, 1024)
    assert all(t.full for t in full_tiles)
    as_full = shape_from(full_tiles)
    as_png = shape_from([
        pb.MaskTile(coord=t.coord, kind=FOREGROUND, png=_png(np.ones((TILE_SIZE, TILE_SIZE), dtype=bool)))
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


def test_a_background_mask_only_knows_the_tiles_it_was_told_about() -> None:
    background = TiledMaskShape()
    cell = Cell(row=1, col=1)  # window 512..1535: tiles (0,0) (0,1) (1,0) (1,1)
    assert {(t.row, t.col) for t in background.unknown_tiles(cell)} == {(0, 0), (0, 1), (1, 0), (1, 1)}

    background.settle((0, 0), None)  # "no neighbors here" is knowledge too
    background.settle((1, 1), True)

    assert {(t.row, t.col) for t in background.unknown_tiles(cell)} == {(0, 1), (1, 0)}
    assert not background.empty and background.box == (1024, 1024, 2047, 2047)
    background.mark_complete()
    assert background.unknown_tiles(cell) == []


# ---- the wire: decoding and validation ---------------------------------------------------------------------------

def _add(collector: MaskTileCollector, *tiles: pb.MaskTile) -> None:
    collector.add(pb.MaskTiles(tiles=list(tiles)), _tile(0, 0), _same_tile_identity)


def test_a_duplicate_coord_and_kind_is_refused_but_the_same_coord_in_each_kind_is_not() -> None:
    fg = pb.MaskTile(coord=_tile(1, 1), kind=FOREGROUND, full=True)
    bg = pb.MaskTile(coord=_tile(1, 1), kind=BACKGROUND, full=True)
    _add(MaskTileCollector(2), fg, bg)
    with pytest.raises(ValueError, match="twice"):
        _add(MaskTileCollector(2), fg, fg)
    collector = MaskTileCollector(1)
    _add(collector, bg)
    with pytest.raises(ValueError, match="twice"):
        _add(collector, bg)


@pytest.mark.parametrize("bad", [
    pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND, png=_png(np.ones((512, 512), bool))),
    pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND, png=_png(np.ones((256, 256), bool))),
    pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND, png=_png(np.ones((1024, 1024), bool)), scale=4),
    pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND, png=_png(np.ones((1024, 1024), bool)), scale=2),
    pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND, png=b"not a png"),
    pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND, full=True, png=_png(np.ones((1024, 1024), bool))),
])
def test_a_tile_with_the_wrong_png_size_scale_or_both_full_and_png_is_refused(bad) -> None:
    with pytest.raises(ValueError):
        _add(MaskTileCollector(1), bad)


def test_a_tile_at_another_downsample_an_unset_kind_or_too_many_foreground_tiles_is_refused() -> None:
    other = _tile(0, 0)
    other.downsample = 2
    with pytest.raises(ValueError, match="same volume"):
        _add(MaskTileCollector(1), pb.MaskTile(coord=other, kind=FOREGROUND, full=True))
    with pytest.raises(ValueError, match="no kind"):
        _add(MaskTileCollector(1), pb.MaskTile(coord=_tile(0, 0), full=True))
    with pytest.raises(ValueError, match="announced"):
        _add(MaskTileCollector(1), pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND, full=True),
             pb.MaskTile(coord=_tile(0, 1), kind=FOREGROUND, full=True))


def test_an_explicitly_empty_tile_is_known_but_has_no_pixels() -> None:
    collector = MaskTileCollector(1)
    _add(collector, pb.MaskTile(coord=_tile(0, 0), kind=BACKGROUND), pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND, full=True))
    assert collector.background.is_known((0, 0)) and collector.background.empty
    assert collector.background.unknown_tiles(Cell(0, 0)) == [], "cell (0, 0)'s window is tile (0, 0) alone"
    assert not collector.background.is_known((0, 1))
    assert [(t.row, t.col) for t in collector.background.unknown_tiles(Cell(0, 1))] == [(0, 1)]  # tiles (0,0) and (0,1)


def test_the_foreground_role_stays_in_the_feature_and_the_click_comes_from_the_tiles() -> None:
    collector = collector_for([pb.MaskTile(coord=_tile(1, 2), kind=FOREGROUND, full=True)])
    request = pb.SegmentTilesRequest(foreground_feature=pb.Feature(mask_role=pb.MASK_ROLE_CORRECTION))

    prompts = _prompts_from(request, collector)

    assert prompts.masks[0].role == "correction"
    assert prompts.foreground == [(2 * 1024 + 512, 1024 + 512)]


# ---- on the stream -----------------------------------------------------------------------------------------------

class Client:
    """A scripted client: what it sends after ``start``, and how it answers ``needed``."""

    def __init__(self, background_truth: Optional[Tuple[np.ndarray, int, int]] = None, answer_masks: bool = True,
                 unavailable_masks: bool = False, answer_images: bool = True) -> None:
        self.background_truth = background_truth
        self.answer_masks = answer_masks
        self.unavailable_masks = unavailable_masks
        self.answer_images = answer_images
        self.asked_masks: List[Tuple[int, int]] = []
        self.asked_images: List[Tuple[int, int]] = []

    def replies(self, needed: pb.TilesNeeded) -> List[pb.SegmentTilesStreamRequest]:
        self.asked_masks += [(c.row, c.col) for c in needed.masks]
        self.asked_images += [(c.row, c.col) for c in needed.tiles]
        out: List[pb.SegmentTilesStreamRequest] = []
        answer = pb.TilesAnswer(unavailable=list(needed.tiles) if self.answer_images else [])
        if self.answer_masks and needed.masks:
            if self.unavailable_masks:
                answer.masks_unavailable.extend(needed.masks)
            else:
                tiles = []
                for coord in needed.masks:
                    if self.background_truth is None:
                        tiles.append(pb.MaskTile(coord=coord, kind=BACKGROUND))
                    else:
                        truth, x0, y0 = self.background_truth
                        tiles.append(_one_tile(truth, x0, y0, coord.row, coord.col, BACKGROUND))
                out.append(pb.SegmentTilesStreamRequest(mask_tiles=pb.MaskTiles(tiles=tiles)))
        if self.answer_images or needed.masks:
            out.append(pb.SegmentTilesStreamRequest(answer=answer))
        return out


async def _stream(servicer, request: pb.SegmentTilesRequest, after_start: Sequence[pb.MaskTiles] = (), context=None,
                  client: Optional[Client] = None, close: bool = False) -> List[pb.SegmentTilesStreamResponse]:
    """Send ``start`` then ``after_start``; answer each ``needed`` as ``client`` does. The call stays open unless ``close``."""
    client = client or Client()
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
            for reply in client.replies(update.needed):
                await inbox.put(reply)
    return updates


def _aborted_with(context: AsyncMock) -> Optional[grpc.StatusCode]:
    return context.abort.await_args.args[0] if context.abort.await_args else None


def _announce(count: int, **fields) -> pb.SegmentTilesRequest:
    return pb.SegmentTilesRequest(tiles=[_tile(0, 0)], foreground_mask_tile_count=count, **fields)


def _fg(row: int, col: int) -> pb.MaskTiles:
    return pb.MaskTiles(tiles=[pb.MaskTile(coord=_tile(row, col), kind=FOREGROUND, full=True)])


@pytest.mark.asyncio
async def test_an_announced_foreground_count_that_is_never_completed_times_out() -> None:
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
    tile = pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND, full=True)
    cases = [
        (_announce(2), [pb.MaskTiles(tiles=[tile, tile])], False),
        (_announce(1), [pb.MaskTiles(tiles=[tile, pb.MaskTile(coord=_tile(0, 1), kind=FOREGROUND, full=True)])], False),
        (_announce(2), [pb.MaskTiles(tiles=[tile])], True),
        (_announce(1), [pb.MaskTiles(tiles=[pb.MaskTile(coord=_tile(0, 0), kind=FOREGROUND, png=b"junk")])], False),
    ]
    for request, messages, close in cases:
        cache = _cache()
        await _upload(cache, 0, 0)
        context = _context()
        updates = await asyncio.wait_for(_stream(_servicer(cache), request, messages, context, close=close), timeout=10)
        assert updates == [] and _aborted_with(context) == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
async def test_more_foreground_tiles_than_the_limit_are_refused_before_any_is_read() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()

    updates = await asyncio.wait_for(_stream(servicer, _announce(servicer._limits.max_mask_tiles + 1), [], context), timeout=10)

    assert updates == [] and _aborted_with(context) == grpc.StatusCode.RESOURCE_EXHAUSTED
    assert "SEGMENTATION_MAX_MASK_TILES" in context.abort.await_args.args[1]


@pytest.mark.asyncio
async def test_the_server_asks_for_the_background_of_a_window_even_when_its_image_is_cached() -> None:
    """The start window's image is uploaded and cached, yet its background mask tile is still asked for, once."""
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()
    client = Client()

    updates = await asyncio.wait_for(_stream(servicer, _announce(1), [_fg(0, 0)], context, client), timeout=30)

    context.abort.assert_not_awaited()
    assert updates[-1].WhichOneof("update") == "result"
    first = next(u.needed for u in updates if u.WhichOneof("update") == "needed")
    assert (0, 0) in {(c.row, c.col) for c in first.masks}, "the cached tile's background mask is asked for"
    assert (0, 0) not in {(c.row, c.col) for c in first.tiles}, "and its cached image is not"
    assert {(c.row, c.col) for c in first.tiles} <= {(c.row, c.col) for c in first.masks}, "other windows' images come in the same round"
    assert len(client.asked_masks) == len(set(client.asked_masks)), "a tile is asked for once"


@pytest.mark.asyncio
async def test_a_neighbour_spanning_20_tiles_is_asked_for_only_where_the_walk_enters() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    servicer.model.predict_tile.return_value = (np.ones((4, 4), dtype=np.bool_), None, 0.8)
    neighbour = np.ones((4 * TILE_SIZE, 5 * TILE_SIZE), dtype=bool)  # tiles rows 0..3, cols 0..4: 20 tiles
    client = Client(background_truth=(neighbour, 0, 0))

    updates = await asyncio.wait_for(
        _stream(servicer, _announce(1), [_fg(0, 0)], _context(), client), timeout=30
    )

    assert updates[-1].WhichOneof("update") == "result"
    region = {(r, c) for r in range(4) for c in range(5)}
    assert (0, 0) in client.asked_masks
    assert len(set(client.asked_masks) & region) < 20, "the walk did not enter every tile the neighbour spans"
    assert len(client.asked_masks) == len(set(client.asked_masks))


@pytest.mark.asyncio
async def test_background_tiles_sent_early_are_not_asked_for_again() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    client = Client()
    early = pb.MaskTiles(tiles=[pb.MaskTile(coord=_tile(0, 0), kind=BACKGROUND)])

    updates = await asyncio.wait_for(_stream(servicer, _announce(1), [_fg(0, 0), early], _context(), client), timeout=30)

    assert updates[-1].WhichOneof("update") == "result"
    assert (0, 0) not in client.asked_masks


@pytest.mark.asyncio
async def test_a_mask_request_nobody_answers_times_out() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    servicer._tile_answer_timeout = 0.3
    context = _context()

    updates = await asyncio.wait_for(
        _stream(servicer, _announce(1), [_fg(0, 0)], context, Client(answer_masks=False, answer_images=False)), timeout=10
    )

    assert _aborted_with(context) == grpc.StatusCode.DEADLINE_EXCEEDED
    assert updates and updates[-1].WhichOneof("update") == "needed"


@pytest.mark.asyncio
async def test_background_tiles_the_client_cannot_supply_are_treated_as_having_no_neighbours() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()

    updates = await asyncio.wait_for(
        _stream(servicer, _announce(1), [_fg(0, 0)], context, Client(unavailable_masks=True)), timeout=30
    )

    context.abort.assert_not_awaited()
    assert updates[-1].WhichOneof("update") == "result"


@pytest.mark.asyncio
async def test_a_foreground_tile_after_the_announced_count_is_a_protocol_error() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()
    late = Client()
    original = late.replies

    def with_extra(needed):
        return [pb.SegmentTilesStreamRequest(mask_tiles=_fg(5, 5))] + original(needed)

    late.replies = with_extra  # type: ignore[method-assign]

    await asyncio.wait_for(_stream(servicer, _announce(1), [_fg(0, 0)], context, late), timeout=30)

    assert _aborted_with(context) == grpc.StatusCode.INVALID_ARGUMENT


# ---- a soma-sized mask, end to end through the walk with a fake predictor ----------------------------------------

def test_a_soma_sized_correction_mask_about_4000_px_across_completes_with_every_mask_pixel_returned(monkeypatch) -> None:
    monkeypatch.setattr(cell_world, "DOMAIN", 6144)
    radius = 1950
    cx = cy = 3072
    yy, xx = np.mgrid[-radius:radius + 1, -radius:radius + 1]
    corrected = xx**2 + yy**2 <= (radius - 40) ** 2  # the user's correction is a little smaller than the truth
    tiles = mask_tiles_for(corrected, cx - radius, cy - radius, scale=LOGIT_SCALE)
    assert len(tiles) >= 12  # spans the 4x4 grid apart from perhaps its corners
    collector = collector_for(tiles)
    prompts = _prompts_from(
        pb.SegmentTilesRequest(foreground_feature=pb.Feature(mask_role=pb.MASK_ROLE_CORRECTION)), collector
    )
    shape = prompts.masks[0]
    assert shape.role == "correction" and shape.box[2] - shape.box[0] > 3800
    background = prompts.background_masks[0]

    world = World(ellipse(cx, cy, radius, radius, 0.0))
    walk = GrowthWalk(prompts.foreground, [], world.predict, masks=[shape], background_masks=[background])
    asked: Set[Tuple[int, int]] = set()
    while True:
        walk.advance()
        wanted = walk.take_requested_masks()
        if not wanted:
            break
        for tile in wanted:
            assert (tile.row, tile.col) not in asked, "a background tile is asked for once"
            asked.add((tile.row, tile.col))
            background.settle((tile.row, tile.col), None)  # no neighbours anywhere
        walk.resume()
    answer = world.to_truth_frame(walk.result())

    covered = shape.region(*shape.box)
    x0, y0, x1, y1 = shape.box
    assert answer[y0:y1 + 1, x0:x1 + 1][covered].all(), "every pixel of the mask is object"
    assert answer[cy, cx] and asked, "and the walk asked for background tiles as it entered windows"


# ---- click-only requests ---------------------------------------------------------------------------------------------

def _clicked(**fields) -> pb.SegmentTilesRequest:
    """A click-only request inside tile (0, 0), as the new client sends it: no foreground mask."""
    return pb.SegmentTilesRequest(
        tiles=[_tile(0, 0)], foreground_feature=pb.Feature(points=[pb.Point(x=300, y=600)]), **fields
    )


def _result_pixels(updates: List[pb.SegmentTilesStreamResponse]) -> int:
    assert updates and updates[-1].WhichOneof("update") == "result"
    return sum(
        int(np.count_nonzero(cv2.imdecode(np.frombuffer(segment.mask, np.uint8), cv2.IMREAD_GRAYSCALE)))
        for segment in updates[-1].result.segments
    )


@pytest.mark.asyncio
async def test_a_click_only_request_with_a_zero_count_and_early_background_tiles_completes_and_clears_the_neighbours() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)  # its model answers a 256 x 256 block in the window: x 256..511, y 512..767
    plain = _result_pixels(await asyncio.wait_for(_stream(servicer, _clicked(), [], _context()), timeout=30))
    assert plain == 256 * 256

    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    neighbour = np.ones((TILE_SIZE, 128), dtype=bool)  # x 256..383 over the whole tile: the left half of the answer
    early = pb.MaskTiles(tiles=[_one_tile(neighbour, 256, 0, 0, 0, BACKGROUND)])
    context = _context()
    client = Client()

    updates = await asyncio.wait_for(
        _stream(servicer, _clicked(foreground_mask_tile_count=0), [early], context, client), timeout=30
    )

    context.abort.assert_not_awaited()
    assert (0, 0) not in client.asked_masks, "sent early, so not asked for"
    assert _result_pixels(updates) == 256 * 128, "the neighbour's pixels are cleared from the result"


@pytest.mark.asyncio
async def test_a_zero_count_still_gets_the_background_asked_for_on_demand() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    client = Client()

    updates = await asyncio.wait_for(_stream(servicer, _clicked(foreground_mask_tile_count=0), [], _context(), client), timeout=30)

    assert updates[-1].WhichOneof("update") == "result" and (0, 0) in client.asked_masks


def test_the_collector_is_done_at_once_for_a_zero_count() -> None:
    assert MaskTileCollector(0).foreground_done and not MaskTileCollector(3).foreground_done


def test_a_click_only_request_uses_the_tiled_background_and_the_clicks() -> None:
    collector = MaskTileCollector(0)
    request = pb.SegmentTilesRequest(foreground_feature=pb.Feature(points=[pb.Point(x=5, y=6)]))
    prompts = _prompts_from(request, collector)
    assert prompts.masks == [] and prompts.background_masks == [collector.background] and prompts.foreground == [(5, 6)]


@pytest.mark.asyncio
async def test_a_request_with_no_foreground_at_all_is_invalid_argument() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()
    request = pb.SegmentTilesRequest(
        tiles=[_tile(0, 0)], background_feature=pb.Feature(points=[pb.Point(x=300, y=600)])
    )

    updates = await asyncio.wait_for(_stream(servicer, request, [], context), timeout=30)

    assert updates == [] and _aborted_with(context) == grpc.StatusCode.INVALID_ARGUMENT
    servicer.model.predict_tile.assert_not_called()


@pytest.mark.asyncio
async def test_a_rectangle_only_foreground_starts_the_walk_from_its_centre() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()
    request = pb.SegmentTilesRequest(
        tiles=[_tile(0, 0)],
        foreground_feature=pb.Feature(rectangles=[pb.BoundingBox(x_min=260, y_min=520, x_max=500, y_max=760)]),
    )

    updates = await asyncio.wait_for(_stream(servicer, request, [], context, Client()), timeout=30)

    context.abort.assert_not_awaited()
    assert updates[-1].WhichOneof("update") == "result"
    assert servicer.model.predict_tile.call_args.args[5] is not None, "the rectangle is the box prompt"


# ---- a budget that stops the walk ----------------------------------------------------------------------------------

def test_a_walk_stopped_by_max_cells_logs_the_mask_boundary_cores_it_never_predicted(caplog) -> None:
    import logging

    radius = 700
    corrected, x0, y0 = disc_truth(2048, 2048, radius)
    shape = shape_from(mask_tiles_for(corrected, x0, y0))
    background = background_from([])
    world = World(ellipse(2048, 2048, radius + 150, radius + 150, 0.0))
    walk = GrowthWalk([shape.interior_point()], [], world.predict, masks=[shape], background_masks=[background], max_cells=2)

    with caplog.at_level(logging.WARNING, logger="segmentation_server.tile_growth"):
        walk.advance()
        walk.result()

    assert any("mask-boundary cores unpredicted" in record.getMessage() and "max_cells=2" in record.getMessage()
               for record in caplog.records)
