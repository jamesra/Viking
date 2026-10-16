"""Dense mask-prompt encoding shared by the trainer and the segmentation server.

Training and production must build ``mask_input`` with this one function and these constants, or the mask
network sees a different scale at inference than it was trained on (see docs/mask_refinement_plan.md, section 2).

Human-traced masks are trusted in their interior and only loosely at their edge, so a prompt value is scaled by
the edge confidence ``c(d) = 1 - exp(-d / tau_eff)``, with ``d`` the distance in full-resolution pixels from the
mask's edge and ``tau_eff = min(tau, depth_frac * D)`` capped by the mask's own maximum depth ``D``, so a thin
process is not all edge. Three kinds of region feed the prompt:

* a *seed* (trusted, inside the cell): ``+SEED_LOGIT * c(d)``
* a *mask to correct* (may be wrong): ``+CORRECTION_LOGIT * c(d)``
* other annotations (negative context): ``-SEED_LOGIT * c(d_neighbor)``, each by its own edge distance
* a *circle annotation* (negative context whose radius is unreliable): ``-SEED_LOGIT * w(rho)``, with ``rho`` the
  distance from the circle's center in units of its true radius and ``w`` the cosine taper of
  :class:`CircleAnnotationTrust` (full trust out to ``full_frac``, nothing beyond ``zero_frac``)

Everything else is 0 (unknown). Overlaps are resolved by the *masks*, not by the confidence values: where a
positive region (a seed or a mask to correct) and a negative region cover the same pixel the annotations
contradict each other, so the pixel is 0 (unknown) however deep either is, and nothing is summed. Where only
positive regions overlap the value is the largest of them; where only negatives overlap it is the strongest
negative (``-max``), never a stack, so three neighbors on one pixel read -10, not -30. The full-resolution canvas
is reduced to the 256x256 ``mask_input`` by area averaging, since the values are continuous.

Needs numpy, and scipy for the distance transforms (imported on first use, so the circle encoding runs with numpy
alone and the annotation gallery needs no scipy).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Iterable, Mapping, NamedTuple, Sequence

import numpy as np


def distance_transform_edt(array: np.ndarray) -> np.ndarray:
    """``scipy.ndimage.distance_transform_edt``, imported on first use."""
    from scipy.ndimage import distance_transform_edt as edt

    return edt(array)


LOW_RES = 256
"""Side of SAM2's ``mask_input`` for a 1024 image (4x the 64x64 image embedding)."""

EDGE_TAU_PX = 8.0
"""Default tau of the confidence curve: traces are about 9 px (20 nm) from the membrane on average, and
confidence reaches 95% about 24 px inside the traced line."""

EDGE_TAU_DEPTH_FRAC = 0.25
"""Thin-process cap: ``tau_eff = min(tau, EDGE_TAU_DEPTH_FRAC * D)``, ``D`` the mask's maximum depth. A process
20 px wide has ``D`` about 10 and gets ``tau_eff`` 2.5, so its whole profile is not treated as edge."""

MIN_TAU_PX = 0.5
"""Floor of ``tau_eff``, so a one-pixel-wide mask does not divide by zero."""

SEED_LOGIT = 10.0
"""M_seed. SAM2's ``mask_downscaling`` was trained on low-resolution logits and the server passes such logits
clipped to +/-20, so a trusted pixel is fed as +10."""

CORRECTION_LOGIT = 3.0
"""M_corr. Lower than a seed so the mask network learns that a mask to correct is a suggestion."""

LOGIT_CLIP = 10.0
"""Bound of a prompt value for the default logits. Since edge-confidence-v5 the encoder no longer clips (overlaps
are resolved by the max-or-unknown rule, never summed); the constant stays in the exported metadata."""

PROMPT_ENCODING_VERSION = "edge-confidence-v6"
"""Name of this encoding. Bump it when the way ``mask_input`` is built changes in a way that needs a retrain; an
exported model carries it so a server can refuse a model whose encoding it does not implement. v6 encodes a circle
annotation by its radius-relative trust (:class:`CircleAnnotationTrust`) instead of the polygon edge curve."""

CIRCLE_TRUST_RAMP = "cosine"
"""Shape of the taper between ``full_frac`` and ``zero_frac``; recorded in the metadata."""


