"""Viking's centripetal Catmull-Rom curve fit, ported so a fitted outline here is the one the client draws.

A ``viking.geometry.v1.CurveFit`` of ``CURVE_METHOD_CATMULL_ROM`` names ``Geometry/CurveFitting/CatmullRom.cs``
``FitCurve`` in the Viking repository. This is a line-by-line port of it and of the helpers it calls
(``CurveExtensions.TryAddTPointsAboveThreshold`` and ``MeasureCurvature``, ``PrimitiveExtensions.RemoveAdjacentDuplicates``
with a preserved path, ``Vector2`` epsilon equality and ``ArcAngle``). The arithmetic is written in the same order as the
C#, so the points agree to the last few bits; ``tests/test_curve_fit.py`` pins them against output of the C# assembly.

Points are ``(x, y)`` tuples of floats.
"""

from __future__ import annotations

import bisect
import math
from typing import Iterator, List, Sequence, Tuple

Point = Tuple[float, float]

EPSILON = 0.001
"""``Geometry.Tolerance.Epsilon``: two points closer than this compare equal."""

EPSILON_SQUARED = EPSILON * EPSILON

_NAN: Point = (math.nan, math.nan)


def _equal(a: Point, b: Point) -> bool:
    """``Vector2.Equals``: equal within EPSILON on each axis and in distance (NaN is never equal)."""
    dx = a[0] - b[0]
    if dx < -EPSILON or dx > EPSILON:
        return False
    dy = a[1] - b[1]
    if dy < -EPSILON or dy > EPSILON:
        return False
    return (dx * dx) + (dy * dy) <= EPSILON_SQUARED


def _distance_squared(a: Point, b: Point) -> float:
    dx = a[0] - b[0]
    dy = a[1] - b[1]
    return (dx * dx) + (dy * dy)


def _point_along_line(a: Point, b: Point, fraction: float) -> Point:
    """``LineSegment(a, b).PointAlongLine(fraction)``."""
    delta = ((b[0] - a[0]) * fraction, (b[1] - a[1]) * fraction)
    return (a[0] + delta[0], a[1] + delta[1])


def _arc_angle(origin: Point, a: Point, b: Point) -> float:
    """``Vector2.ArcAngle(origin, a, b)`` with ``Clockwise`` false."""
    u = (a[0] - origin[0], a[1] - origin[1])
    v = (b[0] - origin[0], b[1] - origin[1])
    angle = math.atan2(u[1], u[0]) - math.atan2(v[1], v[0])
    if angle <= -math.pi:
        angle += math.pi * 2
    elif angle > math.pi:
        angle -= math.pi * 2
    return angle


def _tj(ti: float, pi: Point, pj: Point, alpha: float = 0.5) -> float:
    dx = pi[0] - pj[0]
    dy = pi[1] - pj[1]
    return math.pow(math.sqrt((dx * dx) + (dy * dy)), alpha) + ti


def _measure_curvature(points: Sequence[Point]) -> List[float]:
    """``CurveExtensions.MeasureCurvature``: how far each point turns away from continuing straight, in radians."""
    angles = [0.0] * len(points)
    for i in range(1, len(points) - 1):
        if _equal(points[i - 1], points[i]):
            raise ValueError("Duplicate points found in MeasureCurvature")
        if _distance_squared(points[i], points[i - 1]) < EPSILON_SQUARED:
            angles[i] = 0.0
            continue
        ahead = _point_along_line(points[i - 1], points[i], 2.0)
        angles[i] = _arc_angle(points[i], ahead, points[i + 1])
    return angles


def _fit_segment_with_t_values(
    p0: Point, p1: Point, p2: Point, p3: Point, t_values: Sequence[float]
) -> List[Point]:
    """``CatmullRom.FitCurveSegmentWithTValues``: one point per t, the expressions in the C# order."""
    t0 = 0.0
    t1 = _tj(t0, p0, p1)
    t2 = _tj(t1, p1, p2)
    t3 = _tj(t2, p2, p3)
    output: List[Point] = []
    for t in t_values:
        a1x = (t1 - t) / (t1 - t0) * p0[0] + (t - t0) / (t1 - t0) * p1[0]
        a1y = (t1 - t) / (t1 - t0) * p0[1] + (t - t0) / (t1 - t0) * p1[1]

        a2x = (t2 - t) / (t2 - t1) * p1[0] + (t - t1) / (t2 - t1) * p2[0]
        a2y = (t2 - t) / (t2 - t1) * p1[1] + (t - t1) / (t2 - t1) * p2[1]

        a3x = (t3 - t) / (t3 - t2) * p2[0] + (t - t2) / (t3 - t2) * p3[0]
        a3y = (t3 - t) / (t3 - t2) * p2[1] + (t - t2) / (t3 - t2) * p3[1]

        b1x = ((t2 - t) / (t2 - t0)) * a1x + ((t - t0) / (t2 - t0)) * a2x
        b1y = ((t2 - t) / (t2 - t0)) * a1y + ((t - t0) / (t2 - t0)) * a2y

        b2x = ((t3 - t) / (t3 - t1)) * a2x + ((t - t1) / (t3 - t1)) * a3x
        b2y = ((t3 - t) / (t3 - t1)) * a2y + ((t - t1) / (t3 - t1)) * a3y

        cx = ((t2 - t) / (t2 - t1)) * b1x + ((t - t1) / (t2 - t1)) * b2x
        cy = ((t2 - t) / (t2 - t1)) * b1y + ((t - t1) / (t2 - t1)) * b2y
        output.append((cx, cy))
    return output


