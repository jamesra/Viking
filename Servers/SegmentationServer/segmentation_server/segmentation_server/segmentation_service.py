"""SAM2 wrapper used by the segmentation gRPC service."""

from __future__ import annotations

import logging
import os
import threading
from contextlib import nullcontext
from pathlib import Path
from typing import Any, List, Sequence, Tuple

import numpy as np
import torch
from numpy.typing import NDArray

import sam2
from sam2.build_sam import build_sam2
from sam2.sam2_image_predictor import SAM2ImagePredictor

from segmentation_server.mask_utils import (
    LabeledImage,
    Point,
    SegmentInfo,
    cleanup_mask,
    get_mask_bounds,
    mask_to_polygons,
    prepare_image_for_sam2,
    process_masks,
)

logger = logging.getLogger(__name__)

DEFAULT_MODEL_CFG = "configs/sam2.1/sam2.1_hiera_l.yaml"
DEFAULT_CHECKPOINT_NAME = "sam2.1_hiera_large.pt"


class SegmentationModel:
    """Shared SAM2 weights plus per-image predictors.

    One model instance is created at process start. Each cached image gets its own
    SAM2ImagePredictor wrapping these weights so set_image() state is not shared.
    Predictors are not thread-safe; callers must serialize access with a lock.
    """

    cleanup_mask = staticmethod(cleanup_mask)
    get_mask_bounds = staticmethod(get_mask_bounds)
    mask_to_polygons = staticmethod(mask_to_polygons)
    prepare_image_for_sam2 = staticmethod(prepare_image_for_sam2)

    def __init__(self) -> None:
        if torch.cuda.is_available():
            self.device: torch.device = torch.device("cuda")
            if torch.cuda.get_device_properties(0).major >= 8:
                torch.backends.cuda.matmul.allow_tf32 = True
                torch.backends.cudnn.allow_tf32 = True
        elif torch.backends.mps.is_available():
            self.device = torch.device("mps")
            logger.warning(
                "Support for MPS devices is preliminary. SAM2 is trained with CUDA and might "
                "give numerically different outputs and sometimes degraded performance on MPS."
            )
        else:
            self.device = torch.device("cpu")

        logger.info("Using device: %s", self.device)

        model_cfg, sam2_checkpoint = _resolve_sam2_paths()
        self.sam2_model: Any = build_sam2(model_cfg, sam2_checkpoint, device=self.device)
        self.predictor: SAM2ImagePredictor = SAM2ImagePredictor(self.sam2_model)
        self._shared_predictor_lock = threading.Lock()

    def _autocast(self):
        """Enable CUDA autocast only on CUDA; CPU/MPS leave dtypes unchanged."""
        if self.device.type != "cuda":
            return nullcontext()
        major = torch.cuda.get_device_properties(0).major
        dtype = torch.bfloat16 if major >= 8 else torch.float16
        return torch.autocast("cuda", dtype=dtype)

    def create_predictor(self) -> SAM2ImagePredictor:
        """Return a new predictor wrapping the shared weights; call set_image() before predict()."""
        return SAM2ImagePredictor(self.sam2_model)

    def create_initialized_predictor(self, image_data: bytes) -> SAM2ImagePredictor:
        """Create a predictor and run set_image() so later predict() calls skip embedding."""
        predictor = self.create_predictor()
        image_np = prepare_image_for_sam2(image_data)
        with torch.inference_mode(), self._autocast():
            predictor.set_image(image_np)
        return predictor

    def segment_image_with_predictor(
        self,
        predictor: SAM2ImagePredictor,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool = True,
        empty_shape: Tuple[int, int] = (0, 0),
    ) -> Tuple[LabeledImage, List[SegmentInfo]]:
        """Run predict() on a predictor that already has set_image() applied."""
        point_coords: NDArray[np.int_] = np.array(coordinates)
        point_labels: NDArray[np.int_] = np.array(labels)

        with torch.inference_mode(), self._autocast():
            masks, scores, _logits = predictor.predict(
                point_coords=point_coords,
                point_labels=point_labels,
                multimask_output=multimask_output,
            )

        sorted_ind = np.argsort(scores)[::-1]
        masks = masks[sorted_ind].astype(np.bool_)
        scores = scores[sorted_ind]
        return process_masks(masks, scores, empty_shape=empty_shape)

    def segment_image(
        self,
        image_data: bytes,
        width: int,
        height: int,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool = True,
    ) -> Tuple[LabeledImage, List[SegmentInfo]]:
        """Inline-image path: set_image() + predict() on the shared predictor.

        Concurrent callers are serialized on `_shared_predictor_lock` because the
        shared predictor's embedding state is not thread-safe.
        """
        image_np = prepare_image_for_sam2(image_data)
        point_coords: NDArray[np.int_] = np.array(coordinates)
        point_labels: NDArray[np.int_] = np.array(labels)

        with self._shared_predictor_lock:
            with torch.inference_mode(), self._autocast():
                self.predictor.set_image(image_np)
                masks, scores, _logits = self.predictor.predict(
                    point_coords=point_coords,
                    point_labels=point_labels,
                    multimask_output=multimask_output,
                )

        sorted_ind = np.argsort(scores)[::-1]
        masks = masks[sorted_ind].astype(np.bool_)
        scores = scores[sorted_ind]
        return process_masks(masks, scores, empty_shape=(height, width))


def _resolve_sam2_paths() -> Tuple[str, str]:
    """Hydra config name plus checkpoint filesystem path.

    Override with SAM2_MODEL_CFG and SAM2_CHECKPOINT when the default layout
    (package parent / checkpoints / sam2.1_hiera_large.pt) does not apply.
    """
    model_cfg = os.environ.get("SAM2_MODEL_CFG", DEFAULT_MODEL_CFG)
    checkpoint = os.environ.get("SAM2_CHECKPOINT")
    if checkpoint:
        return model_cfg, checkpoint

    sam2_root = Path(sam2.__file__).resolve().parent.parent
    checkpoint_path = sam2_root / "checkpoints" / DEFAULT_CHECKPOINT_NAME
    return model_cfg, str(checkpoint_path)
