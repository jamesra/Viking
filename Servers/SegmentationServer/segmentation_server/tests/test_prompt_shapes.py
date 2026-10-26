"""Prompt shapes: rasterization, the encodings the served model was trained with, and islands."""

from __future__ import annotations

import math
from typing import List, Sequence, Tuple

import cv2
import numpy as np
import pytest

from protocol import circle, polygon
from segmentation_grpc import CurveFit, CurveMethod, Ellipse, Line, Shape, Vector2
from segmentation_server import trainer_prompt_encoding as enc
from segmentation_server.cell_grid import CELL_SIZE, Cell, MaskShape, window_origin
from segmentation_server.curve_fit import fit_curve
from segmentation_server.depth_trust import edge_depth, local_thickness, trust_weight
from segmentation_server.prompt import BackgroundShape, assign_clicks, build_islands, foreground_parts
from segmentation_server.prompt_encodings import DepthTrustEncoder, EdgeConfidenceEncoder
from segmentation_server.shapes import AreaGeometry, ShapeError, parse_shape, rasterize

CELL = Cell(row=4, col=4)  # window origin (2048, 2048), aligned on tile (2, 2)


def _parse(shape: Shape):
    return parse_shape(shape, "test shape")


def _window_disc(cell: Cell, cx: float, cy: float, radius: float) -> np.ndarray:
    """The pixels of ``cell``'s window (Y down) whose centres are within ``radius`` of ``(cx, cy)``, worked out here."""
    wx0, wy0 = window_origin(cell)
    cols = np.arange(CELL_SIZE) + wx0 + 0.5
    rows_y = (wy0 + CELL_SIZE - 1 - np.arange(CELL_SIZE)) + 0.5
    return (cols[None, :] - cx) ** 2 + (rows_y[:, None] - cy) ** 2 <= radius ** 2


def _client_fill(rings: Sequence[Sequence[Tuple[float, float]]], x0: int, y0: int, width: int, height: int) -> np.ndarray:
    """The Viking client's ``SegmentationMaskUnion.Fill`` at scale 1, written from the C#: Y up, row 0 at ``y0``."""
    edges = []
    for ring in rings:
        for i in range(len(ring)):
            a, b = ring[i], ring[(i + 1) % len(ring)]
            if a[1] != b[1]:
                edges.append((a[0], a[1], b[0], b[1]))
    out = np.zeros((height, width), dtype=bool)
    for row in range(height):
        y = y0 + row + 0.5
        crossings = sorted(ax + (y - ay) / (by - ay) * (bx - ax) for ax, ay, bx, by in edges if (ay <= y) != (by <= y))
        for i in range(0, len(crossings) - 1, 2):
            first = max(x0, math.ceil(crossings[i] - 0.5))
            last = min(x0 + width - 1, math.floor(crossings[i + 1] - 0.5))
            if last >= first:
                out[row, first - x0:last - x0 + 1] = True
    return out


def _client_circle_ring(cx: float, cy: float, radius: float) -> List[Tuple[float, float]]:
    """``SegmentationMaskUnion.Add`` for a circle: a polygon of ``CircleSegmentsFor(radius)`` vertices on the circle."""
    segments = 180 if not radius > 0.25 else max(180, min(4096, math.ceil(math.pi / math.acos(1 - 0.25 / radius))))
    return [
        (cx + radius * math.cos(2 * math.pi * i / segments), cy + radius * math.sin(2 * math.pi * i / segments))
        for i in range(segments)
    ]


def _islands(shapes: Sequence[Shape], scale: float = 1.0, encoder=None):
    parts = foreground_parts([_parse(shape) for shape in shapes], None, scale)
    if encoder is not None:
        return encoder.islands(parts, max_tiles=256, max_islands=16)
    return build_islands(parts, max_tiles=256)


# ---- rasterization -----------------------------------------------------------------------------------------------

def test_a_polygon_fills_the_pixels_the_viking_client_fills() -> None:
    exterior = [(103.25, 97.5), (260.0, 120.75), (240.5, 300.0), (150.0, 250.25), (90.0, 310.5)]
    hole = [(150.0, 150.0), (200.0, 160.0), (180.0, 210.0)]
    geometry = _parse(polygon(exterior, [hole]))

    ours = rasterize(geometry, 64, 64, 256, 300)

    assert np.array_equal(ours, _client_fill([exterior, hole], 64, 64, 256, 300))
    assert ours.any() and not ours[180 - 64, 175 - 64], "the hole is not filled"


