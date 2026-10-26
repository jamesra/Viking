"""Mask-prompt encodings a served model can be trained with, chosen by its manifest.

SAM2 takes the mask prompt as a 256x256 ``mask_input`` of logits. What the values mean is a training choice, and a model
only works well with the encoding it was trained on, so the server, not the client, turns the prompt's shapes and
bitmaps into those values:

* ``legacy-flat-v1`` is the original scheme (``tile_growth.mask_prompt_logits`` and ``_with_background``: a flat +8
  inside the object's mask, -8 inside a background region). It has no encoder object here: a ``None`` encoder keeps
  that code path exactly as it was.
* ``edge-confidence-v6`` (``trainer_prompt_encoding.PROMPT_ENCODING_VERSION``, :class:`EdgeConfidenceEncoder`): each
  part of the object is a seed, ``+SEED_LOGIT * c(d)`` with ``c`` the trainer's edge confidence of its own pixels
  (a softened circle is its disc at ``radius * circle_diameter_scale``); several parts of one island are combined by
  the largest value. Background shapes are negatives each by its own geometry: a circle by its radius trust
  (``circle_negative_canvas``), any other shape and the background bitmap by the edge curve (``negative_canvas``).
  A pixel claimed by both the object and the background is 0 (unknown), the trainer's overlap rule.
* ``edge-confidence-v7`` (:class:`DepthTrustEncoder`, not served yet): softened shapes are trusted relative to their
  own depth (``depth_trust``), islands are the trusted cores, and everything else is as in v6.

The start window of a walk (the one the island lands in) is built wholly by the encoder. Windows the walk grows into
are prompted with the fused logits of their neighbours, and the background is painted into them with the encoder's
negatives (:meth:`background_logits`) rather than the legacy flat value.
"""

from __future__ import annotations

from typing import Any, List, Mapping, Optional, Protocol, Sequence, Tuple

import numpy as np
from numpy.typing import NDArray

from segmentation_server import trainer_prompt_encoding as enc
from segmentation_server.cell_grid import CELL_SIZE, Cell, MaskShape, window_origin
from segmentation_server.depth_trust import DEFAULT_MIN_CORE_PX, build_trust_islands
from segmentation_server.model_manifest import LEGACY_ENCODING, ManifestError, ModelManifest
from segmentation_server.prompt import BackgroundShape, ForegroundPart, Island, build_islands

EDGE_CONFIDENCE_V6 = enc.PROMPT_ENCODING_VERSION
EDGE_CONFIDENCE_V7 = "edge-confidence-v7"


class PromptEncoder(Protocol):
    """Builds ``mask_input`` for the served model from the object's islands and the background."""

    name: str
    circle_scale: float

    def islands(self, parts: Sequence[ForegroundPart], max_tiles: int, max_islands: int) -> List[Island]:
        """The object prompt split into islands, numbered by decreasing trusted area."""
        ...

    def start_window(
        self, cell: Cell, shape: MaskShape, background_masks: Sequence[MaskShape]
    ) -> Optional[NDArray[np.float32]]:
        """The 256x256 ``mask_input`` for ``cell``'s window, or None for "no mask prompt".

        ``shape`` is the island (an :class:`Island`, or a plain mask read as one seed) and ``background_masks`` the
        regions it is not (background shapes and bitmap).
        """
        ...

    def background_logits(
        self, cell: Cell, background_masks: Sequence[MaskShape]
    ) -> Optional[NDArray[np.float32]]:
        """The 256x256 negatives (values <= 0) of the background in ``cell``'s window, or None when none reach it."""
        ...


