"""Cell grid geometry: ownership, window cropping across tile joins, canvas, contacts, seeds."""

from __future__ import annotations

import numpy as np
from hypothesis import given, settings
from hypothesis import strategies as st

from segmentation_server.cell_grid import (
    CELL_SIZE,
    CORE_MARGIN,
    CORE_SIZE,
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


def test_or_window_splits_a_window_across_the_cores_it_overlaps_and_never_clears() -> None:
    canvas = Canvas()
    cell = Cell(2, 2)
    window = np.zeros((CELL_SIZE, CELL_SIZE), dtype=bool)
    window[:] = True

    added = canvas.or_window(cell, window)

    assert set(added) == {cell} | set(neighbor_cells(cell))
    assert added[cell] == CORE_SIZE * CORE_SIZE
    assert sum(added.values()) == CELL_SIZE * CELL_SIZE
    assert canvas.or_window(cell, np.zeros_like(window)) == {}
    assert canvas.or_window(cell, window) == {}
    x0, y0 = window_origin(cell)
    assert canvas.read(x0, y0, CELL_SIZE, CELL_SIZE).all()


@settings(max_examples=25, deadline=None)
@given(
    x=st.integers(0, CELL_SIZE - 60),
    y=st.integers(0, CELL_SIZE - 60),
    size=st.integers(1, 60),
)
def test_or_window_places_pixels_at_their_mosaic_position(x: int, y: int, size: int) -> None:
    canvas = Canvas()
    cell = Cell(3, 5)
    window = np.zeros((CELL_SIZE, CELL_SIZE), dtype=bool)
    window[y:y + size, x:x + size] = True

    canvas.or_window(cell, window)

    x0, y0 = window_origin(cell)
    assert np.array_equal(canvas.read(x0, y0, CELL_SIZE, CELL_SIZE), window)


def test_set_veto_removes_a_neighbors_pixels_but_not_the_owners_own() -> None:
    canvas = Canvas()
    owner = Cell(2, 2)
    neighbor = Cell(2, 1)
    owner_window = np.zeros((CELL_SIZE, CELL_SIZE), dtype=bool)
    owner_window[CORE_MARGIN:CORE_MARGIN + 100, CORE_MARGIN:CORE_MARGIN + 100] = True
    canvas.or_window(owner, owner_window)
    neighbor_window = np.ones((CELL_SIZE, CELL_SIZE), dtype=bool)
    canvas.or_window(neighbor, neighbor_window)

    veto = np.ones((CORE_SIZE, CORE_SIZE), dtype=bool)
    removed = canvas.set_veto(owner, veto)

    after = canvas.core(owner)
    assert removed == CORE_MARGIN * CORE_SIZE - 100 * 100
    assert np.array_equal(after, owner_window[CORE_MARGIN:CORE_MARGIN + CORE_SIZE, CORE_MARGIN:CORE_MARGIN + CORE_SIZE])


def test_a_vetoed_pixel_cannot_be_added_by_a_neighbor_later_and_none_clears_the_veto() -> None:
    canvas = Canvas()
    owner = Cell(2, 2)
    neighbor = Cell(2, 1)
    veto = np.zeros((CORE_SIZE, CORE_SIZE), dtype=bool)
    veto[:, :50] = True
    canvas.set_veto(owner, veto)
    window = np.ones((CELL_SIZE, CELL_SIZE), dtype=bool)
    reach = CORE_MARGIN

    added = canvas.or_window(neighbor, window)

    core = canvas.core(owner)
    assert not core[:, :50].any()
    assert core[:, 50:reach].all()
    assert not core[:, reach:].any()
    assert added[owner] == (reach - 50) * CORE_SIZE

    canvas.set_veto(owner, None)
    canvas.or_window(neighbor, window)
    assert canvas.core(owner)[:, :reach].all()

def _blob(window: np.ndarray, cx: int, cy: int, radius: int) -> None:
    ys, xs = np.ogrid[:CELL_SIZE, :CELL_SIZE]
    window |= (xs - cx) ** 2 + (ys - cy) ** 2 <= radius ** 2