def test_a_polygon_given_with_its_first_point_repeated_is_the_same_polygon() -> None:
    ring = [(10.0, 10.0), (90.0, 15.0), (60.0, 80.0)]
    a = rasterize(_parse(polygon(ring)), 0, 0, 100, 100)
    b = rasterize(_parse(polygon(ring + [ring[0]])), 0, 0, 100, 100)

    assert np.array_equal(a, b)


def test_a_curve_polygon_fills_viking_s_fitted_outline() -> None:
    controls = [(1012.25, 2040.5), (1100.75, 2061.0), (1180.0, 2015.125), (1190.5, 1930.0), (1120.0, 1880.25), (1030.5, 1905.75)]
    shape = polygon(controls)
    shape.polygon.fit.CopyFrom(CurveFit(method=CurveMethod.CURVE_METHOD_CATMULL_ROM, interpolations=5))
    geometry = _parse(shape)

    assert isinstance(geometry, AreaGeometry)
    fitted = fit_curve(controls, 5, True)
    assert list(geometry.rings[0]) == fitted[:-1] or list(geometry.rings[0]) == fitted
    ours = rasterize(geometry, 1000, 1850, 220, 230)
    assert np.array_equal(ours, _client_fill([fitted], 1000, 1850, 220, 230))
    straight = rasterize(_parse(polygon(controls)), 1000, 1850, 220, 230)
    assert ours.sum() > straight.sum(), "the curve bulges past the straight polygon through the same points"


def test_a_circle_is_every_pixel_whose_centre_is_within_its_radius() -> None:
    disc = rasterize(_parse(circle(100.0, 100.0, 30.0)), 0, 0, 200, 200)
    ys, xs = np.mgrid[0:200, 0:200]

    assert np.array_equal(disc, (xs + 0.5 - 100) ** 2 + (ys + 0.5 - 100) ** 2 <= 30 ** 2)


def test_an_ellipse_is_rotated_counter_clockwise_from_x() -> None:
    shape = Shape(ellipse=Ellipse(center=Vector2(x=100, y=100), semi_major=40, semi_minor=10, rotation=math.pi / 2))
    pixels = rasterize(_parse(shape), 0, 0, 200, 200)

    assert pixels[100 + 35, 100] and not pixels[100, 100 + 35], "a quarter turn puts the major axis on Y"


def test_a_line_is_drawn_with_its_width_and_round_ends() -> None:
    shape = Shape(line=Line(points=[Vector2(x=10, y=10.5), Vector2(x=50, y=10.5)], width=4))
    pixels = rasterize(_parse(shape), 0, 0, 64, 32)

    rows = np.flatnonzero(pixels.any(axis=1))
    cols = np.flatnonzero(pixels.any(axis=0))
    assert (rows.min(), rows.max()) == (8, 12), "centres within 2 of y = 10.5"
    assert (cols.min(), cols.max()) == (8, 51), "and of the end points"


def test_an_open_curve_line_follows_the_fitted_points() -> None:
    controls = [(500.0, 500.0), (560.0, 620.0), (600.0, 700.0), (640.0, 610.0), (700.0, 505.5)]
    shape = Shape(line=Line(points=[Vector2(x=x, y=y) for x, y in controls], width=2,
                            fit=CurveFit(method=CurveMethod.CURVE_METHOD_CATMULL_ROM, interpolations=5)))

    geometry = _parse(shape)

    assert list(geometry.points) == fit_curve(controls, 5, False)


@pytest.mark.parametrize(
    "shape,fragment",
    [
        (Shape(point=Vector2(x=1, y=2)), "Shape.point"),
        (Shape(), "no geometry"),
        (circle(1, 2, 0), "radius"),
        (circle(math.nan, 2, 5), "finite"),
        (polygon([(0, 0), (5, 5)]), "at least three"),
        (Shape(line=Line()), "no points"),
    ],
    ids=["point", "empty", "zero-radius", "nan", "two-point-ring", "empty-line"],
)
def test_shapes_the_server_cannot_use_are_refused(shape: Shape, fragment: str) -> None:
    with pytest.raises(ShapeError, match=fragment):
        _parse(shape)


