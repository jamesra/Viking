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


def test_a_client_box_is_taken_as_object_so_a_hole_inside_it_is_filled() -> None:
    world, hole = _world_with_a_hole_in_the_box()
    box, points = client_prompt(2304, 2304, 300)

    plain = world.to_truth_frame(grow_segmentation(points, [], world.predict, boxes=[box]))
    assumed = world.to_truth_frame(grow_segmentation(points, [], world.predict, boxes=[box], assume_boxes=True))

    interior = (slice(hole[0].start + 4, hole[0].stop - 4), slice(hole[1].start + 4, hole[1].stop - 4))
    assert not plain[interior].any(), "SAM2 leaves the hole out, as it does in the field"
    assert assumed[hole].all()
    assert iou(assumed, ellipse(2304, 2304, 300, 300, 0.0)) >= 0.97


def test_only_boxes_the_client_sent_are_taken_as_object() -> None:
    from types import SimpleNamespace

    from segmentation_server.server import _resolve_boxes

    sent = SimpleNamespace(
        foreground_boxes=[SimpleNamespace(x_min=10, y_min=20, x_max=110, y_max=140)]
    )
    boxes, omit, client_chose = _resolve_boxes(sent, [(60, 80)])
    assert boxes == [(10, 20, 110, 140)] and omit == [] and client_chose is True

    none_sent = SimpleNamespace(foreground_boxes=[])
    _boxes, _omit, client_chose = _resolve_boxes(none_sent, [(60, 80)])
    assert client_chose is False


def test_a_box_taken_as_object_is_not_an_answer_when_every_start_is_rejected() -> None:
    def refuse(row, col, points, labels, box=None):
        raise NoMatchingMask("nothing fits")

    box, points = client_prompt(2304, 2304, 300)
    walk = GrowthWalk(points, [], refuse, boxes=[box], assume_boxes=True)
    walk.advance()

    with pytest.raises(NoMatchingMask):
        walk.result()


def test_reserve_cells_still_start_when_a_box_taken_as_object_is_all_the_canvas_holds() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 100, 100, 0.0))
    box, points = client_prompt(2304, 2304, 100)
    refused = []

    def refuse_the_first_window(row, col, pts, lbls, box=None, mask_logits=None):
        if not refused:
            refused.append(Cell(row, col))
            raise NoMatchingMask("the best window finds nothing")
        return world.predict(row, col, pts, lbls, box=box)

    result = grow_segmentation(points, [], refuse_the_first_window, boxes=[box], assume_boxes=True)

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


def _request(**fields):
    from segmentation_grpc import segmentation_pb2

    return segmentation_pb2.SegmentTilesRequest(**fields)


def test_features_merge_with_the_legacy_prompt_fields() -> None:
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server.server import _prompts_from

    request = _request(
        foreground=[pb.Point(x=1, y=2)],
        foreground_feature=pb.Feature(
            points=[pb.Point(x=3, y=4)],
            rectangles=[pb.BoundingBox(x_min=0, y_min=0, x_max=10, y_max=10)],
            mask=pb.Mask(bounds=pb.BoundingBox(x_min=20, y_min=20, x_max=40, y_max=60)),
        ),
        background_feature=pb.Feature(
            points=[pb.Point(x=9, y=9)],
            rectangles=[pb.BoundingBox(x_min=100, y_min=100, x_max=110, y_max=110)],
            mask=pb.Mask(bounds=pb.BoundingBox(x_min=200, y_min=200, x_max=220, y_max=220)),
        ),
    )
    prompts = _prompts_from(request)

    assert prompts.foreground == [(1, 2), (3, 4)] and prompts.background == [(9, 9)]
    assert prompts.hint_boxes == [] and prompts.boxes == [], "a foreground mask replaces the boxes"
    assert [shape.box for shape in prompts.masks] == [(20, 20, 40, 60)]
    assert prompts.background_rects == [(100, 100, 110, 110)] and [shape.box for shape in prompts.background_masks] == [(200, 200, 220, 220)]
    assert prompts.client_chose is True


