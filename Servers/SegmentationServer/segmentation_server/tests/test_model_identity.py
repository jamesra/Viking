"""A server says which model it serves, refuses requests built for another, and keeps whole-screen and tiled models apart."""

from __future__ import annotations

import json
from unittest.mock import AsyncMock, MagicMock

import grpc
import pytest
from segmentation_grpc import (
    BoundingBox,
    MaskRole,
    SegmentationRequest,
    SegmentTilesRequest,
    SubmissionMode,
    TileCoord,
    UploadTileRequest,
)
from segmentation_grpc.segmentation_pb2 import Feature, Mask

from segmentation_server.image_cache import ImageCache
from segmentation_server.model_manifest import ManifestError, ModelManifest, default_manifest, parse_manifest
from segmentation_server.prompt_encodings import EdgeConfidenceEncoder
from segmentation_server.server import SegmentationServicer, _prompts_from
from test_model_manifest import v4_manifest

FAILED = grpc.StatusCode.FAILED_PRECONDITION


def manifest(**changes) -> ModelManifest:
    raw = v4_manifest()
    if "submission_mode" in changes:
        raw["capabilities"]["submission_mode"] = changes.pop("submission_mode")
    if "requires_model_id" in changes:
        raw["client_contract"]["requires_model_id"] = changes.pop("requires_model_id")
    assert not changes
    return parse_manifest(raw, source="test")


def servicer(model_manifest: ModelManifest | None) -> SegmentationServicer:
    model = MagicMock()
    model.capabilities = (model_manifest or default_manifest()).capabilities()
    if model_manifest is not None:
        model.manifest = model_manifest
    return SegmentationServicer(
        model=model, image_cache=ImageCache(max_memory_bytes=1024, ttl_seconds=60), server_start_time=0.0
    )


def aborted_with(context: AsyncMock) -> grpc.StatusCode:
    return context.abort.await_args.args[0]


@pytest.fixture(autouse=True)
def _clean_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("SEGMENTATION_REQUIRE_MODEL_ID", raising=False)
    monkeypatch.delenv("SEGMENTATION_STRICT_SUBMISSION_MODE", raising=False)


@pytest.mark.asyncio
async def test_status_reports_the_model_its_data_and_the_client_contract() -> None:
    response = await servicer(manifest()).GetServerStatus(MagicMock(), AsyncMock())

    assert response.model.model_id == "v4"
    assert response.model.model_version == "v4.3"
    assert response.model.description == "Trained on the repopulated set; use for RPC1."
    assert response.model.training_data_set == "v3"
    assert response.model.prompt_encoding == manifest().prompt_encoding
    assert json.loads(response.model.contract_json)["mask_roles"] == ["seed", "correction"]


@pytest.mark.asyncio
async def test_a_model_without_a_manifest_reports_unknown_and_the_legacy_encoding() -> None:
    response = await servicer(None).GetServerStatus(MagicMock(), AsyncMock())

    assert response.model.model_id == "unknown"
    assert response.model.prompt_encoding == default_manifest().prompt_encoding


@pytest.mark.asyncio
async def test_a_request_built_for_another_model_is_refused_naming_both() -> None:
    context = AsyncMock()

    ok = await servicer(manifest())._model_id_ok(SegmentationRequest(model_id="v3"), context)

    assert ok is False
    assert aborted_with(context) == FAILED
    message = context.abort.await_args.args[1]
    assert "v4" in message and "v3" in message


@pytest.mark.asyncio
async def test_a_request_for_this_model_is_accepted() -> None:
    context = AsyncMock()

    assert await servicer(manifest())._model_id_ok(SegmentationRequest(model_id="v4"), context) is True
    context.abort.assert_not_awaited()


@pytest.mark.asyncio
async def test_a_request_with_no_model_id_is_refused_when_the_manifest_requires_one() -> None:
    context = AsyncMock()

    assert await servicer(manifest())._model_id_ok(SegmentationRequest(), context) is False
    assert aborted_with(context) == FAILED


@pytest.mark.asyncio
async def test_a_request_with_no_model_id_is_accepted_when_the_manifest_does_not_require_one() -> None:
    context = AsyncMock()

    assert await servicer(manifest(requires_model_id=False))._model_id_ok(SegmentationRequest(), context) is True
    context.abort.assert_not_awaited()


