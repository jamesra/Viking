"""SAM2 wrapper used by the segmentation gRPC service."""

from __future__ import annotations

import hashlib
import io
import logging
import os
import threading
import time
from contextlib import nullcontext
from pathlib import Path
from typing import Any, Callable, List, Optional, Sequence, Tuple

import numpy as np
import torch
from numpy.typing import NDArray

import sam2
from sam2.build_sam import build_sam2
from sam2.sam2_image_predictor import SAM2ImagePredictor

from segmentation_server.compile_config import (
    SAM2_IMAGE_SIZE,
    hydra_overrides_for_image_encoder,
)
from segmentation_server.embedding_store import EmbeddingStore, open_embedding_store
from segmentation_server.cuda_errors import raise_if_cuda_lost
from segmentation_server.model_capabilities import SAM2_CAPABILITIES
from segmentation_server.mask_utils import (
    LabeledImage,
    Point,
    SegmentInfo,
    UnionMaskStats,
    cleanup_mask,
    combined_mask_to_segments,
    fill_small_holes,
    get_mask_bounds,
    mask_to_polygons,
    prepare_image_for_sam2,
    process_masks,
    union_masks_covering_positives,
)

logger = logging.getLogger(__name__)

DEFAULT_MODEL_CFG = "configs/sam2.1/sam2.1_hiera_l.yaml"
DEFAULT_CHECKPOINT_NAME = "sam2.1_hiera_large.pt"

COMPILE_OFF = "off"
COMPILE_WARMING = "warming"
COMPILE_READY = "ready"
COMPILE_FAILED = "failed"


