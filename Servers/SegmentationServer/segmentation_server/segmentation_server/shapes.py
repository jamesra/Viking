"""Annotation shapes of a prompt (``viking.geometry.v1.Shape``): validated, fitted and rasterized per tile or window.

Coordinates are the grid's space at its downsample, X right, Y up, as in the rest of the server (``cell_grid``). Mosaic
pixel ``(x, y)`` covers ``[x, x + 1) x [y, y + 1)``; a pixel is inside a shape when its centre ``(x + 0.5, y + 0.5)`` is,
the rule the Viking client's mask rasterizer uses (``SegmentationMaskUnion.FillCentres``), so a shape sent as geometry
covers the pixels the client used to send as a bitmap.

* A circle is analytic: ``|centre - c| <= radius``.
* An ellipse is analytic, rotated by ``rotation`` radians counter-clockwise from +X.
* A line is a stroke: every pixel whose centre is within ``width / 2`` of the polyline (round joins and caps); a closed
  line adds the closing segment. A width below one pixel is drawn one pixel wide, so a line is never invisible.
* A polygon is an even-odd scanline fill of all its rings, exterior and holes together, exactly as the client's
  ``SegmentationMaskUnion.Fill`` does.
* A ``CurveFit`` of ``CURVE_METHOD_CATMULL_ROM`` replaces each ring or line by Viking's fitted outline
  (:func:`curve_fit.fit_curve`, closed for rings and closed lines). ``interpolations`` 0 means straight segments.

:class:`ShapeRegion` is a shape as a :class:`cell_grid.MaskShape`, rasterized on demand, so the growth walk, the canvas
and the prompt encoders read it like any other mask.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Any, List, Optional, Sequence, Tuple, Union

import numpy as np

from segmentation_server.cell_grid import Box, MaskArray, MaskShape, Point
from segmentation_server.curve_fit import fit_curve

FloatPoint = Tuple[float, float]

# Proto enum values of viking.geometry.v1.CurveMethod, kept as numbers so this module does not import the stubs.
CURVE_METHOD_NONE = 0
CURVE_METHOD_CATMULL_ROM = 1

MAX_CURVE_INTERPOLATIONS = 64
"""Most points a CurveFit may add between two control points. Viking's views use single digits; a large value would
only cost memory and time."""

MAX_SHAPE_VERTICES = 100_000
"""Most vertices one shape may have after fitting."""

MIN_STROKE_HALF_WIDTH = 0.5
"""Half the narrowest stroke drawn: one pixel."""


class ShapeError(ValueError):
    """A shape the server cannot use. The message names the shape and what is wrong; the RPC answers INVALID_ARGUMENT."""


@dataclass(frozen=True)
class CircleGeometry:
    cx: float
    cy: float
    radius: float


@dataclass(frozen=True)
class EllipseGeometry:
    cx: float
    cy: float
    semi_major: float
    semi_minor: float
    rotation: float


@dataclass(frozen=True)
class StrokeGeometry:
    """A line drawn with a width: ``points`` already fitted, ``closed`` adds the segment back to the first point."""

    points: Tuple[FloatPoint, ...]
    closed: bool
    half_width: float


@dataclass(frozen=True)
class AreaGeometry:
    """A polygon: its rings (exterior first, then holes), already fitted, filled even-odd."""

    rings: Tuple[Tuple[FloatPoint, ...], ...]


Geometry = Union[CircleGeometry, EllipseGeometry, StrokeGeometry, AreaGeometry]


def _finite(where: str, *values: float) -> None:
    if not all(math.isfinite(value) for value in values):
        raise ShapeError(f"{where} has a coordinate that is not a finite number.")


def _points(where: str, points: Sequence[Any]) -> List[FloatPoint]:
    out = [(float(point.x), float(point.y)) for point in points]
    for x, y in out:
        _finite(where, x, y)
    return out


def _fitted(where: str, points: List[FloatPoint], fit: Any, closed: bool) -> List[FloatPoint]:
    """``points`` through the shape's CurveFit: unchanged for none, Viking's Catmull-Rom otherwise."""
    method = int(fit.method) if fit is not None else CURVE_METHOD_NONE
    if method == CURVE_METHOD_NONE:
        return points
    if method != CURVE_METHOD_CATMULL_ROM:
        raise ShapeError(f"{where} has an unknown curve method {method}.")
    interpolations = int(fit.interpolations)
    if interpolations > MAX_CURVE_INTERPOLATIONS:
        raise ShapeError(
            f"{where} asks for {interpolations} curve interpolations; the server accepts at most {MAX_CURVE_INTERPOLATIONS}."
        )
    try:
        fitted = fit_curve(points, interpolations, closed)
    except (ValueError, ZeroDivisionError) as error:
        raise ShapeError(f"{where} cannot be fitted as a curve ({error}).") from error
    for x, y in fitted:
        _finite(where, x, y)
    return fitted


def _ring_without_closing_point(points: List[FloatPoint]) -> List[FloatPoint]:
    if len(points) > 1 and points[0] == points[-1]:
        return points[:-1]
    return points


def parse_shape(shape: Any, where: str) -> Geometry:
    """A ``viking.geometry.v1.Shape`` as a :data:`Geometry`, fitted and checked.

    Raises:
        ShapeError: No geometry is set, it is a ``point`` (a click belongs in ``Feature.points``), a number is not
            finite, a radius or semi-axis is not positive, a ring has fewer than three points or a line none, or a
            curve fit is unknown or too dense.
    """
    kind = shape.WhichOneof("kind")
    if kind is None:
        raise ShapeError(f"{where} has no geometry.")
    if kind == "point":
        raise ShapeError(f"{where} is a point; Shape.point is not accepted in a prompt (a click goes in Feature.points).")
    if kind == "circle":
        circle = shape.circle
        _finite(where, circle.center.x, circle.center.y, circle.radius)
        if circle.radius <= 0:
            raise ShapeError(f"{where} is a circle with radius {circle.radius}; the radius must be positive.")
        return CircleGeometry(float(circle.center.x), float(circle.center.y), float(circle.radius))
    if kind == "ellipse":
        ellipse = shape.ellipse
        _finite(where, ellipse.center.x, ellipse.center.y, ellipse.semi_major, ellipse.semi_minor, ellipse.rotation)
        if ellipse.semi_major <= 0 or ellipse.semi_minor <= 0:
            raise ShapeError(f"{where} is an ellipse without two positive semi-axes.")
        return EllipseGeometry(
            float(ellipse.center.x), float(ellipse.center.y), float(ellipse.semi_major), float(ellipse.semi_minor),
            float(ellipse.rotation),
        )
    if kind == "line":
        line = shape.line
        points = _points(where, line.points)
        if not points:
            raise ShapeError(f"{where} is a line with no points.")
        _finite(where, line.width)
        if line.width < 0:
            raise ShapeError(f"{where} is a line with a negative width.")
        closed = bool(line.closed)
        if closed:
            points = _ring_without_closing_point(points)
        fitted = _fitted(where, points, line.fit if line.HasField("fit") else None, closed)
        if len(fitted) > MAX_SHAPE_VERTICES:
            raise ShapeError(f"{where} has {len(fitted)} vertices; the server accepts at most {MAX_SHAPE_VERTICES}.")
        return StrokeGeometry(tuple(fitted), closed, max(float(line.width) / 2.0, MIN_STROKE_HALF_WIDTH))
    polygon = shape.polygon
    fit = polygon.fit if polygon.HasField("fit") else None
    rings: List[Tuple[FloatPoint, ...]] = []
    total = 0
    for index, ring in enumerate([polygon.exterior, *polygon.holes]):
        name = f"{where} exterior" if index == 0 else f"{where} hole {index}"
        points = _ring_without_closing_point(_points(name, ring.points))
        if len(points) < 3:
            raise ShapeError(f"{name} has {len(points)} points; a ring needs at least three.")
        fitted = _fitted(name, points, fit, True)
        total += len(fitted)
        rings.append(tuple(fitted))
    if total > MAX_SHAPE_VERTICES:
        raise ShapeError(f"{where} has {total} vertices; the server accepts at most {MAX_SHAPE_VERTICES}.")
    return AreaGeometry(tuple(rings))


def scaled_circle(circle: CircleGeometry, scale: float) -> CircleGeometry:
    """The same circle with its radius multiplied by ``scale``."""
    return CircleGeometry(circle.cx, circle.cy, circle.radius * float(scale))


def extent(geometry: Geometry) -> Tuple[float, float, float, float]:
    """``(x_min, y_min, x_max, y_max)`` of everything the shape can cover, in continuous coordinates."""
    if isinstance(geometry, CircleGeometry):
        r = geometry.radius
        return geometry.cx - r, geometry.cy - r, geometry.cx + r, geometry.cy + r
    if isinstance(geometry, EllipseGeometry):
        a, b, t = geometry.semi_major, geometry.semi_minor, geometry.rotation
        half_x = math.hypot(a * math.cos(t), b * math.sin(t))
        half_y = math.hypot(a * math.sin(t), b * math.cos(t))
        return geometry.cx - half_x, geometry.cy - half_y, geometry.cx + half_x, geometry.cy + half_y
    if isinstance(geometry, StrokeGeometry):
        xs = [x for x, _ in geometry.points]
        ys = [y for _, y in geometry.points]
        h = geometry.half_width
        return min(xs) - h, min(ys) - h, max(xs) + h, max(ys) + h
    xs = [x for x, _ in geometry.rings[0]]
    ys = [y for _, y in geometry.rings[0]]
    return min(xs), min(ys), max(xs), max(ys)


def pixel_box(geometry: Geometry) -> Optional[Box]:
    """The inclusive mosaic-pixel box of the pixels whose centres can be inside the shape, or None for none."""
    x_min, y_min, x_max, y_max = extent(geometry)
    first_x, last_x = math.ceil(x_min - 0.5), math.floor(x_max - 0.5)
    first_y, last_y = math.ceil(y_min - 0.5), math.floor(y_max - 0.5)
    if last_x < first_x or last_y < first_y:
        return None
    return int(first_x), int(first_y), int(last_x), int(last_y)


def rasterize(geometry: Geometry, x0: int, y0: int, width: int, height: int) -> MaskArray:
    """The shape over the mosaic pixels ``[x0, x0 + width) x [y0, y0 + height)``, Y up (row 0 is ``y0``)."""
    out = np.zeros((max(0, height), max(0, width)), dtype=np.bool_)
    box = pixel_box(geometry)
    if box is None or width <= 0 or height <= 0:
        return out
    xa, ya = max(box[0], x0), max(box[1], y0)
    xb, yb = min(box[2], x0 + width - 1), min(box[3], y0 + height - 1)
    if xa > xb or ya > yb:
        return out
    target = out[ya - y0:yb - y0 + 1, xa - x0:xb - x0 + 1]
    if isinstance(geometry, CircleGeometry):
        _fill_circle(target, geometry, xa, ya)
    elif isinstance(geometry, EllipseGeometry):
        _fill_ellipse(target, geometry, xa, ya)
    elif isinstance(geometry, StrokeGeometry):
        _fill_stroke(target, geometry, xa, ya)
    else:
        _fill_even_odd(target, geometry.rings, xa, ya)
    return out


def _centres(target: MaskArray, x0: int, y0: int) -> Tuple[np.ndarray, np.ndarray]:
    """Pixel-centre coordinates of ``target``'s columns and rows."""
    xs = np.arange(target.shape[1], dtype=np.float64) + (x0 + 0.5)
    ys = np.arange(target.shape[0], dtype=np.float64) + (y0 + 0.5)
    return xs, ys


