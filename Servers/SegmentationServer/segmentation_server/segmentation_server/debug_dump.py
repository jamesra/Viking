"""Opt-in dump of one SegmentTiles growth result for offline mask inspection.

Set SEGMENTATION_DEBUG_DUMP=1 to write one .npz per call under SEGMENTATION_DEBUG_DUMP_DIR
(default: a ``debug-dumps`` folder inside the embedding cache mount, which the embedding store
never touches because its folder names are not checkpoint stamps). Only the newest
MAX_DUMPS files are kept. Failures are logged and swallowed so a debug aid never breaks a request.

Each file holds the fused mask, the 512 core mask of every cell the growth walk wrote, and a JSON
``meta`` entry with the prompts, uploaded and requested tiles, and the mosaic origin, so a
straight edge in a mask can be tied to a cell core border. When the prediction is available it also
holds ``raw_r{row}_c{col}`` (the whole 1024 SAM2 answer) and ``kept_r{row}_c{col}`` (the pieces that
held a positive click), so ``raw & ~kept`` shows what the server discarded and ``raw`` alone shows
where SAM2 itself stopped. ``logits_r{row}_c{col}`` is SAM2's logit map for that prediction
(float16, usually 256x256, Y-down over the same 1024 window; upsample to compare with ``raw``).
A margin pixel was accepted only where its logit reached ``meta["margin_logit_min"]``. A neighbor's margin pixel was removed from a core where
the owning cell's own logit was below ``meta["owner_veto_logit"]``.
"""

import json
import logging
import os
import time
from pathlib import Path
from typing import Any, Mapping, Optional, Sequence, Tuple

import numpy as np

logger = logging.getLogger(__name__)

ENV_ENABLE = "SEGMENTATION_DEBUG_DUMP"
ENV_DIR = "SEGMENTATION_DEBUG_DUMP_DIR"
MAX_DUMPS = 40


def dump_enabled() -> bool:
    return os.environ.get(ENV_ENABLE, "").strip().lower() in ("1", "true", "yes", "on")


def dump_directory() -> Path:
    configured = os.environ.get(ENV_DIR)
    if configured:
        return Path(configured)
    cache = os.environ.get("SEGMENTATION_EMBEDDING_CACHE", "/var/cache/segmentation-embeddings")
    return Path(cache) / "debug-dumps"


def dump_growth(
    session: str,
    *,
    fused_mask: np.ndarray,
    origin: Tuple[int, int],
    cells: Mapping[Any, Any],
    uploaded: Sequence[Any],
    requested: Sequence[Any],
    foreground: Sequence[Tuple[int, int]],
    background: Sequence[Tuple[int, int]],
    score: float,
    directory: Optional[Path] = None,
    margin_logit_min: Optional[float] = None,
    owner_veto_logit: Optional[float] = None,
    request_id: int = 0,
) -> Optional[Path]:
    """Write one growth result. Returns the file path, or None when disabled or on failure.

    ``cells`` maps a cell (with ``row`` and ``col``) to a prediction (with ``core`` and
    ``score``). Cores are stored as bool arrays under ``cell_r{row}_c{col}``.
    """
    if not dump_enabled():
        return None
    try:
        target = directory if directory is not None else dump_directory()
        target.mkdir(parents=True, exist_ok=True)
        stamp = time.strftime("%Y%m%d-%H%M%S", time.gmtime())
        path = target / f"dump-{stamp}-{int(time.time() * 1000) % 1000:03d}-{session}.npz"
        arrays = {"fused": np.asarray(fused_mask, dtype=np.bool_)}
        cell_scores = {}
        for cell, pred in cells.items():
            name = f"cell_r{int(cell.row)}_c{int(cell.col)}"
            arrays[name] = np.asarray(pred.core, dtype=np.bool_)
            cell_scores[name] = float(pred.score)
            raw = getattr(pred, "raw", None)
            kept = getattr(pred, "kept", None)
            if raw is not None:
                arrays[f"raw_r{int(cell.row)}_c{int(cell.col)}"] = np.asarray(raw, dtype=np.bool_)
            if kept is not None:
                arrays[f"kept_r{int(cell.row)}_c{int(cell.col)}"] = np.asarray(kept, dtype=np.bool_)
            logits = getattr(pred, "logits", None)
            if logits is not None:
                arrays[f"logits_r{int(cell.row)}_c{int(cell.col)}"] = np.asarray(logits, dtype=np.float16)
        meta = {
            "session": session,
            "request_id": int(request_id),
            "origin": [int(origin[0]), int(origin[1])],
            "score": float(score),
            "uploaded": [[int(t.row), int(t.col)] for t in uploaded],
            "requested": [[int(t.row), int(t.col)] for t in requested],
            "foreground": [[int(x), int(y)] for x, y in foreground],
            "background": [[int(x), int(y)] for x, y in background],
            "cell_scores": cell_scores,
            "margin_logit_min": None if margin_logit_min is None else float(margin_logit_min),
            "owner_veto_logit": None if owner_veto_logit is None else float(owner_veto_logit),
        }
        arrays["meta"] = np.array(json.dumps(meta))
        np.savez_compressed(path, **arrays)
        _prune(target)
        return path
    except Exception:
        logger.exception("Debug dump failed")
        return None


def _prune(directory: Path, keep: int = MAX_DUMPS) -> None:
    dumps = sorted(directory.glob("dump-*.npz"), key=lambda p: p.stat().st_mtime)
    for stale in dumps[: max(0, len(dumps) - keep)]:
        try:
            stale.unlink()
        except OSError:
            logger.warning("Could not remove old debug dump %s", stale)