@dataclass(frozen=True, slots=True)
class CircleAnnotationTrust:
    """How far a circle annotation is trusted as background, as fractions of its TRUE radius.

    Circle annotations (``source`` "annotated" in ``circle_annotations.json``) have unreliable radii, so they are
    used only as negatives. Inside ``full_frac`` of the true radius a pixel is fed at full strength
    (``-SEED_LOGIT``); the strength tapers with a half cosine to 0 at ``zero_frac``, and the circle claims nothing
    beyond it. Seed circles are a different thing and are not affected.
    """

    full_frac: float = 0.5
    zero_frac: float = 0.95

    def __post_init__(self) -> None:
        full, zero = float(self.full_frac), float(self.zero_frac)
        if not (np.isfinite(full) and np.isfinite(zero) and 0.0 <= full < zero):
            raise ValueError(
                "prompt.circle_annotation_trust must satisfy 0 <= full_frac < zero_frac, "
                f"got full_frac={self.full_frac}, zero_frac={self.zero_frac}"
            )
        object.__setattr__(self, "full_frac", full)
        object.__setattr__(self, "zero_frac", zero)

    def as_dict(self) -> dict[str, float | str]:
        """The form written to the exported metadata."""
        return {"full_frac": self.full_frac, "zero_frac": self.zero_frac, "ramp": CIRCLE_TRUST_RAMP}

    @classmethod
    def from_mapping(cls, values: Mapping[str, Any] | None) -> "CircleAnnotationTrust":
        """From a config or metadata mapping; a missing key keeps its default, None gives the defaults."""
        defaults = cls()
        if not values:
            return defaults
        return cls(
            float(values["full_frac"]) if "full_frac" in values else defaults.full_frac,
            float(values["zero_frac"]) if "zero_frac" in values else defaults.zero_frac,
        )


DEFAULT_CIRCLE_TRUST = CircleAnnotationTrust()
"""The defaults: what a reader without metadata would assume."""


def encoding_metadata(
    circle_diameter_scale: float,
    *,
    edge_tau_px: float = EDGE_TAU_PX,
    edge_tau_depth_frac: float = EDGE_TAU_DEPTH_FRAC,
    seed_logit: float = SEED_LOGIT,
    correction_logit: float = CORRECTION_LOGIT,
    circle_annotation_trust: CircleAnnotationTrust = DEFAULT_CIRCLE_TRUST,
) -> dict[str, Any]:
    """The constants a server must use to build ``mask_input`` for a model trained with this encoding.

    The configurable values default to this module's constants; an exporter passes the ones the run trained
    with. ``LOGIT_CLIP`` and ``LOW_RES`` are not configurable.
    """
    return {
        "prompt_encoding": PROMPT_ENCODING_VERSION,
        "EDGE_TAU_PX": float(edge_tau_px),
        "EDGE_TAU_DEPTH_FRAC": float(edge_tau_depth_frac),
        "SEED_LOGIT": float(seed_logit),
        "CORRECTION_LOGIT": float(correction_logit),
        "LOGIT_CLIP": LOGIT_CLIP,
        "LOW_RES": LOW_RES,
        "circle_diameter_scale": float(circle_diameter_scale),
        "circle_annotation_trust": circle_annotation_trust.as_dict(),
    }


def encoding_differences(
    *,
    edge_tau_px: float,
    edge_tau_depth_frac: float,
    seed_logit: float,
    correction_logit: float,
    circle_diameter_scale: float,
    default_circle_diameter_scale: float,
    circle_annotation_trust: CircleAnnotationTrust = DEFAULT_CIRCLE_TRUST,
) -> list[str]:
    """Messages for each value that differs from this module's constants (the ones a server implements)."""
    pairs = (
        ("prompt.edge_tau_px", edge_tau_px, EDGE_TAU_PX, "EDGE_TAU_PX"),
        ("prompt.edge_tau_depth_frac", edge_tau_depth_frac, EDGE_TAU_DEPTH_FRAC, "EDGE_TAU_DEPTH_FRAC"),
        ("prompt.seed_logit", seed_logit, SEED_LOGIT, "SEED_LOGIT"),
        ("prompt.correction_logit", correction_logit, CORRECTION_LOGIT, "CORRECTION_LOGIT"),
        (
            "prompt.circle_diameter_scale", circle_diameter_scale, default_circle_diameter_scale,
            "DEFAULT_CIRCLE_DIAMETER_SCALE",
        ),
        (
            "prompt.circle_annotation_trust.full_frac", circle_annotation_trust.full_frac,
            DEFAULT_CIRCLE_TRUST.full_frac, "DEFAULT_CIRCLE_TRUST.full_frac",
        ),
        (
            "prompt.circle_annotation_trust.zero_frac", circle_annotation_trust.zero_frac,
            DEFAULT_CIRCLE_TRUST.zero_frac, "DEFAULT_CIRCLE_TRUST.zero_frac",
        ),
    )
    return [
        f"{key} is {value} but annotation_crops {name} is {library}"
        for key, value, library, name in pairs
        if abs(float(value) - float(library)) > 1e-9
    ]


