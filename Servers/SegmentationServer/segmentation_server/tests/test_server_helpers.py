"""Servicer helper tests that mock the SAM2 model."""

from __future__ import annotations

from types import SimpleNamespace
from unittest.mock import MagicMock

from segmentation_grpc import Point
from segmentation_server.server import (
    _EMPTY_COORDINATES_MESSAGE,
    _EMPTY_FOREGROUND_POINTS_MESSAGE,
    SegmentationServicer,
)


def _servicer() -> SegmentationServicer:
    return SegmentationServicer(
        model=MagicMock(),
        image_cache=MagicMock(),
        server_start_time=0.0,
    )


def test_extract_segment_request_coordinates() -> None:
    request = SimpleNamespace(
        coordinates=[Point(x=1, y=2), Point(x=3, y=4)],
        labels=[1, 0],
    )
    coordinates, labels = _servicer()._extract_coordinates_and_labels_from_segment_request(request)
    assert coordinates == [(1, 2), (3, 4)]
    assert labels == [1, 0]


def test_extract_multi_request_treats_id_zero_as_background() -> None:
    request = SimpleNamespace(
        foreground_points={
            0: Point(x=5, y=6),
            7: Point(x=8, y=9),
        }
    )
    coordinates, labels = _servicer()._extract_coordinates_and_labels_from_multi_request(request)
    by_point = dict(zip(coordinates, labels))
    assert by_point[(5, 6)] == 0
    assert by_point[(8, 9)] == 1


def test_empty_message_constants_match_rpc() -> None:
    assert "coordinates" in _EMPTY_COORDINATES_MESSAGE.lower()
    assert "foreground_points" in _EMPTY_FOREGROUND_POINTS_MESSAGE
