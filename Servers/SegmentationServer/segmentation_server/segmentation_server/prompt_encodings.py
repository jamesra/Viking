"""Mask-prompt encodings a served model can be trained with, chosen by its manifest.

SAM2 takes the mask prompt as a 256x256 ``mask_input`` of logits. What the values mean is a training choice, and a model
only works well with the encoding it was trained on:

* ``legacy-flat-v1`` is the original scheme (``tile_growth.mask_prompt_logits`` and ``_with_background``: a flat +8
  inside a binary mask, -8 for a background mask, -2 for a background rectangle). It has no encoder object here: a
  ``None`` encoder keeps that code path exactly as it was.
* The trainer's edge-confidence encoding (``trainer_prompt_encoding.PROMPT_ENCODING_VERSION``) scales each value by the
  distance from the mask's edge, with a different strength for a seed and for a mask to correct, and negatives drawn
  from the neighbouring annotations. :class:`EdgeConfidenceEncoder` builds it with the trainer's own function.

The start window of a walk (the one the client's mask lands in) is built wholly by the encoder. Windows the walk
grows into are prompted with the fused logits of their neighbours, and the client's background masks are painted into
them with the encoder's negatives (:meth:`PromptEncoder.background_logits`) rather than the legacy flat value.
"""

from __future__ import annotations

from typing import Any, Mapping, Optional, Protocol, Sequence

import numpy as np
from numpy.typing import NDArray

from segmentation_server import trainer_prompt_encoding as enc
from segmentation_server.cell_grid import CELL_SIZE, Cell, MaskShape
from segmentation_server.model_manifest import LEGACY_ENCODING, ManifestError, ModelManifest


class PromptEncoder(Protocol):
    """Builds the start window's ``mask_input`` from the client's masks."""

    name: str

    def start_window(
        self, cell: Cell, shape: MaskShape, background_masks: Sequence[MaskShape]
    ) -> Optional[NDArray[np.float32]]:
        """The 256x256 ``mask_input`` for ``cell``'s window, or None for "no mask prompt".

        ``shape`` is the client's foreground mask (its ``role`` says seed or correction) and ``background_masks``
        are the masks it said are not the object. Background rectangles are not passed: this encoding has no weak
        negative.
        """
        ...

    def background_logits(
        self, cell: Cell, background_masks: Sequence[MaskShape]
    ) -> Optional[NDArray[np.float32]]:
        """The 256x256 negatives (values <= 0) of ``background_masks`` in ``cell``'s window, or None when none reach it.

        Painted over a continuation window's fused logits, so a window the walk grows into is told about the
        client's background masks in the encoding the model was trained on.
        """
        ...


class EdgeConfidenceEncoder:
    """The trainer's edge-confidence encoding, with the constants the model was trained with."""

    def __init__(self, record: Mapping[str, Any]) -> None:
        self.name = enc.PROMPT_ENCODING_VERSION
        self.tau = float(record.get("EDGE_TAU_PX", enc.EDGE_TAU_PX))
        self.depth_frac = float(record.get("EDGE_TAU_DEPTH_FRAC", enc.EDGE_TAU_DEPTH_FRAC))
        self.seed_logit = float(record.get("SEED_LOGIT", enc.SEED_LOGIT))
        self.correction_logit = float(record.get("CORRECTION_LOGIT", enc.CORRECTION_LOGIT))

    def start_window(
        self, cell: Cell, shape: MaskShape, background_masks: Sequence[MaskShape]
    ) -> Optional[NDArray[np.float32]]:
        mask = shape.window_full(cell)
        if mask is None:
            return None
        negatives = [
            pixels for pixels in (other.window_full(cell) for other in background_masks) if pixels is not None
        ]
        correcting = shape.role == "correction"
        return enc.encode_mask_input(
            (CELL_SIZE, CELL_SIZE),
            seed=None if correcting else mask,
            correction=mask if correcting else None,
            negatives=negatives,
            tau=self.tau,
            seed_logit=self.seed_logit,
            correction_logit=self.correction_logit,
            depth_frac=self.depth_frac,
        )


    def background_logits(
        self, cell: Cell, background_masks: Sequence[MaskShape]
    ) -> Optional[NDArray[np.float32]]:
        negatives = [
            pixels for pixels in (other.window_full(cell) for other in background_masks) if pixels is not None
        ]
        if not negatives:
            return None
        canvas = enc.encode_dense_canvas(
            (CELL_SIZE, CELL_SIZE),
            negatives=negatives,
            tau=self.tau,
            seed_logit=self.seed_logit,
            correction_logit=self.correction_logit,
            depth_frac=self.depth_frac,
        )
        if not canvas.any():
            return None
        return enc.area_resize(canvas, enc.LOW_RES)


def supported_encodings() -> tuple[str, ...]:
    return (LEGACY_ENCODING, enc.PROMPT_ENCODING_VERSION)


def encoder_for(manifest: ModelManifest) -> Optional[PromptEncoder]:
    """The encoder ``manifest`` names; None for the legacy encoding.

    Raises:
        ManifestError: The manifest names an encoding this server does not implement.
    """
    name = manifest.prompt_encoding
    if name == LEGACY_ENCODING:
        return None
    if name == enc.PROMPT_ENCODING_VERSION:
        return EdgeConfidenceEncoder(manifest.encoding_record)
    raise ManifestError(
        f"the model's mask-prompt encoding {name!r} is not implemented by this server "
        f"(it implements {', '.join(supported_encodings())}); refusing to serve it with the wrong prompt"
    )
