"""The client's prompt starts the walk in every cell that holds part of its box or any of its clicks."""

from __future__ import annotations

import numpy as np
import pytest

from cell_world import World, ellipse, iou
from segmentation_server.cell_grid import Cell, cell_of_point, core_origin, mosaic_to_window
from segmentation_server.mask_utils import NoMatchingMask
from segmentation_server.tile_growth import GrowthWalk, grow_segmentation


class _RecordingWorld(World):
    def __init__(self, truth) -> None:
        super().__init__(truth)
        self.given = []

    def predict(self, row, col, points, labels, box=None, mask_logits=None):
        self.given.append((Cell(row, col), list(points), list(labels), box))
        return super().predict(row, col, points, labels, box=box, mask_logits=mask_logits)


def client_prompt(cx: int, cy: int, radius: int):
    """What a modern client sends: the inscribed square, and four points on the axes at 0.95 radius."""
    half = int(radius / np.sqrt(2))
    box = (cx - half, cy - half, cx + half, cy + half)
    reach = int(round(0.95 * radius))
    points = [(cx + reach, cy), (cx, cy + reach), (cx - reach, cy), (cx, cy - reach)]
    return box, points


def _first_call(world, cell):
    return next(call for call in world.given if call[0] == cell)


def test_every_core_the_box_touches_is_started_with_the_box_or_continued_from_its_neighbors() -> None:
    # A 400 px disc centred on the corner shared by four cores (their boundaries meet at 2304, 2304).
    world = _RecordingWorld(ellipse(2304, 2304, 300, 300, 0.0))
    box, points = client_prompt(2304, 2304, 300)

    grow_segmentation(points, [], world.predict, boxes=[box])

    touched = {cell_of_point(x, y) for x in (box[0], box[2]) for y in (box[1], box[3])}
    assert len(touched) == 4
    boxed_starts = 0
    for cell in touched:
        index = next(i for i, call in enumerate(world.given) if call[0] == cell)
        given_box = world.given[index][3]
        if given_box is not None:
            boxed_starts += 1
        else:
            assert world.mask_prompts[index] is not None, f"{cell} got neither the box nor a mask prompt"
    assert boxed_starts >= 1, "the cell the user's box starts in is prompted with the box"


def _world_with_a_hole_in_the_box():
    truth = ellipse(2304, 2304, 300, 300, 0.0).copy()
    hole = (slice(2290, 2330), slice(2250, 2290))
    truth[hole] = False
    return _RecordingWorld(truth), hole


def test_a_rectangle_mask_is_taken_as_object_so_a_hole_inside_it_is_filled() -> None:
    world, hole = _world_with_a_hole_in_the_box()
    box, points = client_prompt(2304, 2304, 300)

    plain = world.to_truth_frame(grow_segmentation(points, [], world.predict, boxes=[box]))
    masked = world.to_truth_frame(grow_segmentation(points, [], world.predict, masks=[box]))

    interior = (slice(hole[0].start + 4, hole[0].stop - 4), slice(hole[1].start + 4, hole[1].stop - 4))
    assert not plain[interior].any(), "SAM2 leaves the hole out, as it does in the field, and a box is never object"
    assert masked[hole].all()
    assert iou(masked, ellipse(2304, 2304, 300, 300, 0.0)) >= 0.97


def test_a_mask_is_not_an_answer_when_every_start_is_rejected() -> None:
    def refuse(row, col, points, labels, box=None, mask_logits=None):
        raise NoMatchingMask("nothing fits")

    box, points = client_prompt(2304, 2304, 300)
    walk = GrowthWalk(points, [], refuse, masks=[box])
    walk.advance()

    with pytest.raises(NoMatchingMask):
        walk.result()


def test_reserve_cells_still_start_when_a_mask_is_all_the_canvas_holds() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 100, 100, 0.0))
    box, points = client_prompt(2304, 2304, 100)
    refused = []

    def refuse_the_first_window(row, col, pts, lbls, box=None, mask_logits=None):
        if not refused:
            refused.append(Cell(row, col))
            raise NoMatchingMask("the best window finds nothing")
        return world.predict(row, col, pts, lbls, box=box, mask_logits=mask_logits)

    result = grow_segmentation(points, [], refuse_the_first_window, masks=[box])

    starts = _user_starts(result)
    assert starts[0].outcome == "rejected" and len(starts) >= 2


def _user_starts(result):
    return [seam for seam in result.seams if seam.kind == "user"]


