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
    env_box_multimask_enabled,
    hydra_overrides_for_image_encoder,
)
from segmentation_server.embedding_store import EmbeddingStore, open_embedding_store
from segmentation_server.cuda_errors import raise_if_cuda_lost
from segmentation_server.encoder_parity import EncoderParityError, compare_feature_maps
from segmentation_server.model_capabilities import SAM2_CAPABILITIES
from segmentation_server.mask_utils import (
    DEFAULT_MASK_THRESHOLD,
    LabeledImage,
    Point,
    SegmentInfo,
    combined_mask_to_segments,
    count_covered_points,
    mask_grows_past_box,
    prepare_image_for_sam2,
    process_masks,
    select_mask,
)

logger = logging.getLogger(__name__)

DEFAULT_MODEL_CFG = "configs/sam2.1/sam2.1_hiera_l.yaml"
DEFAULT_CHECKPOINT_NAME = "sam2.1_hiera_large.pt"

_GENERATION_ATTR = "_viking_encoder_generation"
# Layout of a disk embedding blob. It touches private SAM2 predictor state (_features, _orig_hw),
# so a blob carries this number and one with another value is discarded instead of installed.
# Bump it when the payload or the SAM2 pin changes what those fields mean.
DISK_BLOB_FORMAT = 1
# Sanity bounds for a blob read back from disk. SAM2 keeps two high-resolution levels and the
# largest image the server accepts is far below 65536 pixels on a side; anything outside these
# is a corrupt or foreign file and is discarded instead of installed on the predictor.
_MAX_DISK_HIGH_RES_LEVELS = 4
_MAX_DISK_IMAGE_SIDE = 65536
_PARITY_SEED = 20261002

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
        # One SAM2 forward at a time on the shared weights, whatever the inference pool size.
        # Re-entrant because a predict path may call back into another guarded step. Lock
        # order everywhere is per-predictor lock first, then this one.
        self._gpu_lock = threading.RLock()
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
            self._verify_compiled_parity(compiled)
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
            with self._callback_lock:
                self.compile_status = COMPILE_FAILED

    @property
    def encoder_generation(self) -> str:
        """``"eager"`` or ``"compiled"``: which encoder new embeddings are made with right now."""
        with self._swap_lock:
            return self._embedding_mode

    @staticmethod
    def predictor_generation(predictor: Any) -> Optional[str]:
        """The encoder generation a predictor's embedding was made with, or None if unknown.

        Stamped when the predictor is created, because a predictor keeps wrapping the model it
        was built from after a swap, so its generation can lag the model's.
        """
        value = getattr(predictor, _GENERATION_ATTR, None)
        return value if isinstance(value, str) else None

    def _encoder_features(self, model: Any) -> List[NDArray]:
        """Feature maps ``model``'s image encoder produces for a fixed pseudo-random image.

        Runs under the same autocast and inference mode as serving. The input is seeded so the
        eager and compiled encoders see identical pixels.
        """
        generator = torch.Generator(device="cpu")
        generator.manual_seed(_PARITY_SEED)
        image = torch.randn(
            1, 3, SAM2_IMAGE_SIZE, SAM2_IMAGE_SIZE, generator=generator
        ).to(self.device)
        with torch.inference_mode(), self._autocast():
            out = model.forward_image(image)
        maps = [out["vision_features"], *out["backbone_fpn"]]
        return [m.detach().float().cpu().numpy() for m in maps]

    def _verify_compiled_parity(self, compiled: Any) -> None:
        """Refuse the swap unless the compiled encoder agrees numerically with the eager one.

        Both forwards take the GPU lock: the eager one because serving threads share that model,
        the compiled one so it does not run a full encoder pass beside a serving prediction.
        Raises ``EncoderParityError`` on disagreement, which leaves the server on eager.
        """
        with self._swap_lock:
            eager = self.sam2_model
        with self._gpu_lock:
            reference = self._encoder_features(eager)
        with self._gpu_lock:
            candidate = self._encoder_features(compiled)
        result = compare_feature_maps(reference, candidate)
        if not result.ok:
            raise EncoderParityError(result.detail)
        logger.info("Compiled image encoder matches eager: %s", result.detail)

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

    def _encode_image(self, predictor: SAM2ImagePredictor, image_np: NDArray) -> None:
        """``set_image()`` under the GPU lock, turning a dead CUDA context into its own error."""
        try:
            with self._gpu_lock, torch.inference_mode(), self._autocast():
                predictor.set_image(image_np)
        except Exception as e:
            raise_if_cuda_lost(e)
            raise

    def create_initialized_predictor(
        self, image_data: bytes, image_np: Optional[NDArray] = None
    ) -> SAM2ImagePredictor:
        """Create a predictor whose image embedding is ready for predict().

        The embedding is filed under a hash of the decoded pixels (:func:`_image_digest`), the
        same key an offset cell window gets in :meth:`predict_ephemeral`, so identical pixels
        reload from disk whichever way they arrived. A miss runs set_image() and stores the
        feature maps for the next process. ``image_np`` is the already decoded image when the
        caller has one (the upload path decodes to validate), which saves decoding it again.
        The encoder generation (eager or compiled) is captured with the model so a compile swap
        cannot file an eager embedding under the compiled namespace.
        """
        if image_np is None:
            image_np = prepare_image_for_sam2(image_data)
        digest = _image_digest(image_np)
        with self._swap_lock:
            model = self.sam2_model
            mode = self._embedding_mode
        predictor = SAM2ImagePredictor(model)
        setattr(predictor, _GENERATION_ATTR, mode)
        if self._try_load_disk_embedding(predictor, digest, mode):
            return predictor
        self._encode_image(predictor, image_np)
        self._save_disk_embedding(predictor, digest, mode)
        return predictor

    def note_disk_embedding_used(self, image_data: bytes, image_np: Optional[NDArray] = None) -> None:
        """Bump the disk mtime when the GPU cache already held this image.

        UploadTile skips ``set_image()`` on an in-memory hit, so without this
        the file looks idle and the disk cap can drop a cell that is still live.
        """
        store = self._embedding_store
        if store is None or not image_data:
            return
        if image_np is None:
            image_np = prepare_image_for_sam2(image_data)
        digest = _image_digest(image_np)
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
            payload = torch.load(io.BytesIO(blob), map_location=predictor.device, weights_only=True)
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
            if not store.save(digest, blob, mode):
                return
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
            "format_version": torch.tensor([DISK_BLOB_FORMAT], dtype=torch.int64),
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
        found = payload.get("format_version")
        if found is None or int(found.item()) != DISK_BLOB_FORMAT:
            raise ValueError(f"embedding blob format {found} is not {DISK_BLOB_FORMAT}")
        n_high = int(payload["n_high"].item())
        if not 0 < n_high <= _MAX_DISK_HIGH_RES_LEVELS:
            raise ValueError(f"embedding blob has {n_high} high-resolution levels")
        orig = payload["orig_hw"].tolist()
        if len(orig) != 2 or not all(0 < int(side) <= _MAX_DISK_IMAGE_SIDE for side in orig):
            raise ValueError(f"embedding blob has implausible original size {orig}")
        image_embed = payload["image_embed"]
        high_res_cpu = [payload[f"high_res_{index}"] for index in range(n_high)]
        for name, tensor in (("image_embed", image_embed), *((f"high_res_{i}", t) for i, t in enumerate(high_res_cpu))):
            if tensor.dim() != 4 or tensor.shape[0] != 1 or tensor.numel() == 0:
                raise ValueError(f"embedding blob tensor {name} has shape {tuple(tensor.shape)}, expected (1, C, H, W)")
        high_res = [tensor.to(device) for tensor in high_res_cpu]
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
        """Drop per-image embeddings so GPU memory can be reclaimed.

        The allocator keeps freed blocks for reuse, and the next tile embedding is the same size,
        so handing them back to the driver on every release only makes the next ``set_image()``
        allocate again. ``empty_cache`` runs only when free memory is already short.
        """
        if predictor is None:
            return
        reset = getattr(predictor, "reset_predictor", None)
        if callable(reset):
            try:
                reset()
            except Exception:
                logger.exception("Failed to reset predictor before release")
        if self.device.type == "cuda" and self.gpu_memory_under_pressure():
            torch.cuda.empty_cache()

    def _predict_with_logits(
        self,
        predictor: SAM2ImagePredictor,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool,
        box: Optional[Sequence[int]] = None,
        mask_threshold: float = DEFAULT_MASK_THRESHOLD,
        use_mask_input: bool = False,
    ) -> Tuple[NDArray[np.bool_], NDArray[np.float32], Optional[NDArray]]:
        """Run predict() and keep logits, highest score first.

        ``box`` is one ``(x0, y0, x1, y1)`` prompt in image pixels, Y-down; SAM2 accepts a
        single box per predict() alongside the points.

        ``mask_threshold`` is the logit a pixel must exceed to be object. predict() is asked
        for logits and thresholded here, rather than setting ``predictor.mask_threshold``,
        because a pinned tile predictor is shared between requests. The default 0.0 is
        SAM2's own.

        ``use_mask_input`` runs predict() a second time with the first pass's best
        low-resolution logits as SAM2's ``mask_input`` and the same prompts. Single-mask
        output only: with ``multimask_output`` the flag is ignored, because mask_input is one mask.
        """
        point_coords: Optional[NDArray[np.int_]] = np.array(coordinates) if len(coordinates) else None
        point_labels: Optional[NDArray[np.int_]] = np.array(labels) if len(coordinates) else None
        box_xyxy = None if box is None else np.array(box, dtype=np.float32)

        try:
            with self._gpu_lock, torch.inference_mode(), self._autocast():
                masks, scores, logits = predictor.predict(
                    point_coords=point_coords,
                    point_labels=point_labels,
                    box=box_xyxy,
                    multimask_output=multimask_output,
                    return_logits=True,
                )
                if use_mask_input and not multimask_output:
                    best = int(np.argmax(np.asarray(scores).reshape(-1)))
                    masks, scores, logits = predictor.predict(
                        point_coords=point_coords,
                        point_labels=point_labels,
                        box=box_xyxy,
                        mask_input=np.asarray(logits)[best:best + 1],
                        multimask_output=False,
                        return_logits=True,
                    )
        except Exception as e:
            raise_if_cuda_lost(e)
            raise
        thresholded = np.asarray(masks) > float(mask_threshold)
        return self._sort_predict_outputs(thresholded, scores, logits)

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
        box: Optional[Sequence[int]] = None,
        mask_threshold: float = DEFAULT_MASK_THRESHOLD,
        use_mask_input: bool = False,
    ) -> Tuple[NDArray[np.bool_], Optional[NDArray], float]:
        """set_image() and predict() on a throwaway predictor.

        Offset cell windows are not pinned beside the aligned tile embeddings. The embedding is
        filed under a hash of the window pixels, so the next call on the same
        window reloads it and skips the encoder even when the prompt point moved.
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
                self._encode_image(predictor, image)
                self._save_disk_embedding(predictor, digest, mode)
            return self.predict_tile(
                predictor,
                coordinates,
                labels,
                multimask_output,
                empty_shape,
                box,
                mask_threshold,
                use_mask_input,
            )
        finally:
            self.release_predictor(predictor)

    def predict_tile(
        self,
        predictor: SAM2ImagePredictor,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool,
        empty_shape: Tuple[int, int],
        box: Optional[Sequence[int]] = None,
        mask_threshold: float = DEFAULT_MASK_THRESHOLD,
        use_mask_input: bool = False,
    ) -> Tuple[NDArray[np.bool_], Optional[NDArray], float]:
        """One tile predict: the one mask the selection rule picks, its logits, and its score.

        ``box``, ``mask_threshold`` and ``use_mask_input`` are described on
        ``_predict_with_logits``. The mask is chosen by :func:`select_mask`, the same rule every
        RPC uses: with a box, the highest-scoring candidate that covers at least 95% of the box and extends past it; without
        one, the candidate that covers the most foreground points. Nothing is combined and no
        looser prompt is tried.

        Logits are the chosen mask's (often 256x256); callers resize them. The returned score is
        its score, and cross-tile fusion takes the minimum of these.

        Raises:
            NoMatchingMask: No candidate satisfies the rule.
        """
        if not coordinates and box is None:
            height, width = empty_shape
            return np.zeros((height, width), dtype=np.bool_), None, 0.0

        # A box prompt can ask SAM2 for its three candidates (sub-part, part, whole) so the rule
        # below chooses among them instead of only accepting or refusing a single guess. Off unless
        # SEGMENTATION_BOX_MULTIMASK is set. mask_input refines one mask, so it is skipped then.
        box_multimask = box is not None and not multimask_output and env_box_multimask_enabled()
        masks, scores, logits = self._predict_with_logits(
            predictor,
            coordinates,
            labels,
            multimask_output or box_multimask,
            box,
            mask_threshold,
            use_mask_input,
        )
        positives = [coord for coord, label in zip(coordinates, labels) if int(label) == 1]
        if box_multimask:
            self._log_box_candidates(masks, scores, box)
        index = select_mask(masks, scores, box=box, positives=positives)
        chosen_logits = None if logits is None or len(logits) <= index else logits[index]
        return masks[index], chosen_logits, float(scores[index])

    @staticmethod
    def _log_box_candidates(masks: NDArray, scores: NDArray, box: Sequence[int]) -> None:
        """One line naming each candidate for a box prompt: score, pixels, and whether it answers the box.

        This is how the multimask trial is judged from real requests: the line shows what SAM2
        offered, and the rule's choice follows from it.
        """
        parts = []
        for index in range(len(scores)):
            mask = masks[index]
            parts.append(
                f"#{index} score={float(scores[index]):.3f} px={int(np.count_nonzero(mask))} "
                f"answers_box={mask_grows_past_box(mask, box)}"
            )
        logger.info("Box prompt %s candidates: %s", tuple(int(v) for v in box), "; ".join(parts))

    def _segment_viewport(
        self,
        predictor: SAM2ImagePredictor,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool,
        empty_shape: Tuple[int, int],
    ) -> Tuple[LabeledImage, List[SegmentInfo]]:
        """One predict, then the one mask :func:`select_mask` picks: the most foreground points covered.

        A prompt with no foreground point has nothing to select against, so every non-empty
        candidate is returned as its own segment.

        Raises:
            NoMatchingMask: No candidate covers any foreground point.
        """
        masks, scores = self._predict_raw(predictor, coordinates, labels, multimask_output)
        height, width = empty_shape
        positives = [coord for coord, label in zip(coordinates, labels) if int(label) == 1]

        if not positives:
            labeled, segments = process_masks(masks, scores, empty_shape=empty_shape)
            logger.info(
                "segment %sx%s fg=0 bg=%s candidates=%s segments=%s area=%s",
                width,
                height,
                len(coordinates),
                int(masks.shape[0]) if masks.ndim >= 1 else 0,
                len(segments),
                int(np.count_nonzero(labeled)),
            )
            return labeled, segments

        index = select_mask(masks, scores, positives=positives)
        chosen = masks[index]
        logger.info(
            "segment %sx%s fg=%s bg=%s candidates=%s chose=%s covered=%s/%s area=%s score=%.3f",
            width,
            height,
            len(positives),
            len(coordinates) - len(positives),
            int(masks.shape[0]),
            index,
            count_covered_points(chosen, positives),
            len(positives),
            int(np.count_nonzero(chosen)),
            float(scores[index]),
        )
        return combined_mask_to_segments(chosen, float(scores[index]), empty_shape=empty_shape)

    def segment_image_with_predictor(
        self,
        predictor: SAM2ImagePredictor,
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool = True,
        empty_shape: Tuple[int, int] = (0, 0),
    ) -> Tuple[LabeledImage, List[SegmentInfo]]:
        """Run predict() on a predictor that already has set_image() applied.

        Raises:
            NoMatchingMask: No candidate covers any foreground point.
        """
        return self._segment_viewport(predictor, coordinates, labels, multimask_output, empty_shape)

    def segment_image(
        self,
        image_np: NDArray[np.uint8],
        coordinates: Sequence[Point],
        labels: Sequence[int],
        multimask_output: bool = True,
    ) -> Tuple[LabeledImage, List[SegmentInfo]]:
        """Inline-image path: set_image() + predict() on the shared predictor.

        ``image_np`` is the already decoded RGB image, so the caller has checked its size against
        what the client declared. Concurrent callers are serialized on `_shared_predictor_lock`
        because the shared predictor's embedding state is not thread-safe.

        Raises:
            NoMatchingMask: No candidate covers any foreground point.
        """
        height, width = int(image_np.shape[0]), int(image_np.shape[1])
        with self._shared_predictor_lock:
            self._encode_image(self.predictor, image_np)
            return self._segment_viewport(
                self.predictor,
                coordinates,
                labels,
                multimask_output,
                (height, width),
            )


def _image_digest(image: NDArray) -> str:
    """Stable id for one cropped cell window. Shape is part of the key."""
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
