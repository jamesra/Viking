"""Eager serving while image-encoder compile runs. No GPU or real torch required."""

from __future__ import annotations

import contextlib
import importlib
import importlib.util
import sys
import threading
from unittest.mock import MagicMock

import cv2
import numpy as np
import pytest


class _Device:
    def __init__(self, kind: str) -> None:
        self.type = kind


def _build_import_stubs() -> dict:
    """Stand-ins for torch and SAM2 on a machine that has neither; empty when real torch exists."""
    if importlib.util.find_spec("torch") is not None:
        return {}
    torch = MagicMock()
    torch.cuda.is_available.return_value = True
    torch.cuda.get_device_properties.return_value.major = 8
    torch.backends.mps.is_available.return_value = False
    torch.device = _Device
    sam2 = MagicMock()
    sam2.__file__ = "sam2/__init__.py"
    return {
        "torch": torch,
        "sam2": sam2,
        "sam2.build_sam": MagicMock(),
        "sam2.sam2_image_predictor": MagicMock(),
    }


_STUBS = _build_import_stubs()


@contextlib.contextmanager
def _stubs_installed():
    """Put the stubs in ``sys.modules`` and take them out again, so no other test module sees them.

    A test module that guards on ``pytest.importorskip("torch")`` would otherwise find the mock and
    run against it as if torch were installed.
    """
    saved = {name: sys.modules.get(name) for name in _STUBS}
    sys.modules.update(_STUBS)
    try:
        yield
    finally:
        for name, previous in saved.items():
            if previous is None:
                sys.modules.pop(name, None)
            else:
                sys.modules[name] = previous


with _stubs_installed():
    svc = importlib.import_module("segmentation_server.segmentation_service")

if _STUBS:
    # This copy of the module is bound to the stubs. Forget it so a later import builds its own.
    sys.modules.pop("segmentation_server.segmentation_service", None)
    import segmentation_server as _package

    if hasattr(_package, "segmentation_service"):
        delattr(_package, "segmentation_service")


@pytest.fixture(autouse=True)
def _torch_and_sam2_stubs():
    with _stubs_installed():
        yield


from segmentation_server.compile_config import COMPILE_IMAGE_ENCODER_HYDRA  # noqa: E402

COMPILE_FAILED = svc.COMPILE_FAILED
COMPILE_OFF = svc.COMPILE_OFF
COMPILE_READY = svc.COMPILE_READY
COMPILE_WARMING = svc.COMPILE_WARMING
SegmentationModel = svc.SegmentationModel


def test_resolve_prefers_sam2_checkpoint_env(monkeypatch) -> None:
    monkeypatch.setenv("SAM2_CHECKPOINT", "/models/best_TEM_model.pt")
    monkeypatch.delenv("SAM2_MODEL_CFG", raising=False)
    cfg, ckpt = svc._resolve_sam2_paths()
    assert ckpt == "/models/best_TEM_model.pt"
    assert cfg == svc.DEFAULT_MODEL_CFG


def _patch_builders(monkeypatch, build, features=None):
    """Stub the SAM2 builders. ``features(model)`` stands in for the encoder's feature maps."""
    monkeypatch.setattr(svc, "build_sam2", build)
    monkeypatch.setattr(svc, "SAM2ImagePredictor", lambda model: MagicMock(model=model))
    monkeypatch.setattr(svc, "_resolve_sam2_paths", lambda: ("cfg", "ckpt"))
    if features is None:
        features = lambda _model: [np.ones((2, 3, 3), dtype=np.float32)]  # noqa: E731
    monkeypatch.setattr(
        SegmentationModel, "_encoder_features", lambda self, model: features(model)
    )


def _png_bytes() -> bytes:
    ok, encoded = cv2.imencode(".png", np.zeros((8, 8, 3), dtype=np.uint8))
    assert ok
    return encoded.tobytes()


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


def test_cuda_enables_cudnn_benchmark(monkeypatch) -> None:
    torch = sys.modules["torch"]
    _patch_builders(monkeypatch, lambda *_args, **_kwargs: MagicMock())
    SegmentationModel(compile_image_encoder=False)
    assert torch.backends.cudnn.benchmark is True


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