def test_a_curve_fit_denser_than_the_limit_is_refused() -> None:
    shape = polygon([(0, 0), (50, 0), (25, 40)])
    shape.polygon.fit.CopyFrom(CurveFit(method=CurveMethod.CURVE_METHOD_CATMULL_ROM, interpolations=100000))

    with pytest.raises(ShapeError, match="interpolations"):
        _parse(shape)


# ---- v6: softened circles, combined parts, background trust -------------------------------------------------------

def test_a_v6_softened_circle_is_its_disc_at_radius_times_the_scale_encoded_as_a_seed() -> None:
    """The server's prompt for a circle is today's seed encoding of the disc the client should have sent at 0.95."""
    encoder = EdgeConfidenceEncoder({}, circle_scale=0.95)
    cx, cy, radius = 2560.25, 2511.5, 180.0
    (island,) = _islands([circle(cx, cy, radius)], encoder.circle_scale, encoder)

    prompt = encoder.start_window(CELL, island, [])

    disc = _window_disc(CELL, cx, cy, radius * 0.95)
    assert np.array_equal(island.window_full(CELL), disc)
    expected = enc.encode_mask_input((CELL_SIZE, CELL_SIZE), seed=disc)
    assert np.array_equal(prompt, expected)
    # Today's server encoding of a client disc at 0.95 (a polygon of many vertices, filled at pixel centres) is the
    # same prompt but for the pixels within a quarter pixel of the circle, where the polygon's chords fall inside it.
    wx0, wy0 = window_origin(CELL)
    client_up = _client_fill([_client_circle_ring(cx, cy, radius * 0.95)], wx0, wy0, CELL_SIZE, CELL_SIZE)
    client_disc = np.flipud(client_up)
    old = EdgeConfidenceEncoder({}).start_window(CELL, MaskShape((wx0, wy0, wx0 + CELL_SIZE - 1, wy0 + CELL_SIZE - 1), client_up), [])
    differs = disc ^ client_disc
    rows, cols = np.nonzero(differs)
    distance = np.hypot(cols + wx0 + 0.5 - cx, (wy0 + CELL_SIZE - 1 - rows) + 0.5 - cy)
    assert np.all(np.abs(distance - radius * 0.95) <= 0.25)
    assert np.abs(prompt - old).max() < 0.5


def test_several_foreground_shapes_are_one_object_by_the_largest_value() -> None:
    encoder = EdgeConfidenceEncoder({})
    a, b = circle(2500.0, 2500.0, 120.0), circle(2640.0, 2500.0, 120.0)  # overlapping: one island
    (island,) = _islands([a, b], 1.0, encoder)

    prompt = encoder.start_window(CELL, island, [])

    first = encoder._seed(_window_disc(CELL, 2500.0, 2500.0, 120.0))
    second = encoder._seed(_window_disc(CELL, 2640.0, 2500.0, 120.0))
    assert np.array_equal(prompt, enc.area_resize(np.maximum(first, second), enc.LOW_RES))


def test_a_background_circle_is_trusted_by_its_radius() -> None:
    encoder = EdgeConfidenceEncoder({"circle_annotation_trust": {"full_frac": 0.5, "zero_frac": 0.95}})
    cx, cy, radius = 2600.0, 2400.0, 200.0
    neighbour = BackgroundShape(_parse(circle(cx, cy, radius)), encoder.circle_trust)
    wx0, wy0 = window_origin(CELL)

    prompt = encoder.background_logits(CELL, [neighbour])

    expected = enc.circle_negative_canvas(
        (CELL_SIZE, CELL_SIZE), cx - wx0, wy0 + CELL_SIZE - cy, radius, encoder.circle_trust
    ).canvas
    assert np.array_equal(prompt, enc.area_resize(expected, enc.LOW_RES))
    full = expected.min()
    assert full == -enc.SEED_LOGIT, "fully trusted inside half the radius"
    assert np.array_equal(neighbour.window_full(CELL), _window_disc(CELL, cx, cy, 0.95 * radius)), "it claims 0.95 R"


