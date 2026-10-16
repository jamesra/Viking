"""The model manifest travels with the weights and tells the server what the model is and how it is fed."""

from __future__ import annotations

import copy
import json
import logging
import os
from pathlib import Path

import pytest
from segmentation_grpc import ModelInfo, ResolutionMode, SubmissionMode

from segmentation_server import trainer_prompt_encoding as enc
from segmentation_server.model_manifest import (
    LEGACY_ENCODING,
    ManifestError,
    default_manifest,
    load_model_manifest,
    manifest_path,
    parse_manifest,
)
from segmentation_server.prompt_encodings import encoder_for


def v4_manifest() -> dict:
    """A manifest as the trainer writes it for a v4 export."""
    return {
        "schema_version": 1,
        "model": {
            "model_id": "v4",
            "model_version": "v4.3",
            "display_name": "SAM2 EM v4",
            "description": "Trained on the repopulated set; use for RPC1.",
            "epoch": 3,
        },
        "training_data": {"set": "v3", "fingerprint": "0123456789ab"},
        "capabilities": {"submission_mode": "tile_grid", "resolution_mode": "single"},
        "prompt_encoding": {"prompt_encoding": enc.PROMPT_ENCODING_VERSION, "SEED_LOGIT": 10.0},
        "client_contract": {
            "contract_version": 1,
            "circle_diameter_scale": 0.95,
            "mask_roles": ["seed", "correction"],
            "requires_model_id": True,
        },
        "server_contract": {"builds_mask_input": True, "encoding": enc.PROMPT_ENCODING_VERSION},
    }


def write(directory: Path, name: str, payload: object) -> Path:
    path = directory / name
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


def test_a_trainer_manifest_is_read_into_the_fields_a_client_needs() -> None:
    manifest = parse_manifest(v4_manifest(), source="x.model.json")

    assert manifest.model_id == "v4"
    assert manifest.model_version == "v4.3"
    assert manifest.description == "Trained on the repopulated set; use for RPC1."
    assert manifest.prompt_encoding == enc.PROMPT_ENCODING_VERSION
    assert manifest.training_data_set == "v3"
    assert manifest.training_data_fingerprint == "0123456789ab"
    assert manifest.requires_model_id is True
    assert manifest.capabilities().submission_mode == SubmissionMode.SUBMISSION_MODE_FIXED_TILE_GRID
    assert manifest.capabilities().resolution_mode == ResolutionMode.RESOLUTION_MODE_SINGLE


def test_the_model_info_carries_both_versions_and_the_client_contract_verbatim() -> None:
    info = parse_manifest(v4_manifest(), source="x").to_model_info()

    assert isinstance(info, ModelInfo)
    assert info.model_id == "v4" and info.model_version == "v4.3"
    assert info.description == "Trained on the repopulated set; use for RPC1."
    assert info.training_data_set == "v3" and info.training_data_fingerprint == "0123456789ab"
    assert json.loads(info.contract_json)["circle_diameter_scale"] == 0.95
    assert json.loads(info.contract_json)["mask_roles"] == ["seed", "correction"]
    assert ModelInfo.FromString(info.SerializeToString()) == info


def test_a_whole_screen_model_advertises_full_viewport() -> None:
    raw = v4_manifest()
    raw["capabilities"] = {"submission_mode": "full_viewport", "resolution_mode": "multi"}

    capabilities = parse_manifest(raw, source="x").capabilities()

    assert capabilities.submission_mode == SubmissionMode.SUBMISSION_MODE_FULL_VIEWPORT
    assert capabilities.resolution_mode == ResolutionMode.RESOLUTION_MODE_MULTI


@pytest.mark.parametrize(
    "mutate, fragment",
    [
        (lambda m: m.update(schema_version=2), "schema_version"),
        (lambda m: m["capabilities"].update(submission_mode="tiled"), "submission_mode"),
        (lambda m: m["capabilities"].update(resolution_mode="auto"), "resolution_mode"),
        (lambda m: m["server_contract"].pop("encoding"), "encoding"),
        (lambda m: m["model"].pop("model_id"), "model_id"),
        (lambda m: m.pop("capabilities"), "capabilities"),
        (lambda m: m["client_contract"].update(requires_model_id="yes"), "requires_model_id"),
    ],
)
def test_a_manifest_the_server_cannot_honour_is_refused(mutate, fragment: str) -> None:
    raw = copy.deepcopy(v4_manifest())
    mutate(raw)

    with pytest.raises(ManifestError, match=fragment):
        parse_manifest(raw, source="x")