def test_new_predictors_use_the_compiled_model_and_older_ones_keep_theirs(monkeypatch) -> None:
    built: list[MagicMock] = []

    def build(*_args, **kwargs):
        model = MagicMock()
        model.overrides = list(kwargs.get("hydra_overrides_extra") or [])
        built.append(model)
        return model

    _patch_builders(monkeypatch, build)
    gate = threading.Event()
    real_warmup = SegmentationModel._warmup_compiled_encoder
    monkeypatch.setattr(
        SegmentationModel,
        "_warmup_compiled_encoder",
        lambda self, model: (gate.wait(2), real_warmup(self, model))[1],
    )
    model = SegmentationModel(compile_image_encoder=True)
    png = _png_bytes()

    before = model.create_initialized_predictor(png)
    assert model.encoder_generation == "eager"
    assert before.model is built[0]
    assert SegmentationModel.predictor_generation(before) == "eager"

    gate.set()
    model._compile_thread.join(2)
    assert model.compile_status == COMPILE_READY
    assert model.encoder_generation == "compiled"

    after = model.create_initialized_predictor(png)
    assert after.model is built[1]
    assert SegmentationModel.predictor_generation(after) == "compiled"
    assert before.model is built[0]
    assert SegmentationModel.predictor_generation(before) == "eager"


def test_a_compiled_encoder_that_disagrees_with_eager_is_never_swapped_in(monkeypatch) -> None:
    built: list[MagicMock] = []

    def build(*_args, **kwargs):
        model = MagicMock()
        model.compiled = bool(kwargs.get("hydra_overrides_extra"))
        built.append(model)
        return model

    def features(model):
        if model.compiled:
            return [np.full((2, 3, 3), -1.0, dtype=np.float32)]
        return [np.ones((2, 3, 3), dtype=np.float32)]

    _patch_builders(monkeypatch, build, features)
    ready = threading.Event()
    model = SegmentationModel(compile_image_encoder=True, on_compiled_ready=ready.set)
    model._compile_thread.join(2)

    assert model.compile_status == COMPILE_FAILED
    assert model.encoder_generation == "eager"
    assert model.sam2_model is built[0]
    assert not ready.is_set()


def test_a_compiled_encoder_that_produces_nan_is_never_swapped_in(monkeypatch) -> None:
    def build(*_args, **kwargs):
        model = MagicMock()
        model.compiled = bool(kwargs.get("hydra_overrides_extra"))
        return model

    def features(model):
        value = np.nan if model.compiled else 1.0
        return [np.full((2, 3, 3), value, dtype=np.float32)]

    _patch_builders(monkeypatch, build, features)
    model = SegmentationModel(compile_image_encoder=True)
    model._compile_thread.join(2)

    assert model.compile_status == COMPILE_FAILED
    assert model.encoder_generation == "eager"


def test_the_parity_check_reads_the_eager_model_under_the_gpu_lock(monkeypatch) -> None:
    seen: list[bool] = []

    def build(*_args, **kwargs):
        model = MagicMock()
        model.compiled = bool(kwargs.get("hydra_overrides_extra"))
        return model

    def features(model):
        # RLock has no public "is owned" probe; a failed non-blocking acquire from another
        # thread shows whether this thread holds it.
        probe: list[bool] = []
        t = threading.Thread(target=lambda: probe.append(holder["m"]._gpu_lock.acquire(False)))
        t.start()
        t.join()
        if probe[0]:
            holder["m"]._gpu_lock.release()
        if not model.compiled:
            seen.append(not probe[0])
        return [np.ones((2, 3, 3), dtype=np.float32)]

    holder: dict = {}
    _patch_builders(monkeypatch, build, features)
    holder["m"] = model = SegmentationModel.__new__(SegmentationModel)
    SegmentationModel.__init__(model, compile_image_encoder=True)
    model._compile_thread.join(2)

    assert seen == [True]

def _plain_model(monkeypatch, pressure: bool):
    """A SegmentationModel with a CUDA-looking device and a controllable pressure check."""
    monkeypatch.setattr(svc, "SAM2ImagePredictor", lambda model: MagicMock(model=model))
    model = SegmentationModel.__new__(SegmentationModel)
    model.device = _Device("cuda")
    monkeypatch.setattr(SegmentationModel, "gpu_memory_under_pressure", lambda self: pressure)
    cache = MagicMock()
    monkeypatch.setattr(sys.modules["torch"].cuda, "empty_cache", cache)
    return model, cache


def test_releasing_a_predictor_leaves_the_allocator_cache_alone_when_memory_is_fine(monkeypatch) -> None:
    model, empty_cache = _plain_model(monkeypatch, pressure=False)
    predictor = MagicMock()

    model.release_predictor(predictor)

    predictor.reset_predictor.assert_called_once()
    empty_cache.assert_not_called()


def test_releasing_a_predictor_frees_the_allocator_cache_only_under_pressure(monkeypatch) -> None:
    model, empty_cache = _plain_model(monkeypatch, pressure=True)

    model.release_predictor(MagicMock())

    empty_cache.assert_called_once()