def test_a_background_polygon_is_trusted_by_its_edge() -> None:
    encoder = EdgeConfidenceEncoder({})
    square = [(2300.0, 2300.0), (2500.0, 2300.0), (2500.0, 2500.0), (2300.0, 2500.0)]
    neighbour = BackgroundShape(_parse(polygon(square)))

    prompt = encoder.background_logits(CELL, [neighbour])

    pixels = neighbour.window_full(CELL)
    assert pixels.sum() == 200 * 200
    assert np.array_equal(prompt, enc.area_resize(enc.negative_canvas(pixels).canvas, enc.LOW_RES))


def test_where_the_object_and_a_neighbour_overlap_the_prompt_is_unknown() -> None:
    encoder = EdgeConfidenceEncoder({})
    (island,) = _islands([circle(2500.0, 2500.0, 150.0)], 1.0, encoder)
    neighbour = BackgroundShape(_parse(polygon([(2600.0, 2400.0), (2800.0, 2400.0), (2800.0, 2700.0), (2600.0, 2700.0)])))

    full = enc.encode_dense_canvas(
        (CELL_SIZE, CELL_SIZE), seed=island.window_full(CELL), negatives=[neighbour.window_full(CELL)]
    )

    assert np.array_equal(encoder.start_window(CELL, island, [neighbour]), enc.area_resize(full, enc.LOW_RES))


# ---- islands ------------------------------------------------------------------------------------------------------

def test_one_disc_is_one_island() -> None:
    islands = _islands([circle(2500.0, 2500.0, 150.0)])

    assert [island.number for island in islands] == [1]


def test_two_separate_discs_are_two_islands_numbered_by_area() -> None:
    small, large = circle(1000.0, 1000.0, 50.0), circle(3000.0, 1500.0, 120.0)

    islands = _islands([small, large])

    assert [island.number for island in islands] == [1, 2]
    assert islands[0].area > islands[1].area
    assert islands[0].contains(3000, 1500) and islands[1].contains(1000, 1000)
    assert [len(island.parts) for island in islands] == [1, 1]


def test_islands_join_across_tile_edges_and_corners() -> None:
    """A shape that crosses tile boundaries (diagonally touching ones too) is one island."""
    across = polygon([(1000.0, 1000.0), (1050.0, 1000.0), (1050.0, 1050.0), (1000.0, 1050.0)])  # four tiles meet
    line = Shape(line=Line(points=[Vector2(x=900, y=900), Vector2(x=1100, y=1100)], width=1))

    assert len(_islands([across])) == 1
    assert len(_islands([line])) == 1


def test_clicks_go_to_the_island_they_lie_in_and_strays_go_to_island_one() -> None:
    islands = _islands([circle(1000.0, 1000.0, 50.0), circle(3000.0, 1500.0, 120.0)])

    assigned = assign_clicks(islands, [(1000, 1000), (3010, 1490), (10, 10)])

    assert assigned == [[(3010, 1490), (10, 10)], [(1000, 1000)]]


def test_a_prompt_wider_than_the_tile_limit_is_refused() -> None:
    from segmentation_server.prompt import PromptTooLarge

    with pytest.raises(PromptTooLarge):
        build_islands(foreground_parts([_parse(circle(5000.0, 5000.0, 3000.0))], None, 1.0), max_tiles=4)


# ---- v7: depth-relative trust -------------------------------------------------------------------------------------

def test_the_trust_profile_is_one_inside_a_cosine_between_and_zero_beyond() -> None:
    trust = enc.CircleAnnotationTrust(0.5, 0.95)
    rho = np.array([0.0, 0.5, 0.725, 0.95, 1.0], dtype=np.float32)

    weights = trust_weight(rho, trust)

    assert weights[0] == 1.0 and weights[1] == 1.0
    assert weights[2] == pytest.approx(0.5, abs=1e-6)
    assert weights[3] == 0.0 and weights[4] == 0.0