def _fill_circle(target: MaskArray, circle: CircleGeometry, x0: int, y0: int) -> None:
    xs, ys = _centres(target, x0, y0)
    dx2 = (xs - circle.cx) ** 2
    dy2 = (ys - circle.cy) ** 2
    target |= (dy2[:, None] + dx2[None, :]) <= circle.radius * circle.radius


def _fill_ellipse(target: MaskArray, ellipse: EllipseGeometry, x0: int, y0: int) -> None:
    xs, ys = _centres(target, x0, y0)
    dx = xs[None, :] - ellipse.cx
    dy = ys[:, None] - ellipse.cy
    cos_t, sin_t = math.cos(ellipse.rotation), math.sin(ellipse.rotation)
    u = (dx * cos_t + dy * sin_t) / ellipse.semi_major
    v = (-dx * sin_t + dy * cos_t) / ellipse.semi_minor
    target |= (u * u + v * v) <= 1.0


def _fill_stroke(target: MaskArray, stroke: StrokeGeometry, x0: int, y0: int) -> None:
    """Every pixel whose centre is within ``half_width`` of a segment (one capsule per segment)."""
    points = list(stroke.points)
    if stroke.closed and len(points) > 2:
        points.append(points[0])
    if len(points) == 1:
        points.append(points[0])
    h = stroke.half_width
    height, width = target.shape
    for (ax, ay), (bx, by) in zip(points[:-1], points[1:]):
        # Only the segment's own box (grown by the half width) can be inside its capsule.
        ca = max(0, math.ceil(min(ax, bx) - h - 0.5) - x0)
        cb = min(width - 1, math.floor(max(ax, bx) + h - 0.5) - x0)
        ra = max(0, math.ceil(min(ay, by) - h - 0.5) - y0)
        rb = min(height - 1, math.floor(max(ay, by) + h - 0.5) - y0)
        if ca > cb or ra > rb:
            continue
        px = np.arange(ca, cb + 1, dtype=np.float64)[None, :] + (x0 + 0.5) - ax
        py = np.arange(ra, rb + 1, dtype=np.float64)[:, None] + (y0 + 0.5) - ay
        vx, vy = bx - ax, by - ay
        length2 = vx * vx + vy * vy
        if length2 > 0:
            t = np.clip((px * vx + py * vy) / length2, 0.0, 1.0)
            dx, dy = px - t * vx, py - t * vy
        else:
            dx, dy = px, py
        target[ra:rb + 1, ca:cb + 1] |= (dx * dx + dy * dy) <= h * h


