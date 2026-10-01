"""ModelCapabilities: what GetServerStatus tells a client about how to submit input."""

from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import pytest
from hypothesis import given
from hypothesis import strategies as st
from segmentation_grpc import ModelCapabilities, ResolutionMode, ServerStatusResponse, SubmissionMode

from segmentation_server.image_cache import ImageCache
from segmentation_server.model_capabilities import SAM2_CAPABILITIES
from segmentation_server.server import SegmentationServicer

_SUBMISSION = st.sampled_from(list(SubmissionMode.values()))
_RESOLUTION = st.sampled_from(list(ResolutionMode.values()))


def test_the_sam2_tile_path_advertises_a_fixed_grid_at_one_resolution() -> None:
    assert SAM2_CAPABILITIES.submission_mode == SubmissionMode.SUBMISSION_MODE_FIXED_TILE_GRID
    assert SAM2_CAPABILITIES.resolution_mode == ResolutionMode.RESOLUTION_MODE_SINGLE


def test_a_model_never_advertises_an_unspecified_flag() -> None:
    assert SAM2_CAPABILITIES.submission_mode != SubmissionMode.SUBMISSION_MODE_UNSPECIFIED
    assert SAM2_CAPABILITIES.resolution_mode != ResolutionMode.RESOLUTION_MODE_UNSPECIFIED


def test_the_real_model_class_declares_its_capabilities() -> None:
    pytest.importorskip("sam2")
    from segmentation_server.segmentation_service import SegmentationModel

    assert SegmentationModel.capabilities == SAM2_CAPABILITIES


@given(submission=_SUBMISSION, resolution=_RESOLUTION)
def test_both_flags_survive_the_wire(submission: int, resolution: int) -> None:
    sent = ServerStatusResponse(
        capabilities=ModelCapabilities(submission_mode=submission, resolution_mode=resolution)
    )

    received = ServerStatusResponse.FromString(sent.SerializeToString())

    assert received.capabilities.submission_mode == submission
    assert received.capabilities.resolution_mode == resolution


@pytest.mark.asyncio
async def test_get_server_status_reports_the_model_capabilities() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    model = MagicMock()
    model.capabilities = ModelCapabilities(
        submission_mode=SubmissionMode.SUBMISSION_MODE_FULL_VIEWPORT,
        resolution_mode=ResolutionMode.RESOLUTION_MODE_MULTI,
    )
    servicer = SegmentationServicer(model=model, image_cache=cache, server_start_time=0.0)

    response = await servicer.GetServerStatus(MagicMock(), AsyncMock())

    assert response.HasField("capabilities")
    assert response.capabilities.submission_mode == SubmissionMode.SUBMISSION_MODE_FULL_VIEWPORT
    assert response.capabilities.resolution_mode == ResolutionMode.RESOLUTION_MODE_MULTI
