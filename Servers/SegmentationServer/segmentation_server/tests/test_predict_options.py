"""mask_threshold, mask_input and the switched-off union in SegmentationModel.

Needs torch and sam2 (the server image has both), so it is skipped elsewhere. A fake predictor
stands in for SAM2ImagePredictor and records what predict() was asked.
"""

from __future__ import annotations

from contextlib import nullcontext
from typing import Any, List

import numpy as np
import pytest

pytest.importorskip("torch")
pytest.importorskip("sam2")

from hypothesis import given, settings, strategies as st  # noqa: E402

from segmentation_server.mask_utils import DEFAULT_MASK_THRESHOLD  # noqa: E402
from segmentation_server.segmentation_service import SegmentationModel  # noqa: E402

SIDE = 32


class FakePredictor:
    """Returns one fixed logit field and remembers each predict() call."""

    def __init__(self, logits: np.ndarray, score: float = 0.9) -> None:
        self._logits = logits.astype(np.float32)
        self._score = score
        self.calls: List[dict[str, Any]] = []

    def predict(self, **kwargs: Any):
        self.calls.append(kwargs)
        assert kwargs.get("return_logits") is True
        field = self._logits[None]
        low_res = np.zeros((1, 256, 256), dtype=np.float32)
        return field, np.array([self._score], dtype=np.float32), low_res


def _model() -> SegmentationModel:
    model = object.__new__(SegmentationModel)
    model._autocast = lambda: nullcontext()  # type: ignore[method-assign]
    return model


def _ramp() -> np.ndarray:
    """Logits that run -3..+3 left to right, so each threshold cuts at a known column."""
    row = np.linspace(-3.0, 3.0, SIDE, dtype=np.float32)
    return np.tile(row, (SIDE, 1))


def _predict(model: SegmentationModel, predictor: FakePredictor, **options: Any):
    return model._predict_with_logits(predictor, [(5, 5)], [1], False, **options)


def test_default_threshold_is_one_logit():
    masks, _scores, _logits = _predict(_model(), FakePredictor(_ramp()))
    assert DEFAULT_MASK_THRESHOLD == 1.0
    assert np.array_equal(masks[0], _ramp() > DEFAULT_MASK_THRESHOLD)


def test_an_explicit_zero_threshold_is_not_replaced_by_the_default():
    masks, _scores, _logits = _predict(_model(), FakePredictor(_ramp()), mask_threshold=0.0)
    assert np.array_equal(masks[0], _ramp() > 0.0)


def test_raising_the_threshold_shrinks_the_mask():
    model = _model()
    loose = _predict(model, FakePredictor(_ramp()), mask_threshold=-1.0)[0][0]
    default = _predict(model, FakePredictor(_ramp()))[0][0]
    strict = _predict(model, FakePredictor(_ramp()), mask_threshold=1.5)[0][0]
    assert strict.sum() < default.sum() < loose.sum()
    assert np.array_equal(strict, _ramp() > 1.5)


@settings(max_examples=40, deadline=None)
@given(
    low=st.floats(min_value=-5.0, max_value=5.0, allow_nan=False),
    high=st.floats(min_value=-5.0, max_value=5.0, allow_nan=False),
)
def test_masks_are_nested_as_the_threshold_rises(low: float, high: float):
    low, high = min(low, high), max(low, high)
    model = _model()
    larger = _predict(model, FakePredictor(_ramp()), mask_threshold=low)[0][0]
    smaller = _predict(model, FakePredictor(_ramp()), mask_threshold=high)[0][0]
    assert not np.any(smaller & ~larger)


def test_no_mask_input_by_default():
    predictor = FakePredictor(_ramp())
    _predict(_model(), predictor)
    assert len(predictor.calls) == 1
    assert "mask_input" not in predictor.calls[0]


def test_mask_input_runs_a_second_pass_with_the_first_passes_logits():
    predictor = FakePredictor(_ramp())
    _predict(_model(), predictor, use_mask_input=True)
    assert len(predictor.calls) == 2
    second = predictor.calls[1]
    assert second["mask_input"].shape == (1, 256, 256)
    assert second["multimask_output"] is False


def test_mask_input_is_ignored_for_multimask_output():
    predictor = FakePredictor(_ramp())
    _model()._predict_with_logits(predictor, [(5, 5)], [1], True, use_mask_input=True)
    assert len(predictor.calls) == 1


