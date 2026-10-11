"""A mask prompt (what neighboring windows found) reaches SAM2 as ``mask_input``, sized and clipped for it."""

from __future__ import annotations

import threading
from contextlib import nullcontext
from typing import Any, List

import numpy as np
import pytest

from test_eager_compile import svc  # the service module, imported with torch and SAM2 stubbed

SIDE = 64


class RecordingPredictor:
    """One candidate; remembers every predict() call."""

    def __init__(self) -> None:
        self.calls: List[dict] = []

    def predict(self, **kwargs: Any):
        self.calls.append(kwargs)
        field = np.full((SIDE, SIDE), -5.0, dtype=np.float32)
        field[10:30, 10:30] = 5.0
        low_res = np.full((1, 256, 256), 2.0, dtype=np.float32)
        return field[None], np.array([0.9], dtype=np.float32), low_res


def _model():
    model = object.__new__(svc.SegmentationModel)
    model._autocast = lambda: nullcontext()
    model._gpu_lock = threading.RLock()
    return model


def _predict(mask_logits=None, multimask=False, use_mask_input=False):
    predictor = RecordingPredictor()
    _model().predict_tile(
        predictor, [(20, 20)], [1], multimask, (SIDE, SIDE),
        mask_threshold=0.0, use_mask_input=use_mask_input, mask_logits=mask_logits,
    )
    return predictor


def test_without_a_mask_prompt_no_mask_input_is_sent() -> None:
    assert _predict().calls[0]["mask_input"] is None


def test_a_256_square_prompt_is_sent_as_is_with_a_leading_axis() -> None:
    prompt = np.zeros((256, 256), dtype=np.float32)
    prompt[50:100, 50:100] = 4.0

    sent = _predict(prompt).calls[0]["mask_input"]

    assert sent.shape == (1, 256, 256) and sent.dtype == np.float32
    assert np.array_equal(sent[0], prompt)


def test_a_prompt_of_another_size_is_resized_and_extreme_values_are_clipped() -> None:
    prompt = np.zeros((512, 512), dtype=np.float32)
    prompt[:256] = 500.0
    prompt[256:] = -500.0

    sent = _predict(prompt).calls[0]["mask_input"]

    assert sent.shape == (1, 256, 256)
    assert sent.max() == pytest.approx(20.0) and sent.min() == pytest.approx(-20.0)
    assert sent[0, 10, 10] == pytest.approx(20.0) and sent[0, 245, 10] == pytest.approx(-20.0)


def test_a_prompt_that_is_not_a_2d_map_is_refused() -> None:
    with pytest.raises(ValueError):
        _predict(np.zeros((3, 256, 256), dtype=np.float32))


def test_multimask_output_ignores_the_prompt_because_mask_input_is_one_mask() -> None:
    prompt = np.ones((256, 256), dtype=np.float32)

    assert _predict(prompt, multimask=True).calls[0]["mask_input"] is None


def test_the_second_refinement_pass_still_uses_the_first_passes_logits() -> None:
    prompt = np.ones((256, 256), dtype=np.float32)

    calls = _predict(prompt, use_mask_input=True).calls

    assert len(calls) == 2
    assert np.array_equal(calls[0]["mask_input"][0], prompt)
    assert np.allclose(calls[1]["mask_input"], 2.0), "the refinement pass is seeded with the first answer, as before"