def window(mask: np.ndarray, margin: int) -> tuple[slice, slice] | None:
    """Bounding box of ``mask`` grown by ``margin`` pixels and clipped to the array, or None for an empty mask.

    A distance transform needs only this window: inside it every distance up to ``margin`` is exact, and the
    callers treat anything farther as "far".
    """
    rows = np.flatnonzero(mask.any(axis=1))
    if rows.size == 0:
        return None
    cols = np.flatnonzero(mask.any(axis=0))
    height, width = mask.shape
    return (
        slice(max(int(rows[0]) - margin, 0), min(int(rows[-1]) + 1 + margin, height)),
        slice(max(int(cols[0]) - margin, 0), min(int(cols[-1]) + 1 + margin, width)),
    )


def reach_px(tau: float) -> int:
    """Distance beyond which the confidence and the loss weight are within 5% of 1 (``3 tau`` px, 24 at 8)."""
    return int(np.ceil(3.0 * float(tau)))


def edge_distance(mask: np.ndarray) -> np.ndarray:
    """Distance in pixels from each pixel inside ``mask`` to its edge (0 outside).

    Measured from pixel centers to the boundary between a mask pixel and a background pixel, so a pixel next to
    the background is half a pixel from the edge. The border of the array is *not* an edge: a mask cut by the
    tile edge stays confident there. A mask with no background at all is infinitely far from any edge.

    Only the mask's bounding box plus one background pixel is transformed; that is exact, because the nearest
    background pixel of a mask pixel is never outside it.
    """
    mask = np.asarray(mask, dtype=bool)
    box = window(mask, 1)
    if box is None:
        return np.zeros(mask.shape, dtype=np.float32)
    if mask.all():
        return np.full(mask.shape, np.inf, dtype=np.float32)
    crop = mask[box]
    out = np.zeros(mask.shape, dtype=np.float32)
    out[box] = np.maximum(distance_transform_edt(crop) - 0.5, 0.0) * crop
    return out


def effective_tau(depth: float, tau: float = EDGE_TAU_PX, depth_frac: float = EDGE_TAU_DEPTH_FRAC) -> float:
    """``min(tau, depth_frac * depth)`` for a mask whose maximum edge distance is ``depth`` (floored at MIN_TAU_PX).

    ``tau <= 0`` stays 0 (no edge weighting). ``depth_frac <= 0`` or an infinite ``depth`` disables the cap.
    """
    if tau <= 0.0:
        return 0.0
    if depth_frac <= 0.0 or not np.isfinite(depth):
        return float(tau)
    return float(max(MIN_TAU_PX, min(tau, depth_frac * depth)))


def edge_confidence(
    mask: np.ndarray,
    tau: float = EDGE_TAU_PX,
    depth_frac: float = EDGE_TAU_DEPTH_FRAC,
    distance: np.ndarray | None = None,
) -> np.ndarray:
    """``c(d) = 1 - exp(-d / tau_eff)`` inside ``mask`` (float32), 0 outside; ``tau_eff`` is capped per mask.

    ``tau <= 0`` gives the plain 0/1 mask. ``distance`` is a precomputed :func:`edge_distance` of ``mask``.
    """
    mask = np.asarray(mask, dtype=bool)
    if tau <= 0.0:
        return mask.astype(np.float32)
    if distance is None:
        distance = edge_distance(mask)
    if not mask.any():
        return np.zeros(mask.shape, dtype=np.float32)
    tau_eff = effective_tau(float(distance.max()), tau, depth_frac)
    return (1.0 - np.exp(-distance / tau_eff)).astype(np.float32)


def signed_edge_distance(target: np.ndarray, reach: int | None = None) -> np.ndarray:
    """Distance in pixels from every pixel to the edge of ``target``, on both sides (float32, >= 0).

    This is the ``d`` of the loss weight: pixels inside and outside the traced line both count. An all-True or
    all-False array has no edge, so every distance is infinite. With ``reach`` (see :func:`reach_px`) only the
    bounding box grown by that many pixels is transformed and everything beyond it is infinite, which the weight
    curve maps to 1.
    """
    target = np.asarray(target, dtype=bool)
    if target.all() or not target.any():
        return np.full(target.shape, np.inf, dtype=np.float32)
    box = (slice(None), slice(None)) if reach is None else window(target, int(reach) + 1)
    crop = target[box]
    inside = distance_transform_edt(crop)
    outside = distance_transform_edt(~crop)
    near = np.maximum(np.where(crop, inside, outside) - 0.5, 0.0).astype(np.float32)
    if reach is None:
        return near
    out = np.full(target.shape, np.inf, dtype=np.float32)
    out[box] = near
    return out