def test_a_feature_with_only_a_mask_starts_the_walk_from_the_mask_center_and_png_is_refused() -> None:
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server.server import _prompts_from

    bounds = pb.BoundingBox(x_min=20, y_min=20, x_max=40, y_max=60)
    assert _prompts_from(_request(foreground_feature=pb.Feature(mask=pb.Mask(bounds=bounds)))).foreground == [(30, 40)]
    with pytest.raises(ValueError):
        _prompts_from(_request(foreground_feature=pb.Feature(mask=pb.Mask(bounds=bounds, png=b"x"))))


def test_a_hint_box_is_prompted_as_a_box_but_not_taken_as_object() -> None:
    world, hole = _world_with_a_hole_in_the_box()
    square, points = client_prompt(2304, 2304, 300)

    result = grow_segmentation(points, [], world.predict, hint_boxes=[square], assume_boxes=True)

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


def test_a_foreground_rectangle_without_a_mask_stays_a_hint_box() -> None:
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server.server import _prompts_from

    request = _request(
        foreground_boxes=[pb.BoundingBox(x_min=1, y_min=1, x_max=5, y_max=5)],
        foreground_feature=pb.Feature(
            points=[pb.Point(x=3, y=3)], rectangles=[pb.BoundingBox(x_min=0, y_min=0, x_max=10, y_max=10)]
        ),
    )
    prompts = _prompts_from(request)

    assert prompts.hint_boxes == [(0, 0, 10, 10)] and prompts.boxes == [(1, 1, 5, 5)] and prompts.masks == []


def test_a_background_png_mask_is_decoded_y_up_and_only_its_pixels_are_cleared() -> None:
    import cv2
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server.cell_grid import Canvas
    from segmentation_server.server import _prompts_from

    image = np.zeros((40, 50), dtype=np.uint8)  # 50 wide, 40 high; row 0 is y_max
    image[:20, :] = 255  # the top half of the image is the high-y half
    ok, png = cv2.imencode(".png", image)
    bounds = pb.BoundingBox(x_min=2200, y_min=2200, x_max=2249, y_max=2239)
    prompts = _prompts_from(_request(
        foreground=[pb.Point(x=1, y=2)], background_feature=pb.Feature(mask=pb.Mask(bounds=bounds, png=png.tobytes()))
    ))
    shape = prompts.background_masks[0]
    assert shape.bitmap_up.shape == (40, 50) and shape.bitmap_up[39, 0] and not shape.bitmap_up[0, 0]

    canvas = Canvas()
    canvas.assume_box((2200, 2200, 2249, 2239))
    canvas.deny_mask(shape)
    core = canvas.decided_core(cell_of_point(2200, 2200))
    bx, by = core_origin(cell_of_point(2200, 2200))
    assert not core[2239 - by, 2210 - bx], "high-y half is cleared"
    assert core[2200 - by, 2210 - bx], "low-y half is still object"


def test_a_png_that_does_not_match_its_bounds_or_a_foreground_png_is_refused() -> None:
    import cv2
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server.server import _prompts_from

    ok, png = cv2.imencode(".png", np.full((5, 5), 255, dtype=np.uint8))
    bounds = pb.BoundingBox(x_min=0, y_min=0, x_max=9, y_max=9)
    with pytest.raises(ValueError):
        _prompts_from(_request(background_feature=pb.Feature(mask=pb.Mask(bounds=bounds, png=png.tobytes()))))
    with pytest.raises(ValueError):
        _prompts_from(_request(foreground_feature=pb.Feature(mask=pb.Mask(bounds=bounds, png=png.tobytes()))))


def _one_bit_png(rows) -> bytes:
    """A 1-bit greyscale PNG built by hand, the way the client sends a mask."""
    import struct
    import zlib

    height, width = len(rows), len(rows[0])
    raw = b"".join(bytes(1) + np.packbits(np.array(row, dtype=np.uint8)).tobytes() for row in rows)

    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    return (
        bytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 1, 0, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw))
        + chunk(b"IEND", b"")
    )


def test_a_one_bit_png_foreground_mask_decodes() -> None:
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server.server import _prompts_from

    rows = [[1, 0, 0, 0], [1, 1, 0, 0]]  # 4 wide, 2 high; row 0 is y_max
    bounds = pb.BoundingBox(x_min=10, y_min=20, x_max=13, y_max=21)
    prompts = _prompts_from(_request(
        foreground_feature=pb.Feature(mask=pb.Mask(bounds=bounds, png=_one_bit_png(rows)))
    ))
    assert prompts.masks[0].bitmap_up.tolist() == [[True, True, False, False], [True, False, False, False]]
    assert prompts.foreground == [prompts.masks[0].interior_point()], "the click lands on the mask"


