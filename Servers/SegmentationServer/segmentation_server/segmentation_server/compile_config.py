"""SAM2 image-encoder torch.compile flags. Kept torch-free for unit tests."""

from __future__ import annotations

import logging
import os

logger = logging.getLogger(__name__)

COMPILE_IMAGE_ENCODER_ENV = "SAM2_COMPILE_IMAGE_ENCODER"
COMPILE_IMAGE_ENCODER_HYDRA = "++model.compile_image_encoder=True"
SAM2_IMAGE_SIZE = 1024

_TRUTHY = frozenset({"1", "true", "yes", "on"})
_FALSY = frozenset({"0", "false", "no", "off", ""})


def env_flag_enabled(name: str, default: bool = True) -> bool:
    """True when *name* is truthy, ``default`` when it is unset, false for 0/false/no/off or blank.

    A value that is none of those (a typo such as ``ture``) is logged and treated as **false**,
    not as the default: a flag that silently turned a feature on because it was misspelled would
    be worse than one that visibly turned it off.
    """
    raw = os.environ.get(name)
    if raw is None:
        return default
    stripped = raw.strip().lower()
    if stripped in _FALSY:
        return False
    if stripped in _TRUTHY:
        return True
    logger.warning(
        "%s=%r is not a recognised on/off value (use 1/true/yes/on or 0/false/no/off); treating it as off",
        name,
        raw,
    )
    return False


def env_compile_image_encoder_enabled() -> bool:
    return env_flag_enabled(COMPILE_IMAGE_ENCODER_ENV, default=True)


def hydra_overrides_for_image_encoder(compile_image_encoder: bool, device_type: str) -> list[str]:
    """Hydra overrides for build_sam2. Compile is CUDA-only; SAM2 uses dynamic=False."""
    if compile_image_encoder and device_type == "cuda":
        return [COMPILE_IMAGE_ENCODER_HYDRA]
    return []
