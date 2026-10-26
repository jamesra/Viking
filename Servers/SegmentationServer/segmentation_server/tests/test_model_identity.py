"""A server says which model it serves, refuses requests built for another, and encodes prompts the way it was trained."""

from __future__ import annotations

import json
from unittest.mock import AsyncMock, MagicMock

import grpc
import pytest
from segmentation_grpc import SegmentTilesRequest

from protocol import start
from segmentation_server.image_cache import ImageCache
from segmentation_server.model_manifest import ManifestError, ModelManifest, default_manifest, parse_manifest
from segmentation_server.prompt_encodings import DepthTrustEncoder, EdgeConfidenceEncoder
from segmentation_server.server import SegmentationServicer
from test_model_manifest import v4_manifest

FAILED = grpc.StatusCode.FAILED_PRECONDITION


def manifest(**changes) -> ModelManifest:
    raw = v4_manifest()
    if "encoding" in changes:
        raw["server_contract"]["encoding"] = changes.pop("encoding")
    if "circle_diameter_scale" in changes:
        raw["client_contract"]["circle_diameter_scale"] = changes.pop("circle_diameter_scale")
    if "requires_model_id" in changes:
        raw["client_contract"]["requires_model_id"] = changes.pop("requires_model_id")
    assert not changes
    return parse_manifest(raw, source="test")


def servicer(model_manifest: ModelManifest | None) -> SegmentationServicer:
    model = MagicMock()
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

    ok = await servicer(manifest())._model_id_ok(SegmentTilesRequest(model_id="v3"), context)

    assert ok is False
    assert aborted_with(context) == FAILED
    message = context.abort.await_args.args[1]
    assert "v4" in message and "v3" in message


@pytest.mark.asyncio
async def test_a_request_for_this_model_is_accepted() -> None:
    context = AsyncMock()

    assert await servicer(manifest())._model_id_ok(SegmentTilesRequest(model_id="v4"), context) is True
    context.abort.assert_not_awaited()


@pytest.mark.asyncio
async def test_a_request_with_no_model_id_is_refused_when_the_manifest_requires_one() -> None:
    context = AsyncMock()

    assert await servicer(manifest())._model_id_ok(SegmentTilesRequest(), context) is False
    assert aborted_with(context) == FAILED


@pytest.mark.asyncio
async def test_a_request_with_no_model_id_is_accepted_when_the_manifest_does_not_require_one() -> None:
    context = AsyncMock()

    assert await servicer(manifest(requires_model_id=False))._model_id_ok(SegmentTilesRequest(), context) is True
    context.abort.assert_not_awaited()


@pytest.mark.asyncio
async def test_the_environment_overrides_the_manifest_about_requiring_a_model_id(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("SEGMENTATION_REQUIRE_MODEL_ID", "0")
    assert await servicer(manifest())._model_id_ok(SegmentTilesRequest(), AsyncMock()) is True

    monkeypatch.setenv("SEGMENTATION_REQUIRE_MODEL_ID", "1")
    assert await servicer(manifest(requires_model_id=False))._model_id_ok(SegmentTilesRequest(), AsyncMock()) is False


@pytest.mark.asyncio
async def test_a_server_that_does_not_know_its_own_id_has_nothing_to_compare_and_accepts_any() -> None:
    context = AsyncMock()

    assert await servicer(None)._model_id_ok(SegmentTilesRequest(model_id="anything"), context) is True
    context.abort.assert_not_awaited()


@pytest.mark.asyncio
async def test_a_tile_stream_start_for_another_model_is_refused() -> None:
    context = AsyncMock()

    assert await servicer(manifest())._validate_stream_start(start([(1, 1)], model_id="v3"), context) is None
    assert aborted_with(context) == FAILED


def test_the_servicer_builds_the_encoder_the_manifest_names() -> None:
    assert type(servicer(manifest())._prompt_encoder) is EdgeConfidenceEncoder
    assert isinstance(servicer(manifest(encoding="edge-confidence-v7"))._prompt_encoder, DepthTrustEncoder)
    assert servicer(None)._prompt_encoder is None  # legacy: the existing prompt path


def test_the_circle_scale_comes_from_the_manifests_client_contract() -> None:
    assert servicer(manifest())._circle_scale == 0.95
    assert servicer(manifest(circle_diameter_scale=0.8))._circle_scale == 0.8
    assert servicer(None)._circle_scale == 1.0  # a model that says nothing was trained on circles as drawn


def test_under_v7_a_circle_claims_its_support_disc() -> None:
    """v7 trusts a circle out to zero_frac of its radius; that disc is the region the walk pins and splits."""
    assert servicer(manifest(encoding="edge-confidence-v7"))._circle_scale == pytest.approx(0.95)


def test_a_manifest_naming_an_unknown_encoding_stops_the_server() -> None:
    raw = v4_manifest()
    raw["server_contract"]["encoding"] = "edge-confidence-v99"
    unknown = parse_manifest(raw, source="test")

    with pytest.raises(ManifestError):
        servicer(unknown)