def compose(
    shape: Tuple[int, int],
    positive: Optional[NDArray[np.float32]],
    positive_mask: Optional[NDArray[np.bool_]],
    negatives: Sequence[enc.NegativeCanvas],
) -> NDArray[np.float32]:
    """The full-resolution canvas from a positive layer and negative regions, with the trainer's overlap rule.

    The same rule as ``trainer_prompt_encoding.encode_dense_canvas``: a pixel claimed by both sides is 0 (unknown),
    positives keep their value, and where only negatives overlap the strongest one wins. With one seed it gives exactly
    what ``encode_dense_canvas(seed=...)`` gives.
    """
    strength = np.zeros(shape, dtype=np.float32)
    negative_mask = np.zeros(shape, dtype=bool)
    for region in negatives:
        np.logical_or(negative_mask, region.mask, out=negative_mask)
        np.maximum(strength, -region.canvas, out=strength)
    if positive is None or positive_mask is None:
        positive = np.zeros(shape, dtype=np.float32)
        positive_mask = np.zeros(shape, dtype=bool)
    canvas = np.where(positive_mask & ~negative_mask, positive, np.float32(0.0)).astype(np.float32, copy=False)
    only_negative = negative_mask & ~positive_mask
    canvas[only_negative] = -strength[only_negative]
    return canvas


class EdgeConfidenceEncoder:
    """The trainer's edge-confidence encoding (v6), with the constants the model was trained with."""

    name = EDGE_CONFIDENCE_V6

    def __init__(self, record: Mapping[str, Any], circle_scale: float = 1.0) -> None:
        self.tau = float(record.get("EDGE_TAU_PX", enc.EDGE_TAU_PX))
        self.depth_frac = float(record.get("EDGE_TAU_DEPTH_FRAC", enc.EDGE_TAU_DEPTH_FRAC))
        self.seed_logit = float(record.get("SEED_LOGIT", enc.SEED_LOGIT))
        self.correction_logit = float(record.get("CORRECTION_LOGIT", enc.CORRECTION_LOGIT))
        self.circle_trust = enc.CircleAnnotationTrust.from_mapping(record.get("circle_annotation_trust"))
        self.circle_scale = float(circle_scale)

    def islands(self, parts: Sequence[ForegroundPart], max_tiles: int, max_islands: int) -> List[Island]:
        return build_islands(parts, max_tiles, max_islands)

    def _seed(self, mask: NDArray[np.bool_], logit: Optional[float] = None) -> NDArray[np.float32]:
        logit = self.seed_logit if logit is None else logit
        return np.float32(logit) * enc.edge_confidence(mask, self.tau, self.depth_frac)

    def _positive(self, cell: Cell, shape: MaskShape) -> Optional[Tuple[NDArray[np.float32], NDArray[np.bool_]]]:
        """The object's values over ``cell``'s window and the pixels it claims, or None when it misses the window."""
        claimed = shape.window_full(cell)
        if claimed is None:
            return None
        parts = getattr(shape, "parts", None)
        if parts is None:
            # A plain mask: one seed as drawn, or a mask to correct.
            logit = self.correction_logit if shape.role == "correction" else self.seed_logit
            return self._seed(claimed, logit), claimed
        positive = np.zeros((CELL_SIZE, CELL_SIZE), dtype=np.float32)
        for part in parts:
            self._add_part(positive, cell, shape, part, claimed)
        return positive, claimed

    def _add_part(
        self, positive: NDArray[np.float32], cell: Cell, island: MaskShape, part: ForegroundPart,
        claimed: NDArray[np.bool_],
    ) -> None:
        """Raise ``positive`` to one part's seed values, each part with its own edge distance and depth cap."""
        pixels = part.region.window_full(cell)
        if pixels is None:
            return
        pixels &= claimed
        if pixels.any():
            np.maximum(positive, self._seed(pixels), out=positive)

    def _negatives(self, cell: Cell, background_masks: Sequence[MaskShape]) -> List[enc.NegativeCanvas]:
        """Each background region that reaches the window as a negative, each by its own geometry."""
        negatives: List[enc.NegativeCanvas] = []
        wx0, wy0 = window_origin(cell)
        for region in background_masks:
            circle = getattr(region, "circle", None) if isinstance(region, BackgroundShape) else None
            if circle is not None:
                # The window is Y-down: mosaic y maps to row coordinate wy0 + CELL_SIZE - y, pixel centres at row + 0.5.
                ready = enc.circle_negative_canvas(
                    (CELL_SIZE, CELL_SIZE), circle.cx - wx0, wy0 + CELL_SIZE - circle.cy, circle.radius,
                    self.circle_trust, self.seed_logit,
                )
                if ready.mask.any():
                    negatives.append(ready)
                continue
            pixels = region.window_full(cell)
            if pixels is not None:
                negatives.append(enc.negative_canvas(pixels, self.tau, self.seed_logit, self.depth_frac))
        return negatives

    def start_window(
        self, cell: Cell, shape: MaskShape, background_masks: Sequence[MaskShape]
    ) -> Optional[NDArray[np.float32]]:
        found = self._positive(cell, shape)
        if found is None:
            return None
        canvas = compose((CELL_SIZE, CELL_SIZE), found[0], found[1], self._negatives(cell, background_masks))
        if not canvas.any():
            return None
        return enc.area_resize(canvas, enc.LOW_RES)

    def background_logits(
        self, cell: Cell, background_masks: Sequence[MaskShape]
    ) -> Optional[NDArray[np.float32]]:
        negatives = self._negatives(cell, background_masks)
        if not negatives:
            return None
        canvas = compose((CELL_SIZE, CELL_SIZE), None, None, negatives)
        if not canvas.any():
            return None
        return enc.area_resize(canvas, enc.LOW_RES)