def test_a_png_larger_than_one_sam2_window_is_accepted() -> None:
    import cv2
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server.server import _prompts_from

    width, height = 3000, 1500  # several cells across, well past 1024 on both sides
    image = np.zeros((height, width), dtype=np.uint8)
    image[0, 2999] = 255  # top-right pixel: (x_max, y_max)
    ok, png = cv2.imencode(".png", image)
    bounds = pb.BoundingBox(x_min=1000, y_min=2000, x_max=1000 + width - 1, y_max=2000 + height - 1)
    prompts = _prompts_from(_request(foreground_feature=pb.Feature(mask=pb.Mask(bounds=bounds, png=png.tobytes()))))

    bitmap = prompts.masks[0].bitmap_up
    assert bitmap.shape == (height, width) and bitmap.sum() == 1 and bitmap[height - 1, width - 1]
    assert prompts.foreground == [(1000 + width - 1, 2000 + height - 1)]


def test_a_scaled_png_is_expanded_to_blocks_anchored_at_x_min_and_y_max_and_clipped_to_the_bounds() -> None:
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server.server import _prompts_from

    # 7 wide x 5 high at scale 3 -> a 3x2 PNG. Row 0 is y_max; the last column and row are partial blocks.
    rows = [[1, 0, 0], [0, 0, 1]]
    bounds = pb.BoundingBox(x_min=100, y_min=200, x_max=106, y_max=204)
    mask = pb.Mask(bounds=bounds, png=_one_bit_png(rows), scale=3)
    shape = _prompts_from(_request(foreground_feature=pb.Feature(mask=mask))).masks[0]

    expected_down = np.zeros((5, 7), dtype=bool)  # image order, row 0 is y_max
    expected_down[0:3, 0:3] = True  # PNG pixel (0, 0)
    expected_down[3:5, 6:7] = True  # PNG pixel (2, 1): a 1-wide, 2-high clipped block
    assert shape.bitmap_up.tolist() == np.flipud(expected_down).tolist()


@pytest.mark.parametrize("set_scale", [None, 0, 1])
def test_an_unset_zero_or_one_scale_means_one_png_pixel_per_mosaic_pixel(set_scale) -> None:
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server.server import _prompts_from

    rows = [[1, 0], [0, 1]]
    bounds = pb.BoundingBox(x_min=0, y_min=0, x_max=1, y_max=1)
    mask = pb.Mask(bounds=bounds, png=_one_bit_png(rows))
    if set_scale is not None:
        mask.scale = set_scale
    shape = _prompts_from(_request(background_feature=pb.Feature(mask=mask))).background_masks[0]
    assert shape.bitmap_up.tolist() == [[False, True], [True, False]]


def test_a_png_that_does_not_match_the_bounds_at_its_scale_is_refused_before_it_is_decoded() -> None:
    from segmentation_grpc import segmentation_pb2 as pb

    from segmentation_server import server
    from segmentation_server.server import _prompts_from

    bounds = pb.BoundingBox(x_min=0, y_min=0, x_max=6, y_max=4)  # 7 x 5
    png = _one_bit_png([[1, 0, 0], [0, 0, 1]])  # 3 x 2: right for scale 3 only
    for scale in (1, 2, 4):
        with pytest.raises(ValueError, match="3x2"):
            _prompts_from(_request(background_feature=pb.Feature(mask=pb.Mask(bounds=bounds, png=png, scale=scale))))
    _prompts_from(_request(background_feature=pb.Feature(mask=pb.Mask(bounds=bounds, png=png, scale=3))))

    huge = pb.BoundingBox(x_min=0, y_min=0, x_max=server.MAX_MASK_PIXELS, y_max=1)
    with pytest.raises(ValueError, match="exceed"):
        _prompts_from(_request(background_feature=pb.Feature(mask=pb.Mask(bounds=huge, png=b"x", scale=2))))


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
