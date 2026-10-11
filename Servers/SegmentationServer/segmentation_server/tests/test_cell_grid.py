"""Cell grid geometry: ownership, window cropping across tile joins, canvas, contacts, seeds."""

from __future__ import annotations

import numpy as np
import pytest
from hypothesis import given, settings
from hypothesis import strategies as st

from segmentation_server.cell_grid import (
    CELL_SIZE,
    CORE_MARGIN,
    CORE_SIZE,
    LOGIT_CORE,
    LOGIT_CORE_MARGIN,
    LOGIT_WINDOW,
    UNSEEN_LOGIT,
    Canvas,
    Cell,
    TileIndex,
    cell_of_point,
    core_origin,
    crop_window,
    in_window,
    is_aligned,
    mosaic_to_window,
    neighbor_cells,
    tile_of_point,
    tiles_for_cell,
    window_origin,
)

coords = st.integers(min_value=-20000, max_value=20000)


@given(x=coords, y=coords)
def test_every_point_has_one_owner_whose_core_contains_it(x: int, y: int) -> None:
    cell = cell_of_point(x, y)
    ox, oy = core_origin(cell)

    assert ox <= x < ox + CORE_SIZE
    assert oy <= y < oy + CORE_SIZE
    neighbors = [n for n in neighbor_cells(cell) if _core_contains(n, x, y)]
    assert neighbors == []


def _core_contains(cell: Cell, x: int, y: int) -> bool:
    ox, oy = core_origin(cell)
    return ox <= x < ox + CORE_SIZE and oy <= y < oy + CORE_SIZE


@given(x=coords, y=coords)
def test_owner_window_holds_the_point_with_context_on_every_side(x: int, y: int) -> None:
    cell = cell_of_point(x, y)
    wx, wy = mosaic_to_window(cell, x, y)

    assert in_window((wx, wy))
    assert CORE_MARGIN <= wx < CORE_MARGIN + CORE_SIZE
    assert CORE_MARGIN <= wy < CORE_MARGIN + CORE_SIZE


def test_aligned_cell_window_is_one_uploaded_tile() -> None:
    cell = Cell(row=4, col=6)

    assert is_aligned(cell)
    assert window_origin(cell) == (3072, 2048)
    assert tiles_for_cell(cell) == [TileIndex(row=2, col=3)]
    assert not is_aligned(Cell(row=4, col=5))


def test_offset_cell_needs_two_or_four_tiles() -> None:
    assert len(tiles_for_cell(Cell(row=4, col=5))) == 2
    assert len(tiles_for_cell(Cell(row=5, col=5))) == 4
    assert tile_of_point(1023, 1024) == TileIndex(row=1, col=0)
    assert tile_of_point(-1, -1) == TileIndex(row=-1, col=-1)


def _mosaic_tiles(rows: range, cols: range, seed: int = 0) -> tuple[dict, np.ndarray, int, int]:
    """Y-down tile images cut from one random Y-up mosaic, and that mosaic with its origin."""
    rng = np.random.default_rng(seed)
    x_min, y_min = cols.start * 1024, rows.start * 1024
    mosaic_up = rng.integers(0, 256, size=((rows.stop - rows.start) * 1024, (cols.stop - cols.start) * 1024), dtype=np.uint8)
    tiles = {}
    for row in rows:
        for col in cols:
            up = mosaic_up[(row - rows.start) * 1024:(row - rows.start + 1) * 1024,
                           (col - cols.start) * 1024:(col - cols.start + 1) * 1024]
            tiles[(row, col)] = np.flipud(up)[:, :, None].repeat(3, axis=2)
    return tiles, mosaic_up, x_min, y_min


@settings(max_examples=25, deadline=None)
@given(row=st.integers(min_value=0, max_value=4), col=st.integers(min_value=0, max_value=4))
def test_crop_window_matches_the_mosaic_for_every_alignment(row: int, col: int) -> None:
    tiles, mosaic_up, x_min, y_min = _mosaic_tiles(range(0, 3), range(0, 3))
    cell = Cell(row=row, col=col)

    window = crop_window(cell, tiles)

    assert window is not None and window.shape == (CELL_SIZE, CELL_SIZE, 3)
    x0, y0 = window_origin(cell)
    expected_up = mosaic_up[y0 - y_min:y0 - y_min + CELL_SIZE, x0 - x_min:x0 - x_min + CELL_SIZE]
    assert np.array_equal(window[:, :, 0], np.flipud(expected_up))


