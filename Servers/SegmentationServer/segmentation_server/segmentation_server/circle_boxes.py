"""Prototype: recover each auto-segment circle from its nine clicks and box it from inside.

The client sends a circle as nine foreground clicks (``CircleSegmentationPrompts``): the
center, four points at 0.5 radius rotated 45 degrees, and four at 0.8 radius on the axes.
The server is not told the radius, so this finds those nine-point patterns, divides the
outer-ring distance by 0.8 to estimate the radius, and returns the square inscribed in
that circle, centered on the center click, as a SAM2 box prompt. Its corners sit on the
circle (one pixel inside, to absorb rounding), so the box never extends past the circle.
The prompt for a boxed cell is the box and the center click; the ring clicks are left out
of it (see ``CircleBox``).

A pattern needs both rings, so a click the user added by hand, or a polygon's centroid
prompts, never yields a box. Coordinates are mosaic pixels, X right, Y up.
"""

from __future__ import annotations

import math
import os
from dataclasses import dataclass
from typing import List, Optional, Sequence, Tuple

Point = Tuple[int, int]
# (x_min, y_min, x_max, y_max) in mosaic pixels, Y up.
Box = Tuple[int, int, int, int]

INNER_RING_FRACTION = 0.5
OUTER_RING_FRACTION = 0.8
# A smaller outer ring than this is a cluster of nearby user clicks, not a circle.
MIN_OUTER_RADIUS_PX = 8
# Positions are rounded to whole pixels and mapped through the section-to-volume transform,
# so each expected point may be off by a few pixels, more on a large circle.
MIN_TOLERANCE_PX = 3
TOLERANCE_FRACTION = 0.05


def outer_ring_box_enabled() -> bool:
    """Read SEGMENT_OUTER_RING_BOX. On unless set to 0, false, no or off."""
    raw = os.environ.get("SEGMENT_OUTER_RING_BOX")
    if raw is None or raw.strip() == "":
        return True
    return raw.strip().lower() not in ("0", "false", "no", "off")


@dataclass(frozen=True)
class CircleBox:
    """One recognised circle: the inscribed box and the clicks the box prompt replaces.

    A box prompt is sent with the center click only. The outer-ring clicks lie outside the
    inscribed box on the axes and tell SAM2 the object continues past it, and segmentations
    already tend to take too much territory, so ``ring_clicks`` lists all eight ring clicks
    so the prompt can leave them out and let the box set the extent.
    """

    box: Box
    inner_clicks: Tuple[Point, ...]
    outer_clicks: Tuple[Point, ...]

    @property
    def ring_clicks(self) -> Tuple[Point, ...]:
        """The eight clicks on the two rings, everything except the center."""
        return self.inner_clicks + self.outer_clicks


def outer_ring_boxes(points: Sequence[Point]) -> List[Box]:
    """One inscribed-square box per recognised circle."""
    return [circle.box for circle in find_circles(points)]


def find_circles(points: Sequence[Point]) -> List[CircleBox]:
    """Every nine-click circle in ``points``, as its box plus its ring clicks.

    Each click belongs to at most one circle. Order follows the order of the circle centers
    in ``points``.
    """
    clicks = [(int(x), int(y)) for x, y in points]
    used = [False] * len(clicks)
    circles: List[CircleBox] = []
    for center_index, (cx, cy) in enumerate(clicks):
        if used[center_index]:
            continue
        found = _circle_at(clicks, used, center_index, cx, cy)
        if found is None:
            continue
        members, circle = found
        for index in members:
            used[index] = True
        circles.append(circle)
    return circles


def _circle_at(
    clicks: Sequence[Point], used: Sequence[bool], center_index: int, cx: int, cy: int
) -> Optional[Tuple[List[int], CircleBox]]:
    for outer_index, (ox, oy) in enumerate(clicks):
        if outer_index == center_index or used[outer_index]:
            continue
        radius = ox - cx
        if radius < MIN_OUTER_RADIUS_PX or abs(oy - cy) > _tolerance(radius):
            continue

        tol = _tolerance(radius)
        outer_targets = [(cx - radius, cy), (cx, cy + radius), (cx, cy - radius)]
        inner = int(round(radius * INNER_RING_FRACTION / OUTER_RING_FRACTION / (2 ** 0.5)))
        inner_targets = [(cx + sx * inner, cy + sy * inner) for sx in (1, -1) for sy in (1, -1)]

        members = [center_index, outer_index]
        taken = set(members)
        for target in outer_targets + inner_targets:
            match = _nearest(clicks, used, taken, target, tol)
            if match is None:
                break
            taken.add(match)
            members.append(match)
        else:
            outer = [clicks[i] for i in (outer_index, *members[2:5])]
            outer_distance = sum(math.hypot(x - cx, y - cy) for x, y in outer) / len(outer)
            inner_clicks = tuple(clicks[i] for i in members[5:9])
            outer_clicks = tuple(clicks[i] for i in (members[1], *members[2:5]))
            return members, CircleBox(
                _inscribed_box(cx, cy, outer_distance), inner_clicks, outer_clicks
            )
    return None


def _inscribed_box(cx: int, cy: int, outer_distance: float) -> Box:
    """Square inscribed in the circle whose 0.8-radius ring is ``outer_distance`` from center.

    The half-side is radius / sqrt(2), less one pixel because the clicks were rounded.
    """
    radius = outer_distance / OUTER_RING_FRACTION
    half = max(1, int(math.floor(radius / math.sqrt(2.0) - 1.0)))
    return cx - half, cy - half, cx + half, cy + half


def _tolerance(radius: int) -> float:
    return max(float(MIN_TOLERANCE_PX), TOLERANCE_FRACTION * radius)


def _nearest(
    clicks: Sequence[Point],
    used: Sequence[bool],
    taken: set,
    target: Point,
    tolerance: float,
) -> Optional[int]:
    best: Optional[int] = None
    best_distance = tolerance * tolerance
    for index, (x, y) in enumerate(clicks):
        if used[index] or index in taken:
            continue
        distance = (x - target[0]) ** 2 + (y - target[1]) ** 2
        if distance <= best_distance:
            best = index
            best_distance = distance
    return best