def test_a_prompt_that_fits_one_window_starts_in_one_window() -> None:
    # Centred on the corner four cores share, but small enough that any of the four windows holds all of it.
    world = _RecordingWorld(ellipse(2304, 2304, 100, 100, 0.0))
    box, points = client_prompt(2304, 2304, 100)

    result = grow_segmentation(points, [], world.predict, boxes=[box])

    starts = _user_starts(result)
    assert len(starts) == 1
    _cell, sent, _labels, given_box = world.given[0]
    assert given_box is not None
    assert len(sent) == 4, "the one start window was prompted with the whole prompt"
    assert iou(world.to_truth_frame(result), world.truth_up) >= 0.95


def test_a_prompt_wider_than_any_window_still_starts_in_fewer_windows_than_cells_touched() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 300, 300, 0.0))
    box, points = client_prompt(2304, 2304, 300)

    result = grow_segmentation(points, [], world.predict, boxes=[box])

    assert 1 <= len(_user_starts(result)) < 4
    assert iou(world.to_truth_frame(result), world.truth_up) >= 0.97


def test_cells_the_best_window_did_not_need_start_when_it_finds_nothing() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 100, 100, 0.0))
    box, points = client_prompt(2304, 2304, 100)
    refused = []

    def refuse_the_first_window(row, col, pts, lbls, box=None, mask_logits=None):
        if not refused:
            refused.append(Cell(row, col))
            raise NoMatchingMask("the best window finds nothing")
        return world.predict(row, col, pts, lbls, box=box)

    result = grow_segmentation(points, [], refuse_the_first_window, boxes=[box])

    starts = _user_starts(result)
    assert starts[0].outcome == "rejected" and starts[0].cell == refused[0]
    assert len(starts) >= 2 and starts[1].cell != refused[0]
    assert result.mask.any()


def test_a_click_outside_the_box_starts_its_own_cell() -> None:
    world = _RecordingWorld(ellipse(2048, 2048, 700, 700, 0.0))
    box, points = client_prompt(2048, 2048, 300)
    far = (2048, 2048 + 650)
    assert cell_of_point(*far) not in {cell_of_point(box[0], box[1]), cell_of_point(box[2], box[3])}
    assert cell_of_point(*far) != cell_of_point(*points[0]), "not the cell the first click already starts"

    result = grow_segmentation([*points, far], [], world.predict, boxes=[box])

    kinds = [(seam.kind, seam.cell) for seam in result.seams]
    first_edge = next((i for i, (kind, _cell) in enumerate(kinds) if kind == "edge"), len(kinds))
    started_up_front = {cell for kind, cell in kinds[:first_edge] if kind == "user"}
    assert cell_of_point(*far) in started_up_front, "started with the others, not reached later"


def test_the_prompt_is_the_box_and_the_clicks_in_the_window_and_nothing_else() -> None:
    world = _RecordingWorld(ellipse(2048, 2048, 300, 300, 0.0))
    box, points = client_prompt(2048, 2048, 300)

    grow_segmentation(points, [], world.predict, boxes=[box])

    _cell, sent, labels, given_box = world.given[0]
    assert given_box is not None
    assert len(sent) == 4 and set(labels) == {1}, "the four axis points; the box center is not sent as a click"


def test_the_box_center_is_still_a_click_when_the_client_sent_it() -> None:
    world = _RecordingWorld(ellipse(2048, 2048, 300, 300, 0.0))
    box, points = client_prompt(2048, 2048, 300)

    grow_segmentation([(2048, 2048), *points], [], world.predict, boxes=[box])

    assert len(world.given[0][1]) == 5


def test_a_cell_with_no_click_in_its_window_is_prompted_by_the_box_alone() -> None:
    world = _RecordingWorld(ellipse(2048, 2048, 300, 300, 0.0))
    box, _points = client_prompt(2048, 2048, 300)

    grow_segmentation([(2048 + 700, 2048)], [], world.predict, boxes=[box])

    _cell, sent, _labels, given_box = next(call for call in world.given if call[3] is not None)
    assert sent == [], "the click is outside this window, so the box is the whole prompt"


def test_the_new_prompt_recovers_a_disc() -> None:
    world = _RecordingWorld(ellipse(2048, 2048, 400, 400, 0.0))
    box, points = client_prompt(2048, 2048, 400)

    result = grow_segmentation(points, [], world.predict, boxes=[box])

    assert iou(world.to_truth_frame(result), world.truth_up) >= 0.97


def test_the_request_fails_only_when_every_start_cell_is_rejected_and_nothing_was_found() -> None:
    def refuse(row, col, points, labels, box=None):
        raise NoMatchingMask("nothing fits")

    box, points = client_prompt(2304, 2304, 300)
    walk = GrowthWalk(points, [], refuse, boxes=[box])
    walk.advance()

    with pytest.raises(NoMatchingMask):
        walk.result()