def _fill_even_odd(target: MaskArray, rings: Sequence[Sequence[FloatPoint]], x0: int, y0: int) -> None:
    """Even-odd scanline fill at pixel centres, as ``SegmentationMaskUnion.Fill`` in the Viking client."""
    ax: List[float] = []
    ay: List[float] = []
    bx: List[float] = []
    by: List[float] = []
    for ring in rings:
        count = len(ring)
        for i in range(count):
            a, b = ring[i], ring[(i + 1) % count]
            if a[1] != b[1]:
                ax.append(a[0])
                ay.append(a[1])
                bx.append(b[0])
                by.append(b[1])
    if not ax:
        return
    edges_ax, edges_ay = np.asarray(ax), np.asarray(ay)
    edges_bx, edges_by = np.asarray(bx), np.asarray(by)
    height, width = target.shape
    for row in range(height):
        y = y0 + row + 0.5
        crossing = (edges_ay <= y) != (edges_by <= y)
        if not crossing.any():
            continue
        xs = np.sort(
            edges_ax[crossing]
            + (y - edges_ay[crossing]) / (edges_by[crossing] - edges_ay[crossing]) * (edges_bx[crossing] - edges_ax[crossing])
        )
        for i in range(0, len(xs) - 1, 2):
            first = max(0, math.ceil(xs[i] - 0.5) - x0)
            last = min(width - 1, math.floor(xs[i + 1] - 0.5) - x0)
            if last >= first:
                target[row, first:last + 1] = True