def test_tile_predict_returns_the_best_mask_without_a_union():
    """A second positive the best mask misses is no longer chased with an extra predict."""
    predictor = FakePredictor(_ramp())
    mask, logits, score = _model().predict_tile_union(
        predictor, [(SIDE - 2, 5), (1, 5)], [1, 1], False, (SIDE, SIDE)
    )
    assert len(predictor.calls) == 1
    assert np.array_equal(mask, _ramp() > DEFAULT_MASK_THRESHOLD)
    assert not mask[5, 1]
    assert logits is not None
    assert score == pytest.approx(0.9)


def test_tile_predict_threshold_reaches_the_mask():
    mask, _logits, _score = _model().predict_tile_union(
        FakePredictor(_ramp()), [(SIDE - 2, 5)], [1], False, (SIDE, SIDE), None, 1.5
    )
    assert np.array_equal(mask, _ramp() > 1.5)


class PromptAwarePredictor:
    """Answers a logit field chosen by whether the call carried a box and negative clicks.

    ``fields`` maps (has_box, has_negatives) to the logits to return; a missing key returns
    ``default``. Every call is recorded.
    """

    def __init__(self, fields: dict, default: np.ndarray) -> None:
        self._fields = fields
        self._default = default.astype(np.float32)
        self.calls: List[dict[str, Any]] = []

    def predict(self, **kwargs: Any):
        self.calls.append(kwargs)
        has_box = kwargs.get("box") is not None
        has_negatives = bool(np.any(np.asarray(kwargs["point_labels"]) == 0))
        field = self._fields.get((has_box, has_negatives), self._default)
        low_res = np.zeros((1, 256, 256), dtype=np.float32)
        return field.astype(np.float32)[None], np.array([0.8], dtype=np.float32), low_res


CLICK = (10, 10)
NEGATIVE = (25, 25)


def _field(object_at_click: bool) -> np.ndarray:
    """Positive logits everywhere except a hole at the click when ``object_at_click`` is False."""
    field = np.full((SIDE, SIDE), 3.0, dtype=np.float32)
    if not object_at_click:
        field[CLICK[1] - 3:CLICK[1] + 4, CLICK[0] - 3:CLICK[0] + 4] = -4.0
    return field


def _tile(predictor: PromptAwarePredictor, box=(5, 5, 20, 20)):
    return _model().predict_tile_union(
        predictor, [CLICK, NEGATIVE], [1, 0], False, (SIDE, SIDE), box
    )


def test_a_mask_that_holds_the_click_is_returned_without_retrying():
    predictor = PromptAwarePredictor({}, _field(True))
    mask, _logits, _score = _tile(predictor)
    assert len(predictor.calls) == 1
    assert mask[CLICK[1], CLICK[0]]


def test_box_that_leaves_the_click_out_is_dropped():
    predictor = PromptAwarePredictor({(True, True): _field(False)}, _field(True))
    mask, _logits, _score = _tile(predictor)
    assert len(predictor.calls) == 2
    assert predictor.calls[1].get("box") is None
    assert np.any(np.asarray(predictor.calls[1]["point_labels"]) == 0)
    assert mask[CLICK[1], CLICK[0]]


def test_negatives_are_dropped_when_the_box_was_not_the_cause():
    predictor = PromptAwarePredictor(
        {(True, True): _field(False), (False, True): _field(False)}, _field(True)
    )
    mask, _logits, _score = _tile(predictor)
    assert len(predictor.calls) == 3
    assert predictor.calls[2].get("box") is None
    assert not np.any(np.asarray(predictor.calls[2]["point_labels"]) == 0)
    assert mask[CLICK[1], CLICK[0]]


def test_without_a_box_the_first_retry_drops_the_negatives():
    predictor = PromptAwarePredictor({(False, True): _field(False)}, _field(True))
    mask, _logits, _score = _tile(predictor, box=None)
    assert len(predictor.calls) == 2
    assert not np.any(np.asarray(predictor.calls[1]["point_labels"]) == 0)
    assert mask[CLICK[1], CLICK[0]]


def test_no_retry_is_possible_for_a_lone_click_without_a_box():
    predictor = PromptAwarePredictor({}, _field(False))
    mask, _logits, _score = _model().predict_tile_union(
        predictor, [CLICK], [1], False, (SIDE, SIDE), None
    )
    assert len(predictor.calls) == 1
    assert not mask[CLICK[1], CLICK[0]]


def test_when_every_rung_misses_the_click_the_original_mask_comes_back():
    predictor = PromptAwarePredictor({}, _field(False))
    mask, _logits, _score = _tile(predictor)
    assert len(predictor.calls) == 3
    assert not mask[CLICK[1], CLICK[0]]
