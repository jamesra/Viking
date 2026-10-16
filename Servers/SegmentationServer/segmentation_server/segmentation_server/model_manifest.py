"""The model manifest: what a served model is and how it must be fed, read from the files that travel with it.

``<weights stem>.model.json`` sits next to the checkpoint (``SAM2_CHECKPOINT``). It names the model and its version,
the training data it saw, whether it takes tiles or the whole screen, the mask-prompt encoding it was trained with, and
what a client must do to its masks before it sends them. The trainer writes one for every export; a model that
predates the exporter (v2, v3) has a hand-written one. The format is documented in the trainer repository
(``docs/model_manifest.md``).

When there is no ``.model.json`` the older ``<stem>.prompt_encoding.json`` supplies the encoding. When there is neither,
the model is treated as a tiled model with the legacy flat mask prompt, as before manifests existed, and a warning says
so. A manifest the server cannot honour (an unknown encoding or capability value) stops the server from starting:
serving a model with a prompt it was not trained on degrades masks without any error.
"""

from __future__ import annotations

import json
import logging
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Mapping, Optional

from segmentation_grpc import ModelCapabilities, ModelInfo, ResolutionMode, SubmissionMode

logger = logging.getLogger(__name__)

SCHEMA_VERSION = 1
MANIFEST_SUFFIX = ".model.json"
ENCODING_RECORD_SUFFIX = ".prompt_encoding.json"
LEGACY_ENCODING = "legacy-flat-v1"
UNKNOWN = "unknown"

_SUBMISSION = {
    "tile_grid": SubmissionMode.SUBMISSION_MODE_FIXED_TILE_GRID,
    "full_viewport": SubmissionMode.SUBMISSION_MODE_FULL_VIEWPORT,
}
_RESOLUTION = {
    "single": ResolutionMode.RESOLUTION_MODE_SINGLE,
    "multi": ResolutionMode.RESOLUTION_MODE_MULTI,
}


class ManifestError(ValueError):
    """A manifest the server cannot honour. Raised at start-up so the process refuses to serve the model."""


@dataclass(frozen=True)
class ModelManifest:
    """The facts about the served model that the server and its clients use."""

    model_id: str = UNKNOWN
    model_version: str = UNKNOWN
    display_name: str = UNKNOWN
    description: str = ""
    """What the operator wrote about the model in its manifest. Empty when nothing was written."""
    prompt_encoding: str = LEGACY_ENCODING
    submission_mode: str = "tile_grid"
    resolution_mode: str = "single"
    training_data_set: str = UNKNOWN
    training_data_fingerprint: str = UNKNOWN
    requires_model_id: bool = False
    client_contract: Mapping[str, Any] = field(default_factory=dict)
    encoding_record: Mapping[str, Any] = field(default_factory=dict)
    source: str = "none"
    """Where it came from: the manifest file, the encoding record, or ``none`` (the legacy default)."""

    def capabilities(self) -> ModelCapabilities:
        return ModelCapabilities(
            submission_mode=_SUBMISSION[self.submission_mode],
            resolution_mode=_RESOLUTION[self.resolution_mode],
        )

    def to_model_info(self) -> ModelInfo:
        return ModelInfo(
            model_id=self.model_id,
            model_version=self.model_version,
            display_name=self.display_name,
            description=self.description,
            prompt_encoding=self.prompt_encoding,
            training_data_set=self.training_data_set,
            training_data_fingerprint=self.training_data_fingerprint,
            contract_json=json.dumps(dict(self.client_contract), sort_keys=True),
        )

    def describe(self) -> str:
        """One line for the start-up log."""
        return (
            f"model {self.model_id} ({self.model_version}); {self.submission_mode}/{self.resolution_mode}; "
            f"encoding {self.prompt_encoding}; training data {self.training_data_set} "
            f"[{self.training_data_fingerprint}]; manifest from {self.source}"
        )


def default_manifest() -> ModelManifest:
    """A tiled model with the legacy flat mask prompt: what the server assumed before manifests existed."""
    return ModelManifest()


def manifest_path(checkpoint: str | Path) -> Path:
    checkpoint = Path(checkpoint)
    return checkpoint.with_name(f"{checkpoint.stem}{MANIFEST_SUFFIX}")


