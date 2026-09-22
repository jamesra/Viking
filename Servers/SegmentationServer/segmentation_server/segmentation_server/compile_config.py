"""SAM2 image-encoder torch.compile flags. Kept torch-free for unit tests."""

from __future__ import annotations

import os

COMPILE_IMAGE_ENCODER_ENV = "SAM2_COMPILE_IMAGE_ENCODER"
COMPILE_IMAGE_ENCODER_HYDRA = "++model.compile_image_encoder=True"
SAM2_IMAGE_SIZE = 1024

_TRUTHY = frozenset({"1", "true", "yes", "on"})
_FALSY = frozenset({"0", "false", "no", "off", ""})


def env_flag_enabled(name: str, default: bool = True) -> bool:
    """True when *name* is unset or truthy; false for 0/false/no/off."""
    raw = os.environ.get(name)
    if raw is None:
        return default
    stripped = raw.strip().lower()
    if stripped in _FALSY:
        return False
    if stripped in _TRUTHY:
        return True
    return default


def env_compile_image_encoder_enabled() -> bool:
    return env_flag_enabled(COMPILE_IMAGE_ENCODER_ENV, default=True)


def hydra_overrides_for_image_encoder(compile_image_encoder: bool, device_type: str) -> list[str]:
    """Hydra overrides for build_sam2. Compile is CUDA-only; SAM2 uses dynamic=False."""
    if compile_image_encoder and device_type == "cuda":
        return [COMPILE_IMAGE_ENCODER_HYDRA]
    return []