class SegmentationModel:
    """Shared SAM2 weights plus per-image predictors.

    One model instance is created at process start. Each cached image gets its own
    SAM2ImagePredictor wrapping these weights so set_image() state is not shared.
    Predictors are not thread-safe; callers must serialize access with a lock.
    """

    capabilities = SAM2_CAPABILITIES

    cleanup_mask = staticmethod(cleanup_mask)
    fill_small_holes = staticmethod(fill_small_holes)
    get_mask_bounds = staticmethod(get_mask_bounds)
    mask_to_polygons = staticmethod(mask_to_polygons)
    prepare_image_for_sam2 = staticmethod(prepare_image_for_sam2)

    def __init__(
        self,
        compile_image_encoder: bool = True,
        on_compiled_ready: Optional[Callable[[], None]] = None,
    ) -> None:
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

        self._swap_lock = threading.Lock()
        self._callback_lock = threading.Lock()
        self._on_compiled_ready = on_compiled_ready
        self._ready_notified = False
        self._shared_predictor_lock = threading.Lock()
        self.compile_status = COMPILE_OFF
        self._compile_thread: Optional[threading.Thread] = None

        # Always load eager first so gRPC can serve while compile/warmup runs.
        model_cfg, sam2_checkpoint = _resolve_sam2_paths()
        self._model_cfg = model_cfg
        self._sam2_checkpoint = sam2_checkpoint
        logger.info("Loading SAM2 checkpoint %s (config %s)", sam2_checkpoint, model_cfg)
        self.sam2_model: Any = build_sam2(
            model_cfg,
            sam2_checkpoint,
            device=self.device,
            hydra_overrides_extra=[],
        )
        self.predictor: SAM2ImagePredictor = SAM2ImagePredictor(self.sam2_model)
        self._embedding_mode = "eager"
        self._embedding_store: Optional[EmbeddingStore] = open_embedding_store(
            sam2_checkpoint,
            model_cfg,
        )

        if hydra_overrides_for_image_encoder(compile_image_encoder, self.device.type):
            self.compile_status = COMPILE_WARMING
            logger.info("Serving eager image encoder while torch.compile runs in the background")
            self._compile_thread = threading.Thread(
                target=self._compile_in_background,
                name="sam2-compile",
                daemon=True,
            )
            self._compile_thread.start()

    def set_on_compiled_ready(self, callback: Optional[Callable[[], None]]) -> None:
        """Register a callback invoked after the compiled model is swapped in.

        If compile already finished, the callback runs on this thread.
        """
        invoke_now = False
        with self._callback_lock:
            self._on_compiled_ready = callback
            invoke_now = (
                callback is not None
                and self.compile_status == COMPILE_READY
                and not self._ready_notified
            )
            if invoke_now:
                self._ready_notified = True
        if invoke_now:
            self._invoke_compiled_ready()

    def _invoke_compiled_ready(self) -> None:
        with self._callback_lock:
            callback = self._on_compiled_ready
        if callback is None:
            return
        try:
            callback()
        except Exception:
            logger.exception("on_compiled_ready callback failed")

    def _compile_in_background(self) -> None:
        """Build a compiled encoder, warm it, then swap it in. Eager stays up on failure."""
        try:
            compiled = build_sam2(
                self._model_cfg,
                self._sam2_checkpoint,
                device=self.device,
                hydra_overrides_extra=hydra_overrides_for_image_encoder(True, "cuda"),
            )
            self._warmup_compiled_encoder(compiled)
            compiled_predictor = SAM2ImagePredictor(compiled)
            with self._shared_predictor_lock:
                with self._swap_lock:
                    self.sam2_model = compiled
                    self.predictor = compiled_predictor
                    # Compiled numerics are a different cache generation than eager set_image().
                    self._embedding_mode = "compiled"
            with self._callback_lock:
                self.compile_status = COMPILE_READY
                callback = self._on_compiled_ready
                if callback is not None:
                    self._ready_notified = True
            logger.info("Switched to compiled image encoder")
            if callback is not None:
                try:
                    callback()
                except Exception:
                    logger.exception("on_compiled_ready callback failed")
        except Exception:
            logger.exception("Background image-encoder compile failed; staying on eager")
            self.compile_status = COMPILE_FAILED

    def _warmup_compiled_encoder(self, model: Any) -> None:
        """Pay Inductor/Triton autotune on *model* before it replaces the eager encoder."""
        logger.info(
            "Warming compiled image encoder at %sx%s (first pass can take minutes)",
            SAM2_IMAGE_SIZE,
            SAM2_IMAGE_SIZE,
        )
        dummy = torch.zeros(1, 3, SAM2_IMAGE_SIZE, SAM2_IMAGE_SIZE, device=self.device)
        start = time.perf_counter()
        try:
            with torch.inference_mode(), self._autocast():
                model.forward_image(dummy)
        except Exception as e:
            raise_if_cuda_lost(e)
            raise
        logger.info(
            "Compiled image encoder warmup finished in %.1fs",
            time.perf_counter() - start,
        )

    def _autocast(self):
        """Enable CUDA autocast only on CUDA; CPU/MPS leave dtypes unchanged."""
        if self.device.type != "cuda":
            return nullcontext()
        major = torch.cuda.get_device_properties(0).major
        dtype = torch.bfloat16 if major >= 8 else torch.float16
        return torch.autocast("cuda", dtype=dtype)

    def create_predictor(self) -> SAM2ImagePredictor:
        """Return a new predictor wrapping the current weights; call set_image() before predict()."""
        with self._swap_lock:
            model = self.sam2_model
        return SAM2ImagePredictor(model)

    def create_initialized_predictor(self, image_data: bytes) -> SAM2ImagePredictor:
        """Create a predictor whose image embedding is ready for predict().

        Same PNG bytes reload the embedding from disk when the cache is enabled.
        A miss runs set_image() and stores the feature maps for the next process.
        The encoder generation (eager or compiled) is captured with the model so a
        compile swap cannot file an eager embedding under the compiled namespace.
        """
        digest = hashlib.sha256(image_data).hexdigest()
        with self._swap_lock:
            model = self.sam2_model
            mode = self._embedding_mode
        predictor = SAM2ImagePredictor(model)
        if self._try_load_disk_embedding(predictor, digest, mode):
            return predictor
        image_np = prepare_image_for_sam2(image_data)
        try:
            with torch.inference_mode(), self._autocast():
                predictor.set_image(image_np)
        except Exception as e:
            raise_if_cuda_lost(e)
            raise
        self._save_disk_embedding(predictor, digest, mode)
        return predictor

    def note_disk_embedding_used(self, image_data: bytes) -> None:
        """Bump the disk mtime when the GPU cache already held this PNG.

        UploadTile skips ``set_image()`` on an in-memory hit, so without this
        the file looks idle and the disk cap can drop a cell that is still live.
        """
        store = self._embedding_store
        if store is None or not image_data:
            return
        digest = hashlib.sha256(image_data).hexdigest()
        with self._swap_lock:
            mode = self._embedding_mode
        store.touch(digest, mode)

    def _try_load_disk_embedding(
        self,
        predictor: SAM2ImagePredictor,
        digest: str,
        mode: str,
    ) -> bool:
        store = self._embedding_store
        if store is None:
            return False
        blob = store.try_load(digest, mode)
        if blob is None:
            return False
        try:
            payload = torch.load(io.BytesIO(blob), map_location="cpu", weights_only=True)
            self._install_disk_features(predictor, payload)
        except Exception:
            logger.warning("Embedding disk blob unreadable sha=%s mode=%s", digest[:12], mode)
            store.discard(digest, mode)
            return False
        logger.info("Embedding disk hit sha=%s mode=%s bytes=%s", digest[:12], mode, len(blob))
        return True

    def _save_disk_embedding(self, predictor: SAM2ImagePredictor, digest: str, mode: str) -> None:
        store = self._embedding_store
        if store is None or predictor._features is None or not predictor._orig_hw:
            return
        try:
            blob = self._export_disk_features(predictor)
            store.save(digest, blob, mode)
        except Exception:
            logger.exception("Embedding disk write failed sha=%s mode=%s", digest[:12], mode)
            return
        logger.info("Embedding disk store sha=%s mode=%s bytes=%s", digest[:12], mode, len(blob))

    @staticmethod
    def _export_disk_features(predictor: SAM2ImagePredictor) -> bytes:
        """CPU tensor dict. weights_only loads accept tensors, not Python lists."""
        features = predictor._features
        height, width = predictor._orig_hw[0]
        high_res = list(features["high_res_feats"])
        payload: dict[str, torch.Tensor] = {
            "image_embed": features["image_embed"].detach().to("cpu").contiguous(),
            "orig_hw": torch.tensor([int(height), int(width)], dtype=torch.int64),
            "n_high": torch.tensor([len(high_res)], dtype=torch.int64),
        }
        for index, tensor in enumerate(high_res):
            payload[f"high_res_{index}"] = tensor.detach().to("cpu").contiguous()
        buffer = io.BytesIO()
        torch.save(payload, buffer)
        return buffer.getvalue()

    @staticmethod
    def _install_disk_features(predictor: SAM2ImagePredictor, payload: dict) -> None:
        """Restore set_image() state from a disk blob onto ``predictor``'s device."""
        device = predictor.device
        n_high = int(payload["n_high"].item())
        high_res = [payload[f"high_res_{index}"].to(device) for index in range(n_high)]
        orig = payload["orig_hw"].tolist()
        predictor._features = {
            "image_embed": payload["image_embed"].to(device),
            "high_res_feats": high_res,
        }
        predictor._orig_hw = [(int(orig[0]), int(orig[1]))]
        predictor._is_image_set = True
        predictor._is_batch = False

    def gpu_memory_under_pressure(self) -> bool:
        """True when free CUDA memory is below a reserve for the next tile embedding.

        The reserve is the larger of 1 GiB and 10% of the device. CPU and MPS
        have no embedding pile to trim, so they report no pressure.
        """
        if self.device.type != "cuda":
            return False
        try:
            free_bytes, total_bytes = torch.cuda.mem_get_info()
        except Exception:
            logger.exception("CUDA memory query failed")
            return False
        reserve = max(1024 ** 3, int(total_bytes * 0.10))
        return free_bytes < reserve

    def release_predictor(self, predictor: Any) -> None:
        """Drop per-image embeddings so GPU memory can be reclaimed."""
        if predictor is None:
            return
        reset = getattr(predictor, "reset_predictor", None)
        if callable(reset):
            try:
                reset()
            except Exception:
                logger.exception("Failed to reset predictor before release")
        if self.device.type == "cuda":
            torch.cuda.empty_cache()

    def _predict_with_logits(
        self,
        predictor: SAM2ImagePredictor,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool,
    ) -> Tuple[NDArray[np.bool_], NDArray[np.float32], Optional[NDArray]]:
        """Run predict() and keep logits, highest score first."""
        point_coords: NDArray[np.int_] = np.array(coordinates)
        point_labels: NDArray[np.int_] = np.array(labels)

        try:
            with torch.inference_mode(), self._autocast():
                masks, scores, logits = predictor.predict(
                    point_coords=point_coords,
                    point_labels=point_labels,
                    multimask_output=multimask_output,
                )
        except Exception as e:
            raise_if_cuda_lost(e)
            raise
        return self._sort_predict_outputs(masks, scores, logits)

    def _predict_raw(
        self,
        predictor: SAM2ImagePredictor,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool,
    ) -> Tuple[NDArray[np.bool_], NDArray[np.float32]]:
        """Run one predict() on a predictor that already has set_image() applied."""
        masks_np, scores_np, _logits = self._predict_with_logits(
            predictor, coordinates, labels, multimask_output
        )
        return masks_np, scores_np

    def _sort_predict_outputs(
        self,
        masks: Any,
        scores: Any,
        logits: Any,
    ) -> Tuple[NDArray[np.bool_], NDArray[np.float32], Optional[NDArray]]:
        masks_np = np.asarray(masks)
        if masks_np.ndim == 2:
            masks_np = np.expand_dims(masks_np, 0)
        scores_np = np.asarray(scores, dtype=np.float32).reshape(-1)
        sorted_ind = np.argsort(scores_np)[::-1]
        logits_np: Optional[NDArray] = None
        if logits is not None:
            raw_logits = np.asarray(logits)
            if raw_logits.ndim >= 1 and raw_logits.shape[0] == masks_np.shape[0]:
                logits_np = raw_logits[sorted_ind]
        return masks_np[sorted_ind].astype(np.bool_), scores_np[sorted_ind], logits_np

    def predict_ephemeral(
        self,
        image_np: NDArray,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool,
        empty_shape: Tuple[int, int],
    ) -> Tuple[NDArray[np.bool_], Optional[NDArray], float]:
        """set_image() and predict() on a throwaway predictor.

        Half-tiles are not pinned beside the cell embeddings. The embedding is
        filed under a hash of the stitched pixels, so the next call on the same
        cut reloads it and skips the encoder even when the prompt point moved.
        The predictor is released before return.
        """
        image = np.ascontiguousarray(image_np)
        digest = _image_digest(image)
        with self._swap_lock:
            model = self.sam2_model
            mode = self._embedding_mode
        predictor = SAM2ImagePredictor(model)
        try:
            if not self._try_load_disk_embedding(predictor, digest, mode):
                try:
                    with torch.inference_mode(), self._autocast():
                        predictor.set_image(image)
                except Exception as e:
                    raise_if_cuda_lost(e)
                    raise
                self._save_disk_embedding(predictor, digest, mode)
            return self.predict_tile_union(
                predictor,
                coordinates,
                labels,
                multimask_output,
                empty_shape,
            )
        finally:
            reset = getattr(predictor, "reset_predictor", None)
            if callable(reset):
                try:
                    reset()
                except Exception:
                    logger.exception("Failed to reset half-tile predictor")

    def predict_tile_union(
        self,
        predictor: SAM2ImagePredictor,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool,
        empty_shape: Tuple[int, int],
    ) -> Tuple[NDArray[np.bool_], Optional[NDArray], float]:
        """One tile predict: union of masks that cover a foreground click, plus best logits.

        Logits are the highest-scoring mask from the first predict (often 256x256).
        Callers resize them. The returned score is that union's best mask score;
        cross-tile fusion takes the minimum of these.
        """
        if not coordinates:
            height, width = empty_shape
            return np.zeros((height, width), dtype=np.bool_), None, 0.0

        masks, scores, logits = self._predict_with_logits(
            predictor, coordinates, labels, multimask_output
        )
        best_logits = None if logits is None or len(logits) == 0 else logits[0]

        def extra_predict(
            extra_coords: Sequence[Point], extra_labels: Sequence[int]
        ) -> Tuple[NDArray[np.bool_], NDArray[np.float32]]:
            extra_masks, extra_scores, _extra_logits = self._predict_with_logits(
                predictor, extra_coords, extra_labels, multimask_output=False
            )
            return extra_masks, extra_scores

        if not any(int(label) == 1 for label in labels):
            labeled, segments = process_masks(masks, scores, empty_shape=empty_shape)
            if not segments:
                height, width = empty_shape
                return np.zeros((height, width), dtype=np.bool_), best_logits, 0.0
            return segments[0]["mask"], best_logits, float(segments[0]["score"])

        union, stats = union_masks_covering_positives(
            masks,
            scores,
            coordinates,
            labels,
            extra_predict=extra_predict,
            empty_shape=empty_shape,
        )
        self._log_union_stats(stats, empty_shape)
        return union, best_logits, float(stats.score)

    def _log_union_stats(self, stats: UnionMaskStats, empty_shape: Tuple[int, int]) -> None:
        height, width = empty_shape
        if width <= 0 or height <= 0:
            height, width = 0, 0
        line = stats.log_line(width, height)
        if stats.area == 0 or stats.n_uncovered > 0 or stats.n_positives_oob > 0:
            logger.warning(line)
        else:
            logger.info(line)

    def _segment_covering_positives(
        self,
        predictor: SAM2ImagePredictor,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool,
        empty_shape: Tuple[int, int],
    ) -> Tuple[LabeledImage, List[SegmentInfo]]:
        """All-points predict, then per-uncovered-positive extras; OR into one mask."""
        initial_masks, initial_scores = self._predict_raw(
            predictor, coordinates, labels, multimask_output
        )

        if not any(int(label) == 1 for label in labels):
            labeled, segments = process_masks(initial_masks, initial_scores, empty_shape=empty_shape)
            area = int(np.count_nonzero(labeled))
            height, width = empty_shape
            n_neg = sum(1 for label in labels if int(label) != 1)
            logger.info(
                "segment %sx%s fg=0 bg=%s initial=%s segments=%s area=%s",
                width,
                height,
                n_neg,
                int(initial_masks.shape[0]) if initial_masks.ndim >= 1 else 0,
                len(segments),
                area,
            )
            return labeled, segments

        def extra_predict(
            extra_coords: Sequence[Point], extra_labels: Sequence[int]
        ) -> Tuple[NDArray[np.bool_], NDArray[np.float32]]:
            return self._predict_raw(
                predictor, extra_coords, extra_labels, multimask_output=False
            )

        union, stats = union_masks_covering_positives(
            initial_masks,
            initial_scores,
            coordinates,
            labels,
            extra_predict=extra_predict,
            empty_shape=empty_shape,
        )
        self._log_union_stats(stats, empty_shape)
        return combined_mask_to_segments(union, stats.score, empty_shape=empty_shape)

    def segment_image_with_predictor(
        self,
        predictor: SAM2ImagePredictor,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool = True,
        empty_shape: Tuple[int, int] = (0, 0),
    ) -> Tuple[LabeledImage, List[SegmentInfo]]:
        """Run predict() on a predictor that already has set_image() applied."""
        return self._segment_covering_positives(
            predictor, coordinates, labels, multimask_output, empty_shape
        )

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

        with self._shared_predictor_lock:
            try:
                with torch.inference_mode(), self._autocast():
                    self.predictor.set_image(image_np)
            except Exception as e:
                raise_if_cuda_lost(e)
                raise
            return self._segment_covering_positives(
                self.predictor,
                coordinates,
                labels,
                multimask_output,
                (height, width),
            )


def _image_digest(image: NDArray) -> str:
    """Stable id for one stitched half-tile. Shape is part of the key."""
    digest = hashlib.sha256()
    digest.update(str(image.shape).encode("ascii"))
    digest.update(str(image.dtype).encode("ascii"))
    digest.update(np.ascontiguousarray(image).tobytes())
    return digest.hexdigest()


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