def area_weights(source: int, size: int) -> np.ndarray:
    """``[size, source]`` matrix that averages (or, when upsampling, replicates) a length-``source`` axis to ``size``.

    Row ``i`` holds the fractions of each source cell that fall in output cell ``i``, normalized to sum to 1.
    """
    edges_out = np.linspace(0.0, float(source), size + 1)
    edges_in = np.arange(source + 1, dtype=np.float64)
    low = np.maximum(edges_out[:-1, None], edges_in[None, :-1])
    high = np.minimum(edges_out[1:, None], edges_in[None, 1:])
    overlap = np.clip(high - low, 0.0, None)
    return (overlap / overlap.sum(axis=1, keepdims=True)).astype(np.float32)


def area_resize(array: np.ndarray, size: int = LOW_RES) -> np.ndarray:
    """Area-average a 2-D float array to ``size`` x ``size`` (float32)."""
    height, width = array.shape
    rows = area_weights(height, size)
    cols = area_weights(width, size)
    return (rows @ array.astype(np.float32, copy=False) @ cols.T).astype(np.float32)


class NegativeCanvas(NamedTuple):
    """One negative region ready to encode: its boolean ``mask`` and its ``canvas`` of ``-seed_logit * c(d)``.

    The mask rides along because the confidence is 0 on the region's outermost pixels, so ``canvas != 0`` would
    miss them, and overlaps are decided by the mask.
    """

    mask: np.ndarray
    canvas: np.ndarray


def negative_canvas(
    mask: np.ndarray,
    tau: float = EDGE_TAU_PX,
    seed_logit: float = SEED_LOGIT,
    depth_frac: float = EDGE_TAU_DEPTH_FRAC,
) -> NegativeCanvas:
    """One negative region as its mask and ``-seed_logit * c(d)`` canvas (float32, <= 0), with its own ``tau_eff``.

    A sample with several prompt variants shares its negatives between them, so it computes these once and
    passes them to :func:`encode_dense_canvas` as ``negative_canvases``.
    """
    return NegativeCanvas(
        np.asarray(mask, dtype=bool), -np.float32(seed_logit) * edge_confidence(mask, tau, depth_frac)
    )


def circle_trust(
    shape: tuple[int, int], center_x: float, center_y: float, radius: float, trust: CircleAnnotationTrust
) -> tuple[np.ndarray, np.ndarray]:
    """A circle annotation's claimed region and trust map ``w`` (bool, float32 in [0, 1]), computed analytically.

    ``rho = |p - c| / radius`` for a pixel center ``p`` (pixel (x, y) is at (x + 0.5, y + 0.5)), ``radius`` the TRUE
    radius. The mask is ``rho <= zero_frac``; ``w`` is 1 for ``rho <= full_frac``, 0 from ``zero_frac`` out and
    ``0.5 * (1 + cos(pi * (rho - full_frac) / (zero_frac - full_frac)))`` between. Only the circle's bounding box is
    evaluated, so the cost follows the circle, not the tile. A circle that misses the tile gives an empty region.
    """
    height, width = shape
    mask = np.zeros((height, width), dtype=bool)
    weight = np.zeros((height, width), dtype=np.float32)
    if radius <= 0.0:
        return mask, weight
    reach = trust.zero_frac * radius
    x0, x1 = max(int(np.floor(center_x - reach)) - 1, 0), min(int(np.ceil(center_x + reach)) + 1, width)
    y0, y1 = max(int(np.floor(center_y - reach)) - 1, 0), min(int(np.ceil(center_y + reach)) + 1, height)
    if x0 >= x1 or y0 >= y1:
        return mask, weight
    dx2 = (np.arange(x0, x1, dtype=np.float32) + np.float32(0.5) - np.float32(center_x)) ** 2
    dy2 = (np.arange(y0, y1, dtype=np.float32) + np.float32(0.5) - np.float32(center_y)) ** 2
    d2 = dy2[:, None] + dx2[None, :]
    claimed = d2 <= np.float32(reach) ** 2
    full = d2 <= np.float32(trust.full_frac * radius) ** 2
    rho = np.sqrt(d2) / np.float32(radius)
    phase = (rho - np.float32(trust.full_frac)) * np.float32(np.pi / (trust.zero_frac - trust.full_frac))
    taper = np.float32(0.5) * (np.float32(1.0) + np.cos(np.clip(phase, 0.0, np.pi)))
    mask[y0:y1, x0:x1] = claimed
    weight[y0:y1, x0:x1] = np.where(full, np.float32(1.0), np.where(claimed, taper, np.float32(0.0)))
    return mask, weight