def test_crop_window_is_none_when_a_tile_is_missing_or_wrong_size() -> None:
    tiles, _mosaic, _x, _y = _mosaic_tiles(range(0, 2), range(0, 2))
    offset = Cell(row=1, col=1)
    del tiles[(1, 1)]

    assert crop_window(offset, tiles) is None

    tiles[(1, 1)] = np.zeros((512, 512, 3), dtype=np.uint8)
    assert crop_window(offset, tiles) is None


def test_canvas_only_adds_pixels_and_reports_new_ones() -> None:
    canvas = Canvas()
    cell = Cell(1, 1)
    first = np.zeros((CORE_SIZE, CORE_SIZE), dtype=bool)
    first[10:20, 10:20] = True
    second = np.zeros_like(first)
    second[15:30, 15:30] = True

    assert canvas.or_core(cell, first) == 100
    assert canvas.or_core(cell, second) == 225 - 25
    assert canvas.or_core(cell, np.zeros_like(first)) == 0
    assert int(canvas.core(cell).sum()) == 100 + 200


def test_canvas_read_assembles_blocks_across_cores() -> None:
    canvas = Canvas()
    left, right = Cell(0, 0), Cell(0, 1)
    full = np.ones((CORE_SIZE, CORE_SIZE), dtype=bool)
    canvas.or_core(left, full)
    canvas.or_core(right, full)
    lx, ly = core_origin(left)

    region = canvas.read(lx + CORE_SIZE - 4, ly + 5, 8, 3)

    assert region.shape == (3, 8)
    assert region.all()
    assert canvas.contains(lx, ly)
    assert not canvas.contains(lx - 1, ly)
    assert not canvas.read(lx - 10, ly, 5, 5).any()


def test_blend_window_fills_the_cores_it_overlaps_and_reports_what_each_gained() -> None:
    canvas = Canvas()
    cell = Cell(2, 2)

    added = canvas.blend_window(cell, np.full((LOGIT_WINDOW, LOGIT_WINDOW), 5.0, dtype=np.float32), 1.0)

    assert set(added) == {cell} | set(neighbor_cells(cell))
    assert added[cell] == CORE_SIZE * CORE_SIZE
    assert canvas.decided_core(cell).all()
    assert canvas.core(cell).all()
    assert canvas.blend_window(cell, np.full((LOGIT_WINDOW, LOGIT_WINDOW), 5.0, dtype=np.float32), 1.0) == {}
    # The outer pixels of the window blend with an unseen neighbor sample and fall below the iso.
    # The walk's control view also leaves out the unreliable outer 64 px of the window.
    x0, y0 = window_origin(cell)
    window = canvas.read(x0, y0, CELL_SIZE, CELL_SIZE)
    assert window[64:-64, 64:-64].all()
    assert not window[:60, :].any()


def test_blend_window_rejects_logits_that_are_not_the_native_window() -> None:
    with pytest.raises(ValueError):
        Canvas().blend_window(Cell(1, 1), np.zeros((CELL_SIZE, CELL_SIZE), dtype=np.float32), 1.0)


@settings(max_examples=25, deadline=None)
@given(
    x=st.integers(20, LOGIT_WINDOW - 60),
    y=st.integers(20, LOGIT_WINDOW - 60),
    size=st.integers(6, 40),
)
def test_blend_window_places_pixels_at_their_mosaic_position(x: int, y: int, size: int) -> None:
    canvas = Canvas()
    cell = Cell(3, 5)
    logits = np.full((LOGIT_WINDOW, LOGIT_WINDOW), -6.0, dtype=np.float32)
    logits[y:y + size, x:x + size] = 6.0

    canvas.blend_window(cell, logits, 1.0)

    x0, y0 = window_origin(cell)
    window = canvas.read(x0, y0, CELL_SIZE, CELL_SIZE)
    scale = CELL_SIZE // LOGIT_WINDOW
    inside = window[(y + 1) * scale:(y + size - 1) * scale, (x + 1) * scale:(x + size - 1) * scale]
    assert inside.all()
    assert not window[:(y - 1) * scale, :].any() and not window[(y + size + 1) * scale:, :].any()
    assert not window[:, :(x - 1) * scale].any() and not window[:, (x + size + 1) * scale:].any()