def test_releasing_nothing_does_nothing(monkeypatch) -> None:
    model, empty_cache = _plain_model(monkeypatch, pressure=True)
    model.release_predictor(None)
    empty_cache.assert_not_called()


class _RecordingStore:
    def __init__(self) -> None:
        self.saved: list[tuple[str, str]] = []
        self.touched: list[tuple[str, str]] = []
        self.loaded: list[tuple[str, str]] = []

    def try_load(self, digest, mode):
        self.loaded.append((digest, mode))
        return None

    def save(self, digest, blob, mode):
        self.saved.append((digest, mode))
        return True

    def touch(self, digest, mode):
        self.touched.append((digest, mode))

    def discard(self, digest, mode):
        pass


def _model_with_store(monkeypatch):
    monkeypatch.setattr(svc, "SAM2ImagePredictor", lambda model: MagicMock(model=model))
    model = SegmentationModel.__new__(SegmentationModel)
    model.device = _Device("cuda")
    model._swap_lock = threading.Lock()
    model._gpu_lock = threading.RLock()
    model.sam2_model = MagicMock()
    model._embedding_mode = "compiled"
    model._embedding_store = _RecordingStore()
    monkeypatch.setattr(SegmentationModel, "_export_disk_features", staticmethod(lambda predictor: b"blob"))
    return model


def test_a_tile_and_an_offset_window_with_the_same_pixels_share_one_disk_embedding(monkeypatch) -> None:
    model = _model_with_store(monkeypatch)
    source = np.random.default_rng(1).integers(0, 255, (16, 16, 3), dtype=np.uint8)
    ok, encoded = cv2.imencode(".png", source)
    assert ok
    pixels = svc.prepare_image_for_sam2(encoded.tobytes())  # what the server decodes the PNG to

    predictor = model.create_initialized_predictor(encoded.tobytes())
    predictor._features = {"image_embed": 1}
    predictor._orig_hw = [(16, 16)]
    model._save_disk_embedding(predictor, svc._image_digest(pixels), "compiled")
    model.create_initialized_predictor(encoded.tobytes(), pixels)

    digests = {digest for digest, _mode in model._embedding_store.loaded}
    assert digests == {svc._image_digest(pixels)}
    model.note_disk_embedding_used(encoded.tobytes(), pixels)
    assert model._embedding_store.touched == [(svc._image_digest(pixels), "compiled")]


def test_the_decoded_image_is_not_decoded_again_when_the_caller_has_it(monkeypatch) -> None:
    model = _model_with_store(monkeypatch)
    calls: list[int] = []
    real = svc.prepare_image_for_sam2
    monkeypatch.setattr(svc, "prepare_image_for_sam2", lambda data: calls.append(1) or real(data))
    pixels = np.zeros((16, 16, 3), dtype=np.uint8)
    ok, encoded = cv2.imencode(".png", pixels)

    model.create_initialized_predictor(encoded.tobytes(), pixels)
    model.note_disk_embedding_used(encoded.tobytes(), pixels)
    assert calls == []

    model.create_initialized_predictor(encoded.tobytes())
    assert calls == [1]


def test_the_embedding_key_scheme_is_part_of_the_stamp_so_old_folders_are_purged(tmp_path) -> None:
    from segmentation_server import embedding_store

    checkpoint = tmp_path / "model.pt"
    checkpoint.write_bytes(b"weights")
    stamp = embedding_store.checkpoint_stamp(str(checkpoint), "cfg")
    assert embedding_store.KEY_SCHEME == "pixels-v2"
    raw_old = f"cfg|{checkpoint.stat().st_size}|{checkpoint.stat().st_mtime_ns}"
    import hashlib

    assert stamp != hashlib.sha256(raw_old.encode()).hexdigest()[:32]

def test_an_embedding_blob_with_another_format_is_refused_not_installed() -> None:
    import types

    class _Tensor:
        def __init__(self, value):
            self._value = value

        def item(self):
            return self._value

    predictor = types.SimpleNamespace(device="cpu")
    good = {"format_version": _Tensor(svc.DISK_BLOB_FORMAT)}
    for bad in ({}, {"format_version": _Tensor(svc.DISK_BLOB_FORMAT + 1)}):
        try:
            SegmentationModel._install_disk_features(predictor, bad)
        except ValueError as error:
            assert "format" in str(error)
        else:
            raise AssertionError("an incompatible blob was installed")
    # a current blob gets past the format check and fails later only for lack of the tensors
    try:
        SegmentationModel._install_disk_features(predictor, good)
    except KeyError:
        pass