def test_one_start_cell_succeeding_is_enough() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 300, 300, 0.0))
    box, points = client_prompt(2304, 2304, 300)
    first = {"seen": False}

    def refuse_the_first_cell(row, col, pts, lbls, box=None, mask_logits=None):
        if not first["seen"]:
            first["seen"] = True
            raise NoMatchingMask("the first cell finds nothing")
        return world.predict(row, col, pts, lbls, box=box)

    result = grow_segmentation(points, [], refuse_the_first_cell, boxes=[box])

    assert result.mask.any()
    assert result.seams[0].outcome == "rejected"


def test_a_walk_that_is_waiting_for_tiles_has_not_failed() -> None:
    world = World(ellipse(2048, 2048, 300, 300, 0.0), available=[])
    box, points = client_prompt(2048, 2048, 300)
    walk = GrowthWalk(points, [], world.predict, boxes=[box])

    walk.advance()

    assert walk.take_requested(), "the cells need tiles it does not have"
    walk.result()


def test_a_mask_prompt_starts_the_cell_with_mask_input_and_no_box_and_is_taken_as_object() -> None:
    world, hole = _world_with_a_hole_in_the_box()
    square, points = client_prompt(2304, 2304, 300)

    result = grow_segmentation(points, [], world.predict, masks=[square])

    start = world.calls[0]
    assert world.mask_prompts[0] is not None and world.mask_prompts[0].max() > 0, "the square is the mask_input"
    assert mosaic_to_window(start[0], 2304, 2304) in start[1], "the square's center is a click"
    assert world.to_truth_frame(result)[hole].all(), "everything inside a mask is object"


def _mask_tiles(bitmap_up: np.ndarray, x0: int, y0: int, kind: int):
    """A Y-up boolean mask placed at mosaic (x0, y0), cut into the MaskTile messages of the 1024 grid it touches."""
    import cv2
    from segmentation_grpc import GridCell
    from segmentation_grpc import segmentation_pb2 as pb

    height, width = bitmap_up.shape
    tiles = []
    for row in range(y0 // 1024, (y0 + height - 1) // 1024 + 1):
        for col in range(x0 // 1024, (x0 + width - 1) // 1024 + 1):
            tile = np.zeros((1024, 1024), dtype=bool)
            ya, yb = max(y0, row * 1024), min(y0 + height, (row + 1) * 1024)
            xa, xb = max(x0, col * 1024), min(x0 + width, (col + 1) * 1024)
            tile[ya - row * 1024:yb - row * 1024, xa - col * 1024:xb - col * 1024] = bitmap_up[ya - y0:yb - y0, xa - x0:xb - x0]
            if not tile.any():
                continue
            ok, png = cv2.imencode(".png", np.flipud(tile).astype(np.uint8) * 255)
            assert ok
            tiles.append(pb.MaskTile(cell=GridCell(row=row, col=col), kind=kind, png=png.tobytes()))
    return tiles


def _islands(tiles=(), shapes=()):
    """The islands of a request whose FOREGROUND bitmap is ``tiles`` and whose foreground shapes are ``shapes``."""
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server.mask_tiles import MaskTileCollector
    from segmentation_server.prompt import build_islands, foreground_parts

    collector = MaskTileCollector(len(tiles))
    if tiles:
        collector.add(pb.MaskTiles(tiles=list(tiles)))
    return build_islands(foreground_parts(list(shapes), collector.foreground_shape(), 1.0), max_tiles=256)


def test_a_foreground_bitmap_is_one_island_with_the_bitmaps_own_box() -> None:
    from segmentation_grpc import segmentation_pb2 as pb

    square = np.ones((41, 21), dtype=bool)  # x 20..40, y 20..60
    (island,) = _islands(_mask_tiles(square, 20, 20, pb.MASK_KIND_FOREGROUND))

    assert island.number == 1 and island.box == (20, 20, 40, 60) and island.area == 41 * 21


def test_a_foreground_with_only_mask_tiles_starts_the_walk_from_a_point_inside_the_mask() -> None:
    from segmentation_grpc import segmentation_pb2 as pb

    square = np.ones((41, 21), dtype=bool)
    (island,) = _islands(_mask_tiles(square, 20, 20, pb.MASK_KIND_FOREGROUND))

    assert island.interior_point() == (30, 40)


def test_clicks_alone_have_no_islands() -> None:
    assert _islands() == []


def test_a_hint_box_is_prompted_as_a_box_but_not_taken_as_object() -> None:
    world, hole = _world_with_a_hole_in_the_box()
    square, points = client_prompt(2304, 2304, 300)

    result = grow_segmentation(points, [], world.predict, hint_boxes=[square])

    assert world.mask_prompts[0] is None, "a rectangle is a box prompt, not mask_input"
    interior = (slice(hole[0].start + 4, hole[0].stop - 4), slice(hole[1].start + 4, hole[1].stop - 4))
    assert not world.to_truth_frame(result)[interior].any(), "a hint is not pinned as object"


def test_a_background_mask_is_cleared_from_the_answer_and_painted_negative_in_the_prompt() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 300, 300, 0.0))
    square, points = client_prompt(2304, 2304, 300)
    carve = (2400, 2200, 2500, 2400)

    result = grow_segmentation(points, [], world.predict, masks=[square], background_masks=[carve])

    answer = world.to_truth_frame(result)
    assert not answer[carve[1]:carve[3] + 1, carve[0]:carve[2] + 1].any()
    assert answer[2304, 2304]
    assert world.mask_prompts[0].min() <= -8.0


