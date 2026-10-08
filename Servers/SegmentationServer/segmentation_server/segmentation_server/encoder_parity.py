"""Numeric agreement between the eager image encoder and its torch.compile replacement.

``torch.compile`` fuses kernels, so the compiled encoder is not bit-identical to the eager
one, and bf16 autocast makes the difference visible. What must never happen is a compiled
encoder that is *wrong* (NaNs, a wrong weight set, a recompiled graph that took a different
code path). This module judges that from the feature maps alone, with numpy only, so the
rule can be tested without torch or a GPU.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Sequence

import numpy as np
from numpy.typing import NDArray

# Cosine similarity of the flattened maps. Healthy eager-versus-compiled bf16 runs sit well
# above this; a wrong or half-initialized encoder falls far below it.
PARITY_MIN_COSINE = 0.99
# ||compiled - eager|| / ||eager||. Loose on purpose: it guards against gross error, not noise.
PARITY_MAX_REL_L2 = 0.10


class EncoderParityError(RuntimeError):
    """The compiled encoder disagrees with the eager one, so it must not be swapped in."""


@dataclass(frozen=True)
class ParityResult:
    """Outcome of comparing two lists of feature maps."""

    ok: bool
    worst_cosine: float
    worst_rel_l2: float
    detail: str


def compare_feature_maps(
    reference: Sequence[NDArray],
    candidate: Sequence[NDArray],
    min_cosine: float = PARITY_MIN_COSINE,
    max_rel_l2: float = PARITY_MAX_REL_L2,
) -> ParityResult:
    """Compare feature maps pairwise and report the worst agreement.

    Fails when the lists differ in length, a pair differs in shape, any value is not finite,
    or a pair's cosine similarity or relative L2 error is outside the limits.
    """
    if len(reference) != len(candidate):
        return ParityResult(
            False, 0.0, float("inf"),
            f"feature map count differs: eager={len(reference)} compiled={len(candidate)}",
        )
    if len(reference) == 0:
        return ParityResult(False, 0.0, float("inf"), "no feature maps to compare")

    worst_cosine = 1.0
    worst_rel_l2 = 0.0
    for index, (eager, compiled) in enumerate(zip(reference, candidate)):
        eager_arr = np.asarray(eager, dtype=np.float64)
        compiled_arr = np.asarray(compiled, dtype=np.float64)
        if eager_arr.shape != compiled_arr.shape:
            return ParityResult(
                False, 0.0, float("inf"),
                f"map {index} shape differs: eager={eager_arr.shape} compiled={compiled_arr.shape}",
            )
        if not np.all(np.isfinite(eager_arr)) or not np.all(np.isfinite(compiled_arr)):
            return ParityResult(False, 0.0, float("inf"), f"map {index} holds non-finite values")

        cosine, rel_l2 = _agreement(eager_arr.reshape(-1), compiled_arr.reshape(-1))
        worst_cosine = min(worst_cosine, cosine)
        worst_rel_l2 = max(worst_rel_l2, rel_l2)
        if cosine < min_cosine or rel_l2 > max_rel_l2:
            return ParityResult(
                False, worst_cosine, worst_rel_l2,
                f"map {index} disagrees: cosine={cosine:.5f} (min {min_cosine}) "
                f"rel_l2={rel_l2:.5f} (max {max_rel_l2})",
            )

    return ParityResult(
        True, worst_cosine, worst_rel_l2,
        f"{len(reference)} maps agree: worst cosine={worst_cosine:.5f} worst rel_l2={worst_rel_l2:.5f}",
    )


def _agreement(eager: NDArray, compiled: NDArray) -> "tuple[float, float]":
    """``(cosine similarity, relative L2 error)`` of two flat vectors."""
    eager_norm = float(np.linalg.norm(eager))
    compiled_norm = float(np.linalg.norm(compiled))
    if eager_norm == 0.0 and compiled_norm == 0.0:
        return 1.0, 0.0
    if eager_norm == 0.0 or compiled_norm == 0.0:
        return 0.0, float("inf")
    cosine = float(np.dot(eager, compiled) / (eager_norm * compiled_norm))
    rel_l2 = float(np.linalg.norm(compiled - eager) / eager_norm)
    return cosine, rel_l2