class ShapeRegion(MaskShape):
    """A :data:`Geometry` as a :class:`MaskShape` whose pixels are rasterized when read.

    ``box`` is the shape's pixel box (:func:`pixel_box`). It is a claim about every pixel it covers however many cells
    it spans (:attr:`unbounded`).
    """

    def __init__(self, geometry: Geometry, role: str = "seed") -> None:
        box = pixel_box(geometry)
        object.__setattr__(self, "geometry", geometry)
        object.__setattr__(self, "bitmap_up", None)
        object.__setattr__(self, "role", role)
        object.__setattr__(self, "_box", box)

    def __eq__(self, other: object) -> bool:
        return self is other

    __hash__ = object.__hash__

    @property
    def box(self) -> Box:  # type: ignore[override]
        return self._box if self._box is not None else (0, 0, -1, -1)

    @property
    def filled(self) -> bool:
        return False

    @property
    def unbounded(self) -> bool:
        return True

    @property
    def empty(self) -> bool:
        return self._box is None

    def region(self, xa: int, ya: int, xb: int, yb: int) -> MaskArray:
        return rasterize(self.geometry, xa, ya, xb - xa + 1, yb - ya + 1)

    def interior_point(self) -> Point:
        """A pixel of the shape near its middle: the centre for a circle or ellipse, else the set pixel nearest the
        middle of the box."""
        geometry = self.geometry
        if isinstance(geometry, (CircleGeometry, EllipseGeometry)):
            return int(math.floor(geometry.cx)), int(math.floor(geometry.cy))
        return MaskShape(self.box, self.region(*self.box)).interior_point()