def test_a_background_rectangle_is_only_a_weak_negative_and_never_clears_the_answer() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 300, 300, 0.0))
    square, points = client_prompt(2304, 2304, 300)
    region = (2530, 2200, 2600, 2400)  # outside the square mask, so nothing positive is known there

    result = grow_segmentation(points, [], world.predict, masks=[square], background_rects=[region])

    prompts = [prompt for prompt in world.mask_prompts if prompt is not None]
    assert any(prompt.min() == pytest.approx(-2.0) for prompt in prompts), "the rectangle reached a window"
    assert world.mask_prompts[0].max() == pytest.approx(8.0)
    assert world.to_truth_frame(result)[2304, 2304]


def test_every_pixel_of_a_foreground_bitmap_mask_is_returned_as_object_even_where_sam2_left_a_hole() -> None:
    from segmentation_server.cell_grid import MaskShape

    world, hole = _world_with_a_hole_in_the_box()
    # A disc-shaped mask (an inscribed circle, not a rectangle) around the hole.
    x0, y0, x1, y1 = 2204, 2204, 2403, 2403
    yy, xx = np.mgrid[y0:y1 + 1, x0:x1 + 1]
    disc = (xx - 2303.5) ** 2 + (yy - 2303.5) ** 2 <= 99.5 ** 2
    shape = MaskShape((x0, y0, x1, y1), disc)
    _box, points = client_prompt(2304, 2304, 300)

    answer = world.to_truth_frame(grow_segmentation(points, [], world.predict, masks=[shape]))

    assert answer[y0:y1 + 1, x0:x1 + 1][disc].all(), "everything inside the mask is foreground"
    assert world.mask_prompts[0] is not None and world.mask_prompts[0].max() > 0


def test_a_mask_that_wholly_covers_a_core_takes_it_as_object_without_predicting_it() -> None:
    from segmentation_server.cell_grid import MaskShape, core_origin

    cell = cell_of_point(2304, 2304)
    bx, by = core_origin(cell)
    box = (bx, by, bx + 511, by + 511)
    full = MaskShape(box, np.ones((512, 512), dtype=bool))
    partial_bitmap = np.ones((512, 512), dtype=bool)
    partial_bitmap[100, 100] = False
    partial = MaskShape(box, partial_bitmap)
    assert full.covers_core(cell) and not partial.covers_core(cell)


def test_a_tiled_mask_wider_than_a_window_spans_cells_and_is_returned_as_object() -> None:
    from segmentation_grpc import segmentation_pb2 as pb

    # A 1400 x 1400 disc, more than one 1024 window, sent as the four mask tiles it touches.
    cx, cy, radius = 2048, 2048, 700
    side = 2 * radius
    yy, xx = np.mgrid[0:side, 0:side]
    disc = (xx - (side - 1) / 2) ** 2 + (yy - (side - 1) / 2) ** 2 <= radius ** 2
    tiles = _mask_tiles(disc, cx - radius, cy - radius, pb.MASK_KIND_FOREGROUND)
    assert len(tiles) == 4
    (shape,) = _islands(tiles)
    assert shape.box == (cx - radius, cy - radius, cx + radius - 1, cy + radius - 1)

    world = _RecordingWorld(ellipse(cx, cy, radius + 100, radius + 100, 0.0))
    answer = world.to_truth_frame(grow_segmentation([shape.interior_point()], [], world.predict, masks=[shape]))

    assert answer[cy - radius:cy + radius, cx - radius:cx + radius][disc].all(), "every pixel of the large mask is object"
    assert len({call[0] for call in world.given}) > 1, "more than one cell took part"
