"""torch.compile helpers; no SAM2 or GPU required."""

from __future__ import annotations

from segmentation_server.compile_config import (
    COMPILE_IMAGE_ENCODER_HYDRA,
    env_compile_image_encoder_enabled,
    hydra_overrides_for_image_encoder,
)


def test_hydra_override_only_on_cuda_when_enabled() -> None:
    assert hydra_overrides_for_image_encoder(True, "cuda") == [COMPILE_IMAGE_ENCODER_HYDRA]


def test_hydra_override_empty_when_disabled_or_not_cuda() -> None:
    assert hydra_overrides_for_image_encoder(False, "cuda") == []
    assert hydra_overrides_for_image_encoder(True, "cpu") == []
    assert hydra_overrides_for_image_encoder(True, "mps") == []


def test_env_compile_flag(monkeypatch) -> None:
    monkeypatch.delenv("SAM2_COMPILE_IMAGE_ENCODER", raising=False)
    assert env_compile_image_encoder_enabled() is True
    monkeypatch.setenv("SAM2_COMPILE_IMAGE_ENCODER", "1")
    assert env_compile_image_encoder_enabled() is True
    monkeypatch.setenv("SAM2_COMPILE_IMAGE_ENCODER", "0")
    assert env_compile_image_encoder_enabled() is False
    monkeypatch.setenv("SAM2_COMPILE_IMAGE_ENCODER", "false")
    assert env_compile_image_encoder_enabled() is False