class DepthTrustEncoder(EdgeConfidenceEncoder):
    """The v7 encoding: softened shapes by depth-relative trust (``depth_trust``), islands from the trusted cores.

    A softened shape's value is ``SEED_LOGIT * w(rho)``; the painted bitmap keeps the v6 seed. A circle's region is its
    support, the disc at ``zero_frac`` of its true radius.
    """

    name = EDGE_CONFIDENCE_V7

    def __init__(self, record: Mapping[str, Any], circle_scale: float = 1.0) -> None:
        super().__init__(record, circle_scale)
        # w is 0 from zero_frac of the radius out, so that disc is everything a circle claims.
        self.circle_scale = self.circle_trust.zero_frac
        self.min_core_px = int(record.get("island_min_core_px", DEFAULT_MIN_CORE_PX))

    def islands(self, parts: Sequence[ForegroundPart], max_tiles: int, max_islands: int) -> List[Island]:
        return build_trust_islands(parts, self.circle_trust, max_tiles, max_islands, self.min_core_px)

    def _add_part(
        self, positive: NDArray[np.float32], cell: Cell, island: MaskShape, part: ForegroundPart,
        claimed: NDArray[np.bool_],
    ) -> None:
        if not part.softened:
            super()._add_part(positive, cell, island, part, claimed)
            return
        trust = island.trust_window(cell) if isinstance(island, Island) else None
        if trust is not None:
            np.maximum(positive, np.float32(self.seed_logit) * trust * claimed, out=positive)


def supported_encodings() -> tuple[str, ...]:
    return (LEGACY_ENCODING, EDGE_CONFIDENCE_V6, EDGE_CONFIDENCE_V7)


def circle_diameter_scale(manifest: ModelManifest) -> float:
    """The factor a softened circle's radius is scaled by: the manifest's ``client_contract.circle_diameter_scale``,
    else the encoding record's, else 1 (a model that does not say was trained on circles as drawn)."""
    for source in (manifest.client_contract, manifest.encoding_record):
        value = source.get("circle_diameter_scale") if isinstance(source, Mapping) else None
        if isinstance(value, (int, float)) and 0 < float(value) <= 4:
            return float(value)
    return 1.0


def encoder_for(manifest: ModelManifest) -> Optional[PromptEncoder]:
    """The encoder ``manifest`` names; None for the legacy encoding.

    Raises:
        ManifestError: The manifest names an encoding this server does not implement.
    """
    name = manifest.prompt_encoding
    if name == LEGACY_ENCODING:
        return None
    if name == EDGE_CONFIDENCE_V6:
        return EdgeConfidenceEncoder(manifest.encoding_record, circle_diameter_scale(manifest))
    if name == EDGE_CONFIDENCE_V7:
        return DepthTrustEncoder(manifest.encoding_record, circle_diameter_scale(manifest))
    raise ManifestError(
        f"the model's mask-prompt encoding {name!r} is not implemented by this server "
        f"(it implements {', '.join(supported_encodings())}); refusing to serve it with the wrong prompt"
    )