def circle_negative_canvas(
    shape: tuple[int, int],
    center_x: float,
    center_y: float,
    radius: float,
    trust: CircleAnnotationTrust = DEFAULT_CIRCLE_TRUST,
    seed_logit: float = SEED_LOGIT,
) -> NegativeCanvas:
    """A circle annotation as a negative region: its claimed mask and ``-seed_logit * w(rho)`` canvas.

    ``radius`` is the true radius; see :func:`circle_trust`.
    """
    claimed, weight = circle_trust(shape, center_x, center_y, radius, trust)
    return NegativeCanvas(claimed, -np.float32(seed_logit) * weight)


def encode_dense_canvas(
    shape: tuple[int, int],
    *,
    seed: np.ndarray | None = None,
    correction: np.ndarray | None = None,
    negatives: Iterable[np.ndarray] = (),
    negative_canvases: Iterable[NegativeCanvas | np.ndarray] = (),
    tau: float = EDGE_TAU_PX,
    seed_logit: float = SEED_LOGIT,
    correction_logit: float = CORRECTION_LOGIT,
    depth_frac: float = EDGE_TAU_DEPTH_FRAC,
) -> np.ndarray:
    """Full-resolution float32 logit canvas of ``shape`` (see the module docstring for the values).

    ``negative_canvases`` are :func:`negative_canvas` results. A bare canvas array is accepted too, with its
    region taken as ``canvas != 0``, which misses the region's outermost pixels; pass a :class:`NegativeCanvas`.
    """
    positive = np.zeros(shape, dtype=np.float32)
    positive_mask = np.zeros(shape, dtype=bool)
    for layer, logit in ((seed, seed_logit), (correction, correction_logit)):
        if layer is not None and np.any(layer):
            positive_mask |= np.asarray(layer, dtype=bool)
            np.maximum(positive, np.float32(logit) * edge_confidence(layer, tau, depth_frac), out=positive)

    strength = np.zeros(shape, dtype=np.float32)  # the strongest negative at each pixel, as a magnitude
    negative_mask = np.zeros(shape, dtype=bool)

    def add_negative(region: NegativeCanvas) -> None:
        np.logical_or(negative_mask, region.mask, out=negative_mask)
        np.maximum(strength, -region.canvas, out=strength)

    for region in negatives:
        if np.any(region):
            add_negative(negative_canvas(region, tau, seed_logit, depth_frac))
    for ready in negative_canvases:
        if not isinstance(ready, NegativeCanvas):
            canvas = np.asarray(ready, dtype=np.float32)
            ready = NegativeCanvas(canvas != 0, canvas)
        add_negative(ready)

    # A pixel claimed by both a positive and a negative region is unknown, however deep either is.
    canvas = np.where(positive_mask & ~negative_mask, positive, np.float32(0.0)).astype(np.float32, copy=False)
    only_negative = negative_mask & ~positive_mask
    canvas[only_negative] = -strength[only_negative]
    return canvas


def encode_mask_input(
    shape: tuple[int, int],
    *,
    seed: np.ndarray | None = None,
    correction: np.ndarray | None = None,
    negatives: Sequence[np.ndarray] = (),
    negative_canvases: Sequence[NegativeCanvas | np.ndarray] = (),
    tau: float = EDGE_TAU_PX,
    seed_logit: float = SEED_LOGIT,
    correction_logit: float = CORRECTION_LOGIT,
    size: int = LOW_RES,
    depth_frac: float = EDGE_TAU_DEPTH_FRAC,
) -> np.ndarray | None:
    """The ``[size, size]`` float32 ``mask_input`` for a prompt, or None when every value is 0.

    None means "use the prompt encoder's learned no-mask embedding", which an all-zero tensor would not.
    """
    canvas = encode_dense_canvas(
        shape,
        seed=seed,
        correction=correction,
        negatives=negatives,
        negative_canvases=negative_canvases,
        tau=tau,
        seed_logit=seed_logit,
        correction_logit=correction_logit,
        depth_frac=depth_frac,
    )
    if not canvas.any():
        return None
    return area_resize(canvas, size)
