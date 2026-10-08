"""The numeric gate between the eager and the compiled image encoder."""

from __future__ import annotations

import numpy as np
from hypothesis import given, settings
from hypothesis import strategies as st
from hypothesis.extra import numpy as hnp

from segmentation_server.encoder_parity import (
    PARITY_MAX_REL_L2,
    PARITY_MIN_COSINE,
    compare_feature_maps,
)

_maps = hnp.arrays(
    dtype=np.float32,
    shape=st.tuples(st.integers(1, 4), st.integers(2, 6), st.integers(2, 6)),
    elements=st.floats(-10, 10, width=32),
).filter(lambda a: float(np.linalg.norm(a)) > 1e-3)


@settings(max_examples=40, deadline=None)
@given(_maps)
def test_identical_maps_agree(a) -> None:
    result = compare_feature_maps([a], [a.copy()])
    assert result.ok
    assert result.worst_cosine > 0.9999


@settings(max_examples=40, deadline=None)
@given(_maps, st.floats(0.0, 0.005))
def test_small_numeric_noise_is_accepted(a, scale) -> None:
    noise = np.random.default_rng(0).normal(0, scale * (np.abs(a).mean() + 1e-3), a.shape)
    assert compare_feature_maps([a], [a + noise.astype(np.float32)]).ok


@settings(max_examples=40, deadline=None)
@given(_maps)
def test_a_negated_map_is_rejected(a) -> None:
    assert not compare_feature_maps([a], [-a]).ok


def test_non_finite_values_are_rejected() -> None:
    a = np.ones((2, 3, 3), dtype=np.float32)
    for bad in (np.nan, np.inf, -np.inf):
        b = a.copy()
        b[0, 0, 0] = bad
        result = compare_feature_maps([a], [b])
        assert not result.ok
        assert "non-finite" in result.detail


def test_shape_and_count_mismatches_are_rejected() -> None:
    a = np.ones((2, 3, 3), dtype=np.float32)
    assert not compare_feature_maps([a], [np.ones((2, 3, 4), dtype=np.float32)]).ok
    assert not compare_feature_maps([a, a], [a]).ok
    assert not compare_feature_maps([], []).ok


def test_an_all_zero_candidate_against_a_nonzero_reference_is_rejected() -> None:
    a = np.ones((2, 3, 3), dtype=np.float32)
    assert not compare_feature_maps([a], [np.zeros_like(a)]).ok
    assert compare_feature_maps([np.zeros_like(a)], [np.zeros_like(a)]).ok


def test_the_worst_map_decides() -> None:
    good = np.arange(1, 19, dtype=np.float32).reshape(2, 3, 3)
    bad = good[::-1].copy()
    result = compare_feature_maps([good, good], [good, bad])
    assert not result.ok
    assert "map 1" in result.detail


def test_limits_are_the_documented_ones() -> None:
    assert PARITY_MIN_COSINE == 0.99
    assert PARITY_MAX_REL_L2 == 0.10