def _section(raw: Mapping[str, Any], name: str, *, required: bool = True) -> Mapping[str, Any]:
    value = raw.get(name)
    if value is None:
        if required:
            raise ManifestError(f"missing {name!r}")
        return {}
    if not isinstance(value, Mapping):
        raise ManifestError(f"{name!r} must be an object")
    return value


def _text(section: Mapping[str, Any], key: str, default: str = UNKNOWN) -> str:
    value = section.get(key)
    return str(value) if value not in (None, "") else default


def parse_manifest(raw: object, *, source: str) -> ModelManifest:
    """A :class:`ModelManifest` from decoded JSON.

    Raises:
        ManifestError: The schema version, a required field, a capability value or the encoding is not one this
            server understands.
    """
    if not isinstance(raw, Mapping):
        raise ManifestError("the manifest must be a JSON object")
    if raw.get("schema_version") != SCHEMA_VERSION:
        raise ManifestError(f"schema_version must be {SCHEMA_VERSION}, got {raw.get('schema_version')!r}")
    model = _section(raw, "model")
    capabilities = _section(raw, "capabilities")
    server = _section(raw, "server_contract")
    client = _section(raw, "client_contract")
    data = _section(raw, "training_data", required=False)
    record = _section(raw, "prompt_encoding", required=False)

    submission = capabilities.get("submission_mode")
    resolution = capabilities.get("resolution_mode")
    if submission not in _SUBMISSION:
        raise ManifestError(f"capabilities.submission_mode {submission!r} is not one of {sorted(_SUBMISSION)}")
    if resolution not in _RESOLUTION:
        raise ManifestError(f"capabilities.resolution_mode {resolution!r} is not one of {sorted(_RESOLUTION)}")
    encoding = server.get("encoding")
    if not isinstance(encoding, str) or not encoding:
        raise ManifestError("server_contract.encoding must name the mask-prompt encoding")
    model_id = _text(model, "model_id", "")
    if not model_id:
        raise ManifestError("model.model_id is required")
    requires = client.get("requires_model_id", False)
    if not isinstance(requires, bool):
        raise ManifestError("client_contract.requires_model_id must be true or false")
    return ModelManifest(
        model_id=model_id,
        model_version=_text(model, "model_version"),
        display_name=_text(model, "display_name", model_id),
        description=model.get("description") if isinstance(model.get("description"), str) else "",
        prompt_encoding=encoding,
        submission_mode=submission,
        resolution_mode=resolution,
        training_data_set=_text(data, "set"),
        training_data_fingerprint=_text(data, "fingerprint"),
        requires_model_id=requires,
        client_contract=dict(client),
        encoding_record=dict(record),
        source=source,
    )


def _read_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ManifestError(f"cannot read {path}: {exc}") from exc


def load_model_manifest(checkpoint: Optional[str | Path]) -> ModelManifest:
    """The manifest that goes with ``checkpoint``.

    Order: ``<stem>.model.json``, then ``<stem>.prompt_encoding.json`` (the encoding only), then the legacy default.

    Raises:
        ManifestError: A file is present but unreadable or not acceptable. A missing file is not an error.
    """
    if checkpoint is None:
        return default_manifest()
    checkpoint = Path(checkpoint)
    path = manifest_path(checkpoint)
    if path.is_file():
        try:
            return parse_manifest(_read_json(path), source=path.name)
        except ManifestError as exc:
            raise ManifestError(f"{path}: {exc}") from exc
    record_path = checkpoint.with_name(f"{checkpoint.stem}{ENCODING_RECORD_SUFFIX}")
    if record_path.is_file():
        record = _read_json(record_path)
        if not isinstance(record, Mapping) or not isinstance(record.get("prompt_encoding"), str):
            raise ManifestError(f"{record_path}: no 'prompt_encoding' version string")
        logger.warning(
            "%s has no %s; using the encoding named in %s and treating the model as unnamed",
            checkpoint.name, MANIFEST_SUFFIX, record_path.name,
        )
        return ModelManifest(prompt_encoding=record["prompt_encoding"], encoding_record=dict(record), source=record_path.name)
    logger.warning(
        "%s has no %s or %s; treating it as a tiled model with the legacy flat mask prompt",
        checkpoint.name, MANIFEST_SUFFIX, ENCODING_RECORD_SUFFIX,
    )
    return default_manifest()
