"""Servicer helper tests that mock the SAM2 model."""

from __future__ import annotations

import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import grpc
import numpy as np
import pytest

from segmentation_grpc import Point
from segmentation_server.cuda_errors import UnrecoverableGpuError
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


def test_build_response_keeps_disconnected_blobs() -> None:
    mask = np.zeros((20, 20), dtype=np.bool_)
    mask[1:4, 1:4] = True
    mask[12:18, 12:18] = True
    model = MagicMock()
    servicer = SegmentationServicer(
        model=model,
        image_cache=MagicMock(),
        server_start_time=0.0,
    )
    response = servicer._build_segmentation_response(
        mask.astype(np.uint16),
        [{
            "index": 0,
            "score": 0.9,
            "mask": mask,
            "x": 1,
            "y": 1,
            "width": 17,
            "height": 17,
        }],
        20,
        20,
    )
    assert len(response.segments) == 1
    assert len(response.segments[0].polygons) >= 2


@pytest.mark.asyncio
async def test_unrecoverable_gpu_aborts_unavailable_and_schedules_exit() -> None:
    exited: list[int] = []
    servicer = SegmentationServicer(
        model=MagicMock(),
        image_cache=MagicMock(),
        server_start_time=0.0,
        exit_process=exited.append,
    )
    context = AsyncMock()
    await servicer._abort_unrecoverable_gpu(
        UnrecoverableGpuError("lost"), context, delay_seconds=0
    )
    await asyncio.sleep(0.05)
    assert exited == [1]
    context.abort.assert_awaited()
    assert context.abort.await_args.args[0] == grpc.StatusCode.UNAVAILABLE

def test_a_polygon_response_carries_the_hole_rings() -> None:
    mask = np.zeros((64, 64), dtype=np.bool_)
    mask[8:56, 8:56] = True
    mask[24:40, 24:40] = False
    model = MagicMock()
    servicer = SegmentationServicer(model=model, image_cache=MagicMock(), server_start_time=0.0)

    response = servicer._build_segmentation_response(
        mask.astype(np.uint16),
        [{"index": 0, "score": 0.9, "mask": mask, "x": 8, "y": 8, "width": 48, "height": 48}],
        64,
        64,
    )

    polygon = response.segments[0].polygons[0]
    assert len(polygon.points) >= 4
    assert len(polygon.holes) == 1
    hole_xs = [point.x for point in polygon.holes[0].points]
    outer_xs = [point.x for point in polygon.points]
    assert min(outer_xs) < min(hole_xs) and max(hole_xs) < max(outer_xs)
    assert len(polygon.holes[0].holes) == 0


def test_a_solid_polygon_has_no_holes_on_the_wire() -> None:
    mask = np.zeros((32, 32), dtype=np.bool_)
    mask[4:28, 4:28] = True
    model = MagicMock()
    servicer = SegmentationServicer(model=model, image_cache=MagicMock(), server_start_time=0.0)

    response = servicer._build_segmentation_response(
        mask.astype(np.uint16),
        [{"index": 0, "score": 0.9, "mask": mask, "x": 4, "y": 4, "width": 24, "height": 24}],
        32,
        32,
    )

    assert len(response.segments[0].polygons[0].holes) == 0

def test_multi_segment_points_are_ordered_by_id_so_the_prompt_is_deterministic() -> None:
    request = SimpleNamespace(foreground_points={9: Point(x=9, y=9), 0: Point(x=1, y=1), 3: Point(x=3, y=3)})

    coordinates, labels = _servicer()._extract_coordinates_and_labels_from_multi_request(request)

    assert coordinates == [(1, 1), (3, 3), (9, 9)]
    assert labels == [0, 1, 1]
