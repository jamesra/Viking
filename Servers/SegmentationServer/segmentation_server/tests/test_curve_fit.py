"""The Catmull-Rom port draws the outline Viking's client draws (Geometry/CurveFitting/CatmullRom.cs, FitCurve)."""

from __future__ import annotations

import json
import math
from pathlib import Path

import pytest

from segmentation_server.curve_fit import fit_curve

_REFERENCE = json.loads((Path(__file__).parent / "data" / "catmull_rom_reference.json").read_text(encoding="utf-8"))


@pytest.mark.parametrize("case", _REFERENCE["cases"], ids=lambda case: case["name"])
def test_fit_curve_reproduces_the_csharp_points(case) -> None:
    """Every point of the C# assembly's FitCurve, in order, to the last bit."""
    fitted = fit_curve([tuple(point) for point in case["control_points"]], case["interpolations"], case["closed"])

    assert [list(point) for point in fitted] == case["expected"]


def test_zero_interpolations_means_straight_segments() -> None:
    points = [(0.0, 0.0), (10.0, 0.0), (10.0, 10.0), (3.5, 7.25)]

    assert fit_curve(points, 0, True) == points
    assert fit_curve(points, 0, False) == points


def test_two_points_are_a_straight_line() -> None:
    assert fit_curve([(1.0, 2.0), (5.0, 9.0)], 8, False) == [(1.0, 2.0), (5.0, 9.0)]


def test_the_curve_passes_through_every_control_point() -> None:
    """FitCurve keeps the control points (the C# asserts it in debug builds)."""
    points = [(0.0, 0.0), (40.0, 5.0), (60.0, 40.0), (20.0, 55.0), (-5.0, 30.0)]

    fitted = fit_curve(points, 5, True)

    for control in points:
        assert any(math.dist(control, point) <= 1e-9 for point in fitted)
