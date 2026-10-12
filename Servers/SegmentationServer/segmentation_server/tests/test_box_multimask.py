"""Box prompts can ask SAM2 for its three candidates and pick the best-scoring one that touches the box."""

from __future__ import annotations

import logging
import threading
from contextlib import nullcontext
from typing import Any, List

import numpy as np
import pytest

from segmentation_server.compile_config import BOX_MULTIMASK_ENV, env_box_multimask_enabled
from segmentation_server.mask_utils import NoMatchingMask
from test_eager_compile import svc  # the service module, imported with torch and SAM2 stubbed

SIDE = 64
BOX = (10, 10, 30, 30)
# A box the sub-part misses (it ends at 28) but the part and the whole reach.
FAR_BOX = (35, 35, 50, 50)
# A box none of the single-mask answer reaches.
MISS_BOX = (40, 40, 50, 50)


def _field(rect) -> np.ndarray:
    """A logit field that is +5 inside ``rect`` (x0, y0, x1, y1 inclusive) and -5 elsewhere."""
    x0, y0, x1, y1 = rect
    field = np.full((SIDE, SIDE), -5.0, dtype=np.float32)
    field[y0:y1 + 1, x0:x1 + 1] = 5.0
    return field


# A sub-part that scores best but misses the box, a part that covers it, and the whole.
SUB_PART = (12, 12, 28, 28)
PART = (5, 5, 40, 40)
WHOLE = (0, 0, 60, 60)


class FakePredictor:
    """Offers one candidate, or three when asked for multimask, and remembers what it was asked."""

    def __init__(self) -> None:
        self.calls: List[dict] = []

    def predict(self, **kwargs: Any):
        self.calls.append(kwargs)
        low_res = np.zeros((1, 256, 256), dtype=np.float32)
        if kwargs["multimask_output"]:
            masks = np.stack([_field(SUB_PART), _field(PART), _field(WHOLE)])
            scores = np.array([0.95, 0.80, 0.70], dtype=np.float32)
            return masks, scores, np.repeat(low_res, 3, axis=0)
        return _field(SUB_PART)[None], np.array([0.95], dtype=np.float32), low_res


def _model():
    model = object.__new__(svc.SegmentationModel)
    model._autocast = lambda: nullcontext()
    model._gpu_lock = threading.RLock()
    return model


def _predict(box, multimask_output=False):
    predictor = FakePredictor()
    result = _model().predict_tile(
        predictor, [(20, 20)], [1], multimask_output, (SIDE, SIDE), box=box, mask_threshold=0.0
    )
    return predictor, result


def test_the_trial_is_off_unless_the_environment_turns_it_on(monkeypatch) -> None:
    monkeypatch.delenv(BOX_MULTIMASK_ENV, raising=False)
    assert env_box_multimask_enabled() is False
    monkeypatch.setenv(BOX_MULTIMASK_ENV, "1")
    assert env_box_multimask_enabled() is True
    monkeypatch.setenv(BOX_MULTIMASK_ENV, "ture")
    assert env_box_multimask_enabled() is False


def test_with_the_trial_off_a_box_gets_one_candidate_and_it_answers_the_box_it_touches(monkeypatch) -> None:
    monkeypatch.delenv(BOX_MULTIMASK_ENV, raising=False)
    predictor = FakePredictor()

    mask, _logits, score = _model().predict_tile(
        predictor, [(20, 20)], [1], False, (SIDE, SIDE), box=BOX, mask_threshold=0.0
    )

    assert predictor.calls[0]["multimask_output"] is False
    assert score == pytest.approx(0.95)
    assert int(np.count_nonzero(mask)) == 17 * 17, "the sub-part fills two thirds of the box and is still the answer"


def test_with_the_trial_off_a_box_the_single_candidate_misses_is_refused(monkeypatch) -> None:
    monkeypatch.delenv(BOX_MULTIMASK_ENV, raising=False)
    predictor = FakePredictor()

    with pytest.raises(NoMatchingMask):
        _model().predict_tile(predictor, [(20, 20)], [1], False, (SIDE, SIDE), box=MISS_BOX, mask_threshold=0.0)


def test_with_the_trial_on_a_box_asks_for_three_and_takes_the_best_that_answers_it(monkeypatch) -> None:
    monkeypatch.setenv(BOX_MULTIMASK_ENV, "1")

    predictor, (mask, _logits, score) = _predict(FAR_BOX)

    assert predictor.calls[0]["multimask_output"] is True
    assert score == pytest.approx(0.80), "the 0.95 sub-part misses the box, so the 0.80 part wins over the 0.70 whole"
    assert int(np.count_nonzero(mask)) == (PART[2] - PART[0] + 1) * (PART[3] - PART[1] + 1)


def test_the_trial_does_not_change_a_prompt_without_a_box(monkeypatch) -> None:
    monkeypatch.setenv(BOX_MULTIMASK_ENV, "1")

    predictor, (_mask, _logits, score) = _predict(box=None)

    assert predictor.calls[0]["multimask_output"] is False
    assert score == pytest.approx(0.95)


def test_a_request_that_already_wants_multimask_keeps_it(monkeypatch) -> None:
    monkeypatch.delenv(BOX_MULTIMASK_ENV, raising=False)

    predictor, _result = _predict(BOX, multimask_output=True)

    assert predictor.calls[0]["multimask_output"] is True


def test_the_trial_skips_the_second_mask_input_pass(monkeypatch) -> None:
    monkeypatch.setenv(BOX_MULTIMASK_ENV, "1")
    predictor = FakePredictor()

    _model().predict_tile(
        predictor, [(20, 20)], [1], False, (SIDE, SIDE), box=BOX, mask_threshold=0.0, use_mask_input=True
    )

    assert len(predictor.calls) == 1, "mask_input refines a single mask, so it is not used with three candidates"


def test_each_candidate_is_logged_so_the_trial_can_be_judged_from_real_requests(monkeypatch, caplog) -> None:
    monkeypatch.setenv(BOX_MULTIMASK_ENV, "1")

    with caplog.at_level(logging.INFO, logger=svc.logger.name):
        _predict(FAR_BOX)

    line = next(record.getMessage() for record in caplog.records if "Box prompt" in record.getMessage())
    assert "#0 score=0.950" in line and "touches_box=False" in line
    assert "#1 score=0.800" in line and "touches_box=True" in line
    assert "#2 score=0.700" in line


def test_a_positive_point_is_always_foreground_in_the_returned_mask() -> None:
    """The decoder can leave the clicked pixel out; the returned mask covers it anyway."""
    mask = np.zeros((SIDE, SIDE), dtype=np.bool_)
    forced = _model()._force_positive_points(mask, [(20, 20)])
    assert forced[20, 20] and forced[22, 20] and not forced[23, 20]
    assert not mask.any()


def test_no_positive_points_leaves_the_mask_alone() -> None:
    mask = np.zeros((SIDE, SIDE), dtype=np.bool_)
    assert not _model()._force_positive_points(mask, []).any()