@pytest.mark.asyncio
async def test_the_environment_overrides_the_manifest_about_requiring_a_model_id(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("SEGMENTATION_REQUIRE_MODEL_ID", "0")
    assert await servicer(manifest())._model_id_ok(SegmentationRequest(), AsyncMock()) is True

    monkeypatch.setenv("SEGMENTATION_REQUIRE_MODEL_ID", "1")
    assert await servicer(manifest(requires_model_id=False))._model_id_ok(SegmentationRequest(), AsyncMock()) is False


@pytest.mark.asyncio
async def test_a_server_that_does_not_know_its_own_id_has_nothing_to_compare_and_accepts_any() -> None:
    context = AsyncMock()

    assert await servicer(None)._model_id_ok(SegmentationRequest(model_id="anything"), context) is True
    context.abort.assert_not_awaited()


@pytest.mark.asyncio
async def test_segment_image_refuses_a_wrong_model_before_doing_any_work() -> None:
    target = servicer(manifest())
    target._dispatch_segmentation = AsyncMock()
    context = AsyncMock()

    await target.SegmentImage(SegmentationRequest(model_id="v3"), context)

    assert aborted_with(context) == FAILED
    target._dispatch_segmentation.assert_not_awaited()


@pytest.mark.asyncio
async def test_a_tile_stream_start_for_another_model_is_refused() -> None:
    context = AsyncMock()
    request = SegmentTilesRequest(tiles=[TileCoord(volume="V", downsample=1)], model_id="v3")

    assert await servicer(manifest())._validate_stream_start(request, context) is None
    assert aborted_with(context) == FAILED


@pytest.mark.asyncio
async def test_a_whole_screen_model_refuses_the_tile_rpcs() -> None:
    target = servicer(manifest(submission_mode="full_viewport"))
    context = AsyncMock()

    response = await target.UploadTile(UploadTileRequest(), context)
    assert aborted_with(context) == FAILED and response is not None
    assert "whole-screen" in context.abort.await_args.args[1]

    stream_context = AsyncMock()
    request = SegmentTilesRequest(tiles=[TileCoord(volume="V", downsample=1)], model_id="v4")
    assert await target._validate_stream_start(request, stream_context) is None
    assert aborted_with(stream_context) == FAILED


@pytest.mark.asyncio
async def test_a_whole_screen_model_accepts_the_whole_screen_rpcs() -> None:
    context = AsyncMock()

    assert await servicer(manifest(submission_mode="full_viewport"))._submission_ok("SegmentImage", False, context)
    context.abort.assert_not_awaited()


@pytest.mark.asyncio
async def test_a_tiled_model_keeps_accepting_the_whole_screen_rpcs_unless_strict(monkeypatch: pytest.MonkeyPatch) -> None:
    target = servicer(manifest())
    relaxed = AsyncMock()
    assert await target._submission_ok("SegmentImage", False, relaxed) is True
    relaxed.abort.assert_not_awaited()

    monkeypatch.setenv("SEGMENTATION_STRICT_SUBMISSION_MODE", "1")
    strict = AsyncMock()
    assert await target._submission_ok("SegmentImage", False, strict) is False
    assert aborted_with(strict) == FAILED
    assert target.model.capabilities.submission_mode == SubmissionMode.SUBMISSION_MODE_FIXED_TILE_GRID


def mask_request(role: int | None) -> SegmentTilesRequest:
    feature = Feature(mask=Mask(bounds=BoundingBox(x_min=10, y_min=10, x_max=40, y_max=40)))
    if role is not None:
        feature.mask_role = role
    return SegmentTilesRequest(tiles=[TileCoord(volume="V", downsample=1)], foreground_feature=feature)


def test_the_mask_role_the_client_sent_reaches_the_mask_shape() -> None:
    assert _prompts_from(mask_request(MaskRole.MASK_ROLE_CORRECTION)).masks[0].role == "correction"
    assert _prompts_from(mask_request(MaskRole.MASK_ROLE_SEED)).masks[0].role == "seed"


def test_an_unspecified_mask_role_is_a_seed() -> None:
    assert _prompts_from(mask_request(None)).masks[0].role == "seed"


def test_the_servicer_builds_the_encoder_the_manifest_names() -> None:
    assert isinstance(servicer(manifest())._prompt_encoder, EdgeConfidenceEncoder)
    assert servicer(None)._prompt_encoder is None  # legacy: the existing prompt path


def test_a_manifest_naming_an_unknown_encoding_stops_the_server() -> None:
    raw = v4_manifest()
    raw["server_contract"]["encoding"] = "edge-confidence-v99"
    unknown = parse_manifest(raw, source="test")

    with pytest.raises(ManifestError):
        servicer(unknown)