class _SortedSet:
    """The parts of ``SortedSet<double>`` the fit uses: ordered, unique, exact-value remove."""

    def __init__(self, values: Sequence[float]) -> None:
        self._values = sorted(set(values))

    def __len__(self) -> int:
        return len(self._values)

    def __iter__(self) -> Iterator[float]:
        return iter(self._values)

    def add(self, value: float) -> None:
        index = bisect.bisect_left(self._values, value)
        if index == len(self._values) or self._values[index] != value:
            self._values.insert(index, value)

    def remove(self, value: float) -> None:
        index = bisect.bisect_left(self._values, value)
        if index < len(self._values) and self._values[index] == value:
            del self._values[index]


def _try_add_t_points_above_threshold(
    output: Sequence[Point], t_points: _SortedSet, angle_threshold_degrees: float = 10.0
) -> bool:
    """``CurveExtensions.TryAddTPointsAboveThreshold``: add a t midway where the curve turns sharply. False if none added."""
    t_array = list(t_points)
    points = list(output)
    i = 1
    while i < len(points) - 1:
        if (_distance_squared(points[i - 1], points[i]) < EPSILON_SQUARED
                or _distance_squared(points[i], points[i + 1]) < EPSILON_SQUARED):
            del points[i]
            t_points.remove(t_array[i])
            del t_array[i]
            i -= 1
        i += 1

    degrees = [abs(value) for value in _measure_curvature(points)]
    one_degree = math.pi * 2.0 / 360
    threshold = one_degree * angle_threshold_degrees
    distance_threshold = 0.0625

    starting_points = len(t_array)
    needs_interpolation = [False] * len(t_array)
    for i in range(len(t_array) - 2, 0, -1):
        if degrees[i] > threshold:
            distance = _distance_squared(points[i - 1], points[i]) + _distance_squared(points[i], points[i + 1])
            needs_interpolation[i] = distance > distance_threshold

    for i in range(len(needs_interpolation) - 1):
        if needs_interpolation[i] or needs_interpolation[i + 1]:
            t_points.add((t_array[i] + t_array[i + 1]) / 2.0)

    return starting_points != len(t_points)


def recursively_fit_curve_segment(p0: Point, p1: Point, p2: Point, p3: Point, interpolations: int = 5) -> List[Point]:
    """``CatmullRom.RecursivelyFitCurveSegment``: the curve from ``p1`` to ``p2``, refined where it bends."""
    if interpolations == 1:
        interpolations = 2
    t_points = _SortedSet([i / (interpolations - 1.0) for i in range(interpolations)])
    t0 = 0.0
    t1 = _tj(t0, p0, p1)
    t2 = _tj(t1, p1, p2)
    while True:
        t_values = [t1 + scalar * (t2 - t1) for scalar in t_points]
        output = _fit_segment_with_t_values(p0, p1, p2, p3, t_values)
        if not _try_add_t_points_above_threshold(output, t_points):
            return output


def _closed_control_points(points: List[Point]) -> List[Point]:
    """``CatmullRom.GetControlPointsForClosedCurve``: wrap the ring so every segment has four control points."""
    i = 1
    while i < len(points) - 1:
        if _distance_squared(points[i - 1], points[i]) < EPSILON_SQUARED:
            del points[i]
            i -= 1
        i += 1
    output = list(points)
    if not _equal(output[0], output[-1]):
        output.insert(0, output[-1])
    after_start = output[1]
    before_start = output[-2]
    output.insert(0, before_start)
    output.append(after_start)
    return output


def _open_control_points(points: List[Point]) -> List[Point]:
    """``CatmullRom.GetControlPointsForOpenCurve``: extrapolate a control point before the start and after the end."""
    output = list(points)
    output.insert(0, _point_along_line(output[0], output[1], -0.5))
    output.append(_point_along_line(output[-2], output[-1], 1.5))
    return output


def _remove_adjacent_duplicates(points: Sequence[Point], preserved: Sequence[Point]) -> List[Point]:
    """``PrimitiveExtensions.RemoveAdjacentDuplicates(points, preserved_path)``, including its exact-match quirks."""
    if not points:
        raise ValueError("Must have at least one point to remove adjacent duplicates")
    result: List[Point] = []
    iterator = iter(points)
    kept_iterator = iter(preserved)
    preserved_point = next(kept_iterator, _NAN)
    p = next(iterator)
    following = _NAN
    count = 0
    for following in iterator:
        if p[0] == preserved_point[0] and p[1] == preserved_point[1]:
            while _equal(following, preserved_point):
                following = next(iterator, _NAN)
            result.append(p)
            preserved_point = next(kept_iterator, _NAN)
        elif _equal(p, preserved_point):
            if not _equal(following, preserved_point):
                result.append(preserved_point)
                preserved_point = next(kept_iterator, _NAN)
        elif not _equal(p, following):
            result.append(p)
        p = following
        count += 1
    if count == 0:
        result.append(p)
    if count > 0:
        result.append(following)
    return result


def fit_curve(control_points: Sequence[Point], interpolations: int, closed: bool) -> List[Point]:
    """``CatmullRom.FitCurve``: the fitted outline through ``control_points``.

    Two points or fewer, or ``interpolations`` 0, return the control points unchanged (straight segments). A closed curve
    may give its ring with or without the first point repeated at the end; either draws the same outline.
    """
    points = [(float(x), float(y)) for x, y in control_points]
    if len(points) <= 2 or interpolations == 0:
        return points
    wrapped = _closed_control_points(list(points)) if closed else _open_control_points(list(points))
    curved: List[Point] = []
    for i in range(len(wrapped) - 3):
        curved.extend(
            recursively_fit_curve_segment(wrapped[i], wrapped[i + 1], wrapped[i + 2], wrapped[i + 3], interpolations)
        )
    return _remove_adjacent_duplicates(curved, points)
