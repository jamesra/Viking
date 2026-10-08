"""mask_threshold, mask_input and the single-mask selection rule in SegmentationModel.

Needs torch and sam2 (the server image has both), so it is skipped elsewhere. A fake predictor
stands in for SAM2ImagePredictor and records what predict() was asked.
"""

from __future__ import annotations

import threading
from contextlib import nullcontext
from typing import Any, List

import numpy as np
import pytest

pytest.importorskip("torch")
pytest.importorskip("sam2")

from hypothesis import given, settings, strategies as st  # noqa: E402

from segmentation_server.mask_utils import DEFAULT_MASK_THRESHOLD, NoMatchingMask  # noqa: E402
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
    model._gpu_lock = threading.RLock()
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


class CandidatePredictor:
    """Returns several candidate logit fields at once, the way multimask_output does."""

    def __init__(self, fields: List[np.ndarray], scores: List[float]) -> None:
        self._fields = np.stack([f.astype(np.float32) for f in fields])
        self._scores = np.array(scores, dtype=np.float32)
        self.calls: List[dict[str, Any]] = []

    def predict(self, **kwargs: Any):
        self.calls.append(kwargs)
        low_res = np.zeros((len(self._scores), 256, 256), dtype=np.float32)
        return self._fields, self._scores, low_res


def _block(x0: int, y0: int, x1: int, y1: int) -> np.ndarray:
    """Logits that are confidently object inside the inclusive rectangle and confidently not outside."""
    field = np.full((SIDE, SIDE), -4.0, dtype=np.float32)
    field[y0:y1 + 1, x0:x1 + 1] = 4.0
    return field


def test_tile_predict_returns_the_one_selected_mask_with_its_logits_and_score():
    predictor = FakePredictor(_ramp())
    mask, logits, score = _model().predict_tile(
        predictor, [(SIDE - 2, 5)], [1], False, (SIDE, SIDE)
    )
    assert len(predictor.calls) == 1
    assert np.array_equal(mask, _ramp() > DEFAULT_MASK_THRESHOLD)
    assert logits is not None
    assert score == pytest.approx(0.9)


def test_tile_predict_threshold_reaches_the_mask():
    mask, _logits, _score = _model().predict_tile(
        FakePredictor(_ramp()), [(SIDE - 2, 5)], [1], False, (SIDE, SIDE), None, 1.5
    )
    assert np.array_equal(mask, _ramp() > 1.5)


def test_without_a_box_the_candidate_covering_the_most_clicks_wins_over_a_higher_scoring_one():
    wide = _block(0, 0, SIDE - 1, SIDE - 1)
    narrow = _block(2, 2, 8, 8)
    predictor = CandidatePredictor([narrow, wide], [0.99, 0.60])
    mask, logits, score = _model().predict_tile(
        predictor, [(4, 4), (20, 20), (25, 3)], [1, 1, 1], True, (SIDE, SIDE)
    )
    assert len(predictor.calls) == 1
    assert score == pytest.approx(0.60)
    assert mask[20, 20] and mask[3, 25]


def test_with_a_box_the_highest_scoring_candidate_that_covers_the_whole_box_wins():
    small = _block(10, 10, 14, 14)
    medium = _block(6, 6, 22, 22)
    large = _block(0, 0, SIDE - 1, SIDE - 1)
    predictor = CandidatePredictor([small, medium, large], [0.99, 0.80, 0.50])
    mask, _logits, score = _model().predict_tile(
        predictor, [(12, 12)], [1], True, (SIDE, SIDE), box=(8, 8, 20, 20)
    )
    assert score == pytest.approx(0.80)
    assert mask[7, 7] and not mask[3, 3]
    assert predictor.calls[0]["box"].tolist() == [8, 8, 20, 20]


def test_a_box_no_candidate_covers_is_no_match_after_exactly_one_predict():
    predictor = CandidatePredictor([_block(10, 10, 14, 14)], [0.9])
    with pytest.raises(NoMatchingMask):
        _model().predict_tile(predictor, [(12, 12)], [1], True, (SIDE, SIDE), box=(5, 5, 20, 20))
    assert len(predictor.calls) == 1


def test_a_click_no_candidate_covers_is_no_match_and_no_looser_prompt_is_tried():
    predictor = PromptAwarePredictor({}, _field(False))
    with pytest.raises(NoMatchingMask):
        _model().predict_tile(
            predictor, [CLICK, NEGATIVE], [1, 0], False, (SIDE, SIDE), box=None
        )
    assert len(predictor.calls) == 1


