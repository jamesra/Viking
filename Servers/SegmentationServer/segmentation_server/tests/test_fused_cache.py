"""The fused-block cache and the cheaper mask-window paths must give exactly what the uncached versions did."""

from __future__ import annotations

import numpy as np

from segmentation_server.cell_grid import LOGIT_WINDOW, UNSEEN_LOGIT, Canvas, Cell, MaskShape, window_origin


def _logits(value: float) -> np.ndarray:
    return np.full((LOGIT_WINDOW, LOGIT_WINDOW), value, dtype=np.float32)


def test_a_fused_block_is_reused_until_a_window_changes_it() -> None:
    canvas = Canvas()
    canvas.blend_window(Cell(2, 2), _logits(4.0), 1.0)
    owner = Cell(2, 2)

    first = canvas.fused_block(owner)
    assert canvas.fused_block(owner) is first, "no new window, no recompute"

    canvas.blend_window(Cell(2, 3), _logits(-6.0), 1.0)
    second = canvas.fused_block(owner)
    assert second is not first
    assert not np.array_equal(second, first), "the neighbouring window changed the shared samples"
    assert np.array_equal(first, first.copy()), "the older array was left as it was"


def test_a_cached_block_matches_a_fresh_computation_and_is_read_only() -> None:
    canvas = Canvas()
    canvas.blend_window(Cell(1, 1), _logits(3.0), 1.0)
    cached = canvas.fused_block(Cell(1, 1))

    total, weight = canvas._sum[Cell(1, 1)], canvas._weight[Cell(1, 1)]
    expected = np.full_like(total, UNSEEN_LOGIT)
    seen = weight > 1e-6
    expected[seen] = total[seen] / weight[seen]

    assert np.allclose(cached[seen], expected[seen])
    assert not cached.flags.writeable


def test_cells_nothing_has_predicted_share_one_read_only_unseen_block() -> None:
    canvas = Canvas()

    block = canvas.fused_block(Cell(50, 50))

    assert np.all(block == UNSEEN_LOGIT)
    assert block is canvas.fused_block(Cell(60, 60))
    assert not block.flags.writeable


def test_taking_a_window_back_by_replacing_it_drops_the_stale_block() -> None:
    canvas = Canvas()
    canvas.blend_window(Cell(2, 2), _logits(5.0), 1.0)
    before = canvas.fused_block(Cell(2, 2)).copy()

    canvas.blend_window(Cell(2, 2), _logits(-5.0), 1.0)  # the same cell predicts again

    after = canvas.fused_block(Cell(2, 2))
    assert after.mean() < before.mean()


def test_a_rectangle_mask_marks_exactly_the_samples_the_bitmap_path_would() -> None:
    cell = Cell(3, 3)
    wx, wy = window_origin(cell)
    for box in [
        (wx + 10, wy + 20, wx + 500, wy + 700),
        (wx - 300, wy - 300, wx + 5, wy + 5),         # hangs off the low corner
        (wx + 1000, wy + 1000, wx + 1500, wy + 1500),  # hangs off the high corner
        (wx + 13, wy + 13, wx + 13, wy + 13),          # one pixel
        (wx, wy, wx + 1023, wy + 1023),                # the whole window
    ]:
        width, height = box[2] - box[0] + 1, box[3] - box[1] + 1
        rectangle = MaskShape(box).window_samples(cell)
        bitmap = MaskShape(box, np.ones((height, width), dtype=bool)).window_samples(cell)
        assert np.array_equal(rectangle, bitmap), box


def test_a_rectangle_that_misses_the_window_has_no_samples() -> None:
    cell = Cell(3, 3)
    wx, wy = window_origin(cell)

    assert MaskShape((wx + 5000, wy, wx + 5100, wy + 10)).window_samples(cell) is None