def test_a_window_decides_least_at_its_border_so_the_cell_that_sees_a_pixel_centred_wins() -> None:
    canvas = Canvas()
    left, right = Cell(2, 2), Cell(2, 3)
    canvas.blend_window(left, np.full((LOGIT_WINDOW, LOGIT_WINDOW), 6.0, dtype=np.float32), 1.0)

    canvas.blend_window(right, np.full((LOGIT_WINDOW, LOGIT_WINDOW), -6.0, dtype=np.float32), 1.0)

    # The right window starts 256 px into the left core. Just inside its border it barely counts,
    # a full margin in it counts as much as the left window, which cancels it out.
    core = canvas.decided_core(left)
    assert core[:, 256:262].all()
    assert not core[:, CORE_SIZE - 40:].any()
    assert core[:, :256].all()


def test_blending_can_remove_pixels_a_weaker_window_added() -> None:
    canvas = Canvas()
    cell = Cell(2, 2)
    canvas.blend_window(cell, np.full((LOGIT_WINDOW, LOGIT_WINDOW), 2.0, dtype=np.float32), 1.0)
    assert canvas.core(cell).all()

    canvas.blend_window(cell, np.full((LOGIT_WINDOW, LOGIT_WINDOW), -8.0, dtype=np.float32), 1.0)

    assert not canvas.core(cell).any()


def test_pixels_from_a_prediction_without_logits_and_assumed_pixels_survive_blending() -> None:
    canvas = Canvas()
    cell = Cell(2, 2)
    own = np.zeros((CORE_SIZE, CORE_SIZE), dtype=bool)
    own[:10, :10] = True
    canvas.or_core(cell, own)
    bx, by = core_origin(cell)
    canvas.assume_box((bx + 100, by + 100, bx + 120, by + 120))

    canvas.blend_window(cell, np.full((LOGIT_WINDOW, LOGIT_WINDOW), -8.0, dtype=np.float32), 1.0)

    core = canvas.core(cell)
    assert core[:10, :10].all()
    assert core[100:121, 100:121].all()
    assert int(core.sum()) == 100 + 21 * 21


def test_fused_mosaic_is_y_down_marks_unseen_and_raises_assumed_pixels_to_the_pin_level() -> None:
    canvas = Canvas()
    low, high = Cell(2, 2), Cell(3, 2)
    logits = np.full((LOGIT_WINDOW, LOGIT_WINDOW), 3.0, dtype=np.float32)
    canvas.blend_window(high, logits, 1.0)
    canvas.assume_core(low)

    mosaic = canvas.fused_mosaic(2, 3, 2, 2, pin_level=7.5)

    assert mosaic.shape == (2 * LOGIT_CORE, LOGIT_CORE)
    assert np.allclose(mosaic[:LOGIT_CORE], 3.0)  # row 3 is Y-up above row 2, so it is the top block
    assert np.allclose(mosaic[LOGIT_CORE:], 7.5)
    assert np.allclose(canvas.fused_block(Cell(9, 9)), UNSEEN_LOGIT)
    assert canvas.has_logits()
    assert not Canvas().has_logits()


def test_assume_box_fills_the_box_across_cores_and_blending_cannot_take_it_back() -> None:
    canvas = Canvas()
    first = Cell(2, 2)
    bx, by = core_origin(first)
    box = (bx + 400, by + 450, bx + 600, by + 560)  # crosses the east and north core edges

    added = canvas.assume_box(box)

    assert sum(added.values()) == 201 * 111
    assert canvas.core(first)[450:, 400:].all()
    assert not canvas.core(first)[:450, :].any() and not canvas.core(first)[:, :400].any()
    assert canvas.core(Cell(2, 3))[:, :89].any()
    assert canvas.assume_box(box) == {}

    canvas.blend_window(first, np.full((LOGIT_WINDOW, LOGIT_WINDOW), -8.0, dtype=np.float32), 1.0)
    assert sum(int(canvas.core(cell).sum()) for cell in added) == 201 * 111