def test_a_non_object_is_refused() -> None:
    with pytest.raises(ManifestError):
        parse_manifest([1, 2], source="x")


def test_the_manifest_is_found_next_to_the_checkpoint_by_stem(tmp_path: Path) -> None:
    write(tmp_path, "v4.3.model.json", v4_manifest())

    manifest = load_model_manifest(tmp_path / "v4.3.pt")

    assert manifest_path(tmp_path / "v4.3.pt").name == "v4.3.model.json"
    assert manifest.model_id == "v4"
    assert manifest.source == "v4.3.model.json"


def test_without_a_manifest_the_older_encoding_record_still_names_the_encoding(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    write(tmp_path, "m.prompt_encoding.json", {"prompt_encoding": enc.PROMPT_ENCODING_VERSION, "SEED_LOGIT": 9.0})

    with caplog.at_level(logging.WARNING):
        manifest = load_model_manifest(tmp_path / "m.pt")

    assert manifest.prompt_encoding == enc.PROMPT_ENCODING_VERSION
    assert manifest.encoding_record["SEED_LOGIT"] == 9.0
    assert manifest.model_id == "unknown"
    assert "no .model.json" in caplog.text


def test_with_no_files_at_all_the_model_is_the_legacy_tiled_default(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    with caplog.at_level(logging.WARNING):
        manifest = load_model_manifest(tmp_path / "best_TEM_model.pt")

    assert manifest == default_manifest()
    assert manifest.prompt_encoding == LEGACY_ENCODING
    assert manifest.capabilities().submission_mode == SubmissionMode.SUBMISSION_MODE_FIXED_TILE_GRID
    assert "legacy flat mask prompt" in caplog.text
    assert load_model_manifest(None) == default_manifest()


def test_an_unreadable_manifest_stops_the_server_instead_of_falling_back(tmp_path: Path) -> None:
    (tmp_path / "m.model.json").write_text("{not json", encoding="utf-8")

    with pytest.raises(ManifestError, match="m.model.json"):
        load_model_manifest(tmp_path / "m.pt")


def test_a_model_whose_encoding_the_server_does_not_implement_is_refused() -> None:
    raw = v4_manifest()
    raw["server_contract"]["encoding"] = "edge-confidence-v99"

    with pytest.raises(ManifestError, match="edge-confidence-v99"):
        encoder_for(parse_manifest(raw, source="x"))


def _trainer_repo() -> Path | None:
    candidates = [os.environ.get("SAM2_TRAINER_REPO"), str(Path(__file__).resolve().parents[4].parent / "Sam2Trainer")]
    for candidate in candidates:
        if candidate and (Path(candidate) / "annotation_crops" / "prompt_encoding.py").is_file():
            return Path(candidate)
    return None


def test_the_hand_written_v2_and_v3_examples_from_the_trainer_repo_load() -> None:
    repo = _trainer_repo()
    if repo is None:
        pytest.skip("the trainer repository is not next to this one (set SAM2_TRAINER_REPO)")
    examples = repo / "docs" / "model_manifest_examples"

    v2 = parse_manifest(json.loads((examples / "sam2_em_v2.model.json").read_text(encoding="utf-8")), source="v2")
    v3 = parse_manifest(json.loads((examples / "sam2_em_v3.model.json").read_text(encoding="utf-8")), source="v3")

    assert v2.capabilities().submission_mode == SubmissionMode.SUBMISSION_MODE_FULL_VIEWPORT
    assert v3.capabilities().submission_mode == SubmissionMode.SUBMISSION_MODE_FIXED_TILE_GRID
    assert v2.prompt_encoding == v3.prompt_encoding == LEGACY_ENCODING
    assert encoder_for(v2) is None and encoder_for(v3) is None  # legacy: the existing prompt path


def test_the_vendored_encoding_is_the_trainers_file() -> None:
    repo = _trainer_repo()
    if repo is None:
        pytest.skip("the trainer repository is not next to this one (set SAM2_TRAINER_REPO)")
    ours = Path(enc.__file__).read_bytes().replace(b"\r\n", b"\n")
    theirs = (repo / "annotation_crops" / "prompt_encoding.py").read_bytes().replace(b"\r\n", b"\n")

    assert ours == theirs, "trainer_prompt_encoding.py must be a byte-for-byte copy of annotation_crops/prompt_encoding.py"
