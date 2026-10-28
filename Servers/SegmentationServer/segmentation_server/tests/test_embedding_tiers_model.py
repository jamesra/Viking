"""The SAM2 model's side of the embedding tiers, on CPU tensors: sizes in real bytes, a host copy that round-trips
exactly, and release dropping the host copy. Needs torch and sam2 (the server image has both); no GPU."""

from __future__ import annotations

import pytest

torch = pytest.importorskip("torch")
pytest.importorskip("sam2")

from segmentation_server.segmentation_service import _HOST_COPY_ATTR, SegmentationModel  # noqa: E402


class FakePredictor:
    """The predictor state the tiers touch: SAM2ImagePredictor's _features and _orig_hw, and its device."""

    device = torch.device("cpu")

    def __init__(self) -> None:
        self._features = {
            "image_embed": torch.randn(1, 256, 64, 64),
            "high_res_feats": [
                torch.randn(1, 32, 256, 256, dtype=torch.bfloat16),
                torch.randn(1, 64, 128, 128, dtype=torch.bfloat16),
            ],
        }
        self._orig_hw = [(1024, 1024)]
        self._is_image_set = True
        self._is_batch = False

    def reset_predictor(self) -> None:
        self._features = None
        self._is_image_set = False


def _model() -> SegmentationModel:
    model = SegmentationModel.__new__(SegmentationModel)  # the tier methods need no loaded weights
    model.device = torch.device("cpu")
    return model


def test_sizes_are_the_real_tensor_bytes() -> None:
    model = _model()
    predictor = FakePredictor()
    expected = 256 * 64 * 64 * 4 + 32 * 256 * 256 * 2 + 64 * 128 * 128 * 2
    assert model.gpu_bytes(predictor) == expected
    _copy, host_bytes = model.export(predictor)
    assert host_bytes >= expected  # plus the few bytes of shape and format bookkeeping
    assert host_bytes - expected < 1024


def test_a_moved_embedding_comes_back_exactly() -> None:
    model = _model()
    predictor = FakePredictor()
    before = {
        "image_embed": predictor._features["image_embed"].clone(),
        "high": [t.clone() for t in predictor._features["high_res_feats"]],
    }
    host_copy, _bytes = model.export(predictor)
    model.drop_gpu(predictor, host_copy)
    assert predictor._features is None and not predictor._is_image_set
    assert model.gpu_bytes(predictor) == 0

    restored = model.restore(predictor)

    assert restored == model.gpu_bytes(predictor) > 0
    assert torch.equal(predictor._features["image_embed"], before["image_embed"])
    for got, want in zip(predictor._features["high_res_feats"], before["high"]):
        assert torch.equal(got, want) and got.dtype == want.dtype
    assert predictor._is_image_set and predictor._orig_hw == [(1024, 1024)]
    assert getattr(predictor, _HOST_COPY_ATTR) is None


def test_release_drops_the_host_copy() -> None:
    model = _model()
    predictor = FakePredictor()
    host_copy, _bytes = model.export(predictor)
    model.drop_gpu(predictor, host_copy)
    model.release_predictor(predictor)
    assert getattr(predictor, _HOST_COPY_ATTR) is None


def test_restore_without_a_host_copy_is_an_error() -> None:
    with pytest.raises(ValueError):
        _model().restore(FakePredictor())