def test_local_thickness_is_the_radius_of_the_largest_disc_through_each_pixel() -> None:
    yy, xx = np.mgrid[0:200, 0:300]
    disc = (xx - 100.5) ** 2 + (yy - 100.5) ** 2 <= 60 ** 2
    band = (yy >= 90) & (yy < 110) & (xx >= 150) & (xx < 290)  # a 20 px band off the disc's side
    shape = disc | band

    thickness = local_thickness(edge_depth(shape))

    assert thickness[100, 100] == pytest.approx(edge_depth(shape)[100, 100])
    assert abs(thickness[100, 60] - thickness[100, 100]) < 1.0, "every pixel of the disc lies in its largest disc"
    assert 8.0 <= thickness[95, 250] <= 11.0, "the band far from the disc has its own half-width"


def test_a_v7_circle_is_its_radius_relative_trust_used_positively() -> None:
    encoder = DepthTrustEncoder({})
    cx, cy, radius = 2560.25, 2511.5, 180.0
    (island,) = _islands([circle(cx, cy, radius)], encoder.circle_scale, encoder)
    wx0, wy0 = window_origin(CELL)

    prompt = encoder.start_window(CELL, island, [])

    _claimed, weight = enc.circle_trust((CELL_SIZE, CELL_SIZE), cx - wx0, wy0 + CELL_SIZE - cy, radius, encoder.circle_trust)
    assert np.allclose(prompt, enc.area_resize(enc.SEED_LOGIT * weight, enc.LOW_RES), atol=1e-4)


def _two_lobed_outline() -> List[Tuple[float, float]]:
    """The outline of two overlapping discs (radius 150 at x 2350, radius 100 at x 2560, both at y 2400), traced."""
    yy, xx = np.mgrid[0:600, 0:800]
    lobes = ((xx - 250) ** 2 + (yy - 300) ** 2 <= 150 ** 2) | ((xx - 460) ** 2 + (yy - 300) ** 2 <= 100 ** 2)
    contours, _ = cv2.findContours(lobes.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    (outline,) = contours
    return [(2100.0 + float(x) + 0.5, 2100.0 + float(y) + 0.5) for x, y in outline[:, 0, :]]


def test_v7_splits_a_two_lobed_polygon_into_its_lobes_where_v6_sees_one_island() -> None:
    """One softened polygon whose lobes meet at a waist: one connected prompt, but two trusted cores."""
    shapes = [polygon(_two_lobed_outline())]

    v6 = _islands(shapes, 1.0, EdgeConfidenceEncoder({}))
    v7 = _islands(shapes, 1.0, DepthTrustEncoder({}))

    assert len(v6) == 1
    assert [island.number for island in v7] == [1, 2]
    assert v7[0].contains(2350, 2400) and v7[1].contains(2560, 2400), "numbered by core area, the larger first"
    assert not v7[0].contains(2560, 2400) and not v7[1].contains(2350, 2400)
    assert v7[0].area > v7[1].area


def test_under_v7_a_bar_between_two_bodies_has_its_own_core() -> None:
    """A thin bar drawn as its own shape is trusted by its own depth, so it is an island of its own (section 4)."""
    left = [(2100.0, 2300.0), (2300.0, 2300.0), (2300.0, 2500.0), (2100.0, 2500.0)]
    right = [(2600.0, 2250.0), (2900.0, 2250.0), (2900.0, 2550.0), (2600.0, 2550.0)]
    bar = [(2290.0, 2395.0), (2610.0, 2395.0), (2610.0, 2405.0), (2290.0, 2405.0)]

    islands = _islands([polygon(left), polygon(right), polygon(bar)], 1.0, DepthTrustEncoder({}))

    assert len(islands) == 3
    assert islands[0].contains(2750, 2400) and islands[1].contains(2200, 2400) and islands[2].contains(2450, 2400)


def test_a_v7_island_is_zero_on_its_outer_band_and_full_in_its_core() -> None:
    encoder = DepthTrustEncoder({})
    square = [(2300.0, 2300.0), (2700.0, 2300.0), (2700.0, 2700.0), (2300.0, 2700.0)]
    (island,) = _islands([polygon(square)], 1.0, encoder)

    trust = island.trust_window(CELL)
    wx0, wy0 = window_origin(CELL)

    def at(x: int, y: int) -> float:
        return float(trust[wy0 + CELL_SIZE - 1 - y, x - wx0])

    assert at(2500, 2500) == 1.0, "the centre is trusted"
    assert at(2301, 2500) == 0.0, "the outer 5% of the depth is not"
    assert not island.contains(2300, 2500)