def test_a_box_is_not_dropped_to_make_a_click_fit():
    """The old ladder retried without the box when the best mask missed the click."""
    predictor = PromptAwarePredictor({(True, True): _field(False)}, _field(True))
    with pytest.raises(NoMatchingMask):
        _model().predict_tile(
            predictor, [CLICK, NEGATIVE], [1, 0], False, (SIDE, SIDE), box=(5, 5, 20, 20)
        )
    assert len(predictor.calls) == 1
    assert predictor.calls[0].get("box") is not None


def test_a_box_alone_is_a_valid_prompt_and_sends_no_empty_point_arrays():
    predictor = CandidatePredictor([_block(0, 0, SIDE - 1, SIDE - 1)], [0.9])
    mask, _logits, _score = _model().predict_tile(
        predictor, [], [], False, (SIDE, SIDE), box=(4, 4, 10, 10)
    )
    assert mask.all()
    assert predictor.calls[0]["point_coords"] is None
    assert predictor.calls[0]["point_labels"] is None


def test_no_prompt_at_all_is_an_empty_mask_without_calling_the_model():
    predictor = CandidatePredictor([_block(0, 0, 3, 3)], [0.9])
    mask, logits, score = _model().predict_tile(predictor, [], [], False, (SIDE, SIDE))
    assert not mask.any() and mask.shape == (SIDE, SIDE)
    assert logits is None and score == 0.0
    assert predictor.calls == []


def test_the_viewport_path_uses_the_same_rule_and_returns_a_single_segment():
    wide = _block(0, 0, SIDE - 1, SIDE - 1)
    narrow = _block(2, 2, 8, 8)
    predictor = CandidatePredictor([narrow, wide], [0.99, 0.60])
    labeled, segments = _model().segment_image_with_predictor(
        predictor, [(4, 4), (20, 20)], [1, 1], True, (SIDE, SIDE)
    )
    assert len(segments) == 1
    assert segments[0]["score"] == pytest.approx(0.60)
    assert int(labeled[20, 20]) == 1


def test_the_viewport_path_with_no_covered_click_raises_instead_of_returning_an_empty_answer():
    predictor = CandidatePredictor([_block(0, 0, 3, 3)], [0.9])
    with pytest.raises(NoMatchingMask):
        _model().segment_image_with_predictor(predictor, [(20, 20)], [1], True, (SIDE, SIDE))


def test_the_viewport_path_with_only_background_clicks_returns_every_candidate():
    predictor = CandidatePredictor([_block(0, 0, 5, 5), _block(10, 10, 15, 15)], [0.9, 0.8])
    _labeled, segments = _model().segment_image_with_predictor(
        predictor, [(30, 30)], [0], True, (SIDE, SIDE)
    )
    assert len(segments) == 2


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


class _ConcurrencyProbe(FakePredictor):
    """Counts how many predict()/set_image() calls overlap in time."""

    def __init__(self) -> None:
        super().__init__(_ramp())
        self._guard = threading.Lock()
        self.running = 0
        self.max_running = 0

    def _enter(self) -> None:
        with self._guard:
            self.running += 1
            self.max_running = max(self.max_running, self.running)
        import time

        time.sleep(0.02)
        with self._guard:
            self.running -= 1

    def predict(self, **kwargs: Any):
        self._enter()
        return super().predict(**kwargs)

    def set_image(self, _image: Any) -> None:
        self._enter()


def test_forwards_on_the_shared_model_never_overlap_across_predictors():
    """Several inference workers must not run SAM2 on the shared weights at once."""
    model = _model()
    probe = _ConcurrencyProbe()
    image = np.zeros((SIDE, SIDE, 3), dtype=np.uint8)

    def predict() -> None:
        model._predict_with_logits(probe, [(5, 5)], [1], False)

    def encode() -> None:
        model._encode_image(probe, image)

    threads = [threading.Thread(target=fn) for fn in (predict, encode, predict, encode, predict, encode)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join(10)

    assert probe.max_running == 1


def test_a_failed_encode_still_releases_the_gpu_lock():
    model = _model()

    class Boom:
        def set_image(self, _image: Any) -> None:
            raise RuntimeError("out of memory")

    with pytest.raises(RuntimeError):
        model._encode_image(Boom(), np.zeros((2, 2, 3), dtype=np.uint8))

    assert model._gpu_lock.acquire(blocking=False)
    model._gpu_lock.release()
