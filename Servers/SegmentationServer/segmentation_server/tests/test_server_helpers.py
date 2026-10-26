"""Servicer helper tests that mock the SAM2 model."""

from __future__ import annotations

import asyncio
import math
from unittest.mock import AsyncMock, MagicMock

import grpc
import pytest

from segmentation_grpc import Vector2
from segmentation_server.cuda_errors import UnrecoverableGpuError
from segmentation_server.server import SegmentationServicer, _click
from segmentation_server.shapes import ShapeError


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


def test_a_click_is_the_pixel_it_falls_in() -> None:
    """Pixel (x, y) covers [x, x + 1) x [y, y + 1), so a click at its centre or anywhere inside it is that pixel."""
    assert _click(Vector2(x=10.5, y=20.5), "c") == (10, 20)
    assert _click(Vector2(x=10.0, y=20.999), "c") == (10, 20)
    assert _click(Vector2(x=-0.25, y=-1.0), "c") == (-1, -1)


def test_a_click_that_is_not_a_number_is_refused() -> None:
    with pytest.raises(ShapeError):
        _click(Vector2(x=math.inf, y=0), "foreground point 0")
