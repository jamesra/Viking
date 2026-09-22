"""Eager serving while image-encoder compile runs. No GPU or real torch required."""

from __future__ import annotations

import sys
import threading
from unittest.mock import MagicMock


class _Device:
    def __init__(self, kind: str) -> None:
        self.type = kind


def _install_import_stubs() -> None:
    """Let segmentation_service import on machines without torch or SAM2."""
    existing = sys.modules.get("torch")
    if existing is not None and not isinstance(existing, MagicMock):
        return
    torch = MagicMock()
    torch.cuda.is_available.return_value = True
    torch.cuda.get_device_properties.return_value.major = 8
    torch.backends.mps.is_available.return_value = False
    torch.device = _Device
    sys.modules["torch"] = torch
    sam2 = MagicMock()
    sam2.__file__ = "sam2/__init__.py"
    sys.modules["sam2"] = sam2
    sys.modules.setdefault("sam2.build_sam", MagicMock())
    sys.modules.setdefault("sam2.sam2_image_predictor", MagicMock())


_install_import_stubs()

import segmentation_server.segmentation_service as svc  # noqa: E402
from segmentation_server.compile_config import COMPILE_IMAGE_ENCODER_HYDRA  # noqa: E402
from segmentation_server.segmentation_service import (  # noqa: E402
    COMPILE_FAILED,
    COMPILE_OFF,
    COMPILE_READY,
    COMPILE_WARMING,
    SegmentationModel,
)


def test_resolve_prefers_sam2_checkpoint_env(monkeypatch) -> None:
    monkeypatch.setenv("SAM2_CHECKPOINT", "/models/best_TEM_model.pt")
    monkeypatch.delenv("SAM2_MODEL_CFG", raising=False)
    cfg, ckpt = svc._resolve_sam2_paths()
    assert ckpt == "/models/best_TEM_model.pt"
    assert cfg == svc.DEFAULT_MODEL_CFG


def _patch_builders(monkeypatch, build):
    monkeypatch.setattr(svc, "build_sam2", build)
    monkeypatch.setattr(svc, "SAM2ImagePredictor", lambda model: MagicMock(model=model))
    monkeypatch.setattr(svc, "_resolve_sam2_paths", lambda: ("cfg", "ckpt"))


def test_init_returns_eager_before_compile_finishes(monkeypatch) -> None:
    gate = threading.Event()
    compiled_entered = threading.Event()
    models: list[MagicMock] = []

    def build(*_args, **kwargs):
        overrides = list(kwargs.get("hydra_overrides_extra") or [])
        model = MagicMock()
        model.overrides = overrides
        if overrides:
            compiled_entered.set()
            assert gate.wait(2)
        models.append(model)
        return model

    _patch_builders(monkeypatch, build)
    ready = threading.Event()
    model = SegmentationModel(compile_image_encoder=True, on_compiled_ready=ready.set)

    assert compiled_entered.wait(2)
    assert model.compile_status == COMPILE_WARMING
    assert models[0].overrides == []
    assert model.sam2_model is models[0]
    assert not ready.is_set()
    assert len(models) == 1

    gate.set()
    assert model._compile_thread is not None
    model._compile_thread.join(2)
    assert model.compile_status == COMPILE_READY
    assert models[1].overrides == [COMPILE_IMAGE_ENCODER_HYDRA]
    assert models[1].forward_image.called
    assert model.sam2_model is models[1]
    assert ready.is_set()


def test_compile_failure_keeps_eager_model(monkeypatch) -> None:
    def build(*_args, **kwargs):
        overrides = list(kwargs.get("hydra_overrides_extra") or [])
        if overrides:
            raise RuntimeError("inductor failed")
        model = MagicMock()
        model.overrides = overrides
        return model

    _patch_builders(monkeypatch, build)
    model = SegmentationModel(compile_image_encoder=True)
    assert model._compile_thread is not None
    model._compile_thread.join(2)
    assert model.compile_status == COMPILE_FAILED
    assert model.sam2_model.overrides == []


def test_compile_disabled_does_not_start_background_thread(monkeypatch) -> None:
    builds: list[list[str]] = []

    def build(*_args, **kwargs):
        overrides = list(kwargs.get("hydra_overrides_extra") or [])
        builds.append(overrides)
        return MagicMock()

    _patch_builders(monkeypatch, build)
    model = SegmentationModel(compile_image_encoder=False)
    assert model.compile_status == COMPILE_OFF
    assert model._compile_thread is None
    assert builds == [[]]


def test_late_callback_runs_once_after_ready(monkeypatch) -> None:
    def build(*_args, **kwargs):
        return MagicMock()

    _patch_builders(monkeypatch, build)
    model = SegmentationModel(compile_image_encoder=True)
    assert model._compile_thread is not None
    model._compile_thread.join(2)
    assert model.compile_status == COMPILE_READY

    calls: list[int] = []
    model.set_on_compiled_ready(lambda: calls.append(1))
    model.set_on_compiled_ready(lambda: calls.append(2))
    assert calls == [1]


def test_non_cuda_does_not_compile(monkeypatch) -> None:
    torch = sys.modules["torch"]
    monkeypatch.setattr(torch.cuda, "is_available", lambda: False)
    monkeypatch.setattr(torch.backends.mps, "is_available", lambda: False)
    builds: list[list[str]] = []

    def build(*_args, **kwargs):
        builds.append(list(kwargs.get("hydra_overrides_extra") or []))
        return MagicMock()

    _patch_builders(monkeypatch, build)
    model = SegmentationModel(compile_image_encoder=True)
    assert model.compile_status == COMPILE_OFF
    assert model._compile_thread is None
    assert builds == [[]]
