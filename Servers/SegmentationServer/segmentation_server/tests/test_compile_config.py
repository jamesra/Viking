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

def test_a_misspelled_flag_is_off_and_is_logged_not_silently_on(monkeypatch, caplog) -> None:
    from segmentation_server.compile_config import env_compile_image_encoder_enabled, env_flag_enabled

    monkeypatch.setenv("SAM2_COMPILE_IMAGE_ENCODER", "ture")
    with caplog.at_level("WARNING"):
        assert env_compile_image_encoder_enabled() is False
    assert any("SAM2_COMPILE_IMAGE_ENCODER" in r.getMessage() and "off" in r.getMessage() for r in caplog.records)
    monkeypatch.setenv("SOME_FLAG", "maybe")
    assert env_flag_enabled("SOME_FLAG", default=True) is False


def test_recognised_values_and_unset_keep_their_meaning(monkeypatch) -> None:
    from segmentation_server.compile_config import env_compile_image_encoder_enabled, env_flag_enabled

    monkeypatch.delenv("SAM2_COMPILE_IMAGE_ENCODER", raising=False)
    assert env_compile_image_encoder_enabled() is True
    for on in ("1", "true", "YES", " on "):
        monkeypatch.setenv("SAM2_COMPILE_IMAGE_ENCODER", on)
        assert env_compile_image_encoder_enabled() is True
    for off in ("0", "false", "No", "off", ""):
        monkeypatch.setenv("SAM2_COMPILE_IMAGE_ENCODER", off)
        assert env_compile_image_encoder_enabled() is False
    monkeypatch.delenv("UNSET_FLAG", raising=False)
    assert env_flag_enabled("UNSET_FLAG", default=False) is False