def _blob(window: np.ndarray, cx: int, cy: int, radius: int) -> None:
    ys, xs = np.ogrid[:CELL_SIZE, :CELL_SIZE]
    window |= (xs - cx) ** 2 + (ys - cy) ** 2 <= radius ** 2


def test_a_sample_only_silent_predictions_cover_stays_unseen_not_background() -> None:
    canvas = Canvas()
    cell = Cell(2, 2)
    logits = np.full((LOGIT_WINDOW, LOGIT_WINDOW), 4.0, dtype=np.float32)
    silent = np.zeros((LOGIT_WINDOW, LOGIT_WINDOW), dtype=bool)
    silent[100:104, 100:104] = True

    canvas.blend_window(cell, logits, 1.0, ignore_up=silent)

    block = canvas.fused_block(cell)
    oy = ox = LOGIT_CORE_MARGIN
    assert np.all(block[100 - oy:104 - oy, 100 - ox:104 - ox] == UNSEEN_LOGIT)
    assert block[90 - oy, 90 - ox] == pytest.approx(4.0, abs=0.01), "what the prediction is not silent about is kept"


def test_a_silent_sample_loses_to_a_window_that_saw_it() -> None:
    canvas = Canvas()
    first, second = Cell(2, 2), Cell(2, 3)
    silent = np.ones((LOGIT_WINDOW, LOGIT_WINDOW), dtype=bool)
    canvas.blend_window(first, np.full((LOGIT_WINDOW, LOGIT_WINDOW), -6.0, dtype=np.float32), 1.0, ignore_up=silent)

    canvas.blend_window(second, np.full((LOGIT_WINDOW, LOGIT_WINDOW), 6.0, dtype=np.float32), 1.0)

    core = canvas.decided_core(first)
    assert core[:, 300:480].all(), "where the second window saw the pixel, it decides"


def _object_in_covered_background() -> Canvas:
    """A 40x40 sample object well inside one window, background all round, nothing past the window."""
    canvas = Canvas()
    logits = np.full((LOGIT_WINDOW, LOGIT_WINDOW), -6.0, dtype=np.float32)
    logits[100:140, 100:140] = 6.0
    canvas.blend_window(Cell(2, 2), logits, 1.0)
    return canvas


def test_an_object_inside_covered_background_has_no_frontier() -> None:
    assert _object_in_covered_background().frontier_points(Cell(2, 2), 1.0) == []


def test_an_object_that_meets_unseen_area_has_a_frontier_at_its_strongest_sample_there() -> None:
    canvas = Canvas()
    logits = np.full((LOGIT_WINDOW, LOGIT_WINDOW), -6.0, dtype=np.float32)
    logits[100:140, 100:140] = 6.0
    logits[139, 100:140] = 9.0   # the strongest samples are the ones on the uncovered side
    silent = np.zeros((LOGIT_WINDOW, LOGIT_WINDOW), dtype=bool)
    silent[140:, :] = True       # nothing above the object was seen
    canvas.blend_window(Cell(2, 2), logits, 1.0, ignore_up=silent)

    points = canvas.frontier_points(Cell(2, 2), 1.0)

    assert len(points) == 1
    x, y, logit = points[0]
    assert logit == pytest.approx(9.0, abs=0.05)
    # Sample row 139 of the window is mosaic y = (2 * 128 + 139) * 4, and the strip spans samples 100..139 in x.
    assert (2 * 128 + 139) * 4 <= y <= (2 * 128 + 139) * 4 + 4
    assert (2 * 128 + 100) * 4 <= x <= (2 * 128 + 140) * 4


def test_a_speck_touching_unseen_area_is_not_a_frontier() -> None:
    canvas = Canvas()
    logits = np.full((LOGIT_WINDOW, LOGIT_WINDOW), -6.0, dtype=np.float32)
    logits[100:102, 100:102] = 6.0
    silent = np.zeros((LOGIT_WINDOW, LOGIT_WINDOW), dtype=bool)
    silent[102:, :] = True
    canvas.blend_window(Cell(2, 2), logits, 1.0, ignore_up=silent)

    assert canvas.frontier_points(Cell(2, 2), 1.0) == []
