"""Classify GPU failures that make this process unusable."""

from __future__ import annotations


class UnrecoverableGpuError(RuntimeError):
    """CUDA context is dead. The process should exit so Docker can restart."""


_UNRECOVERABLE_MARKERS = (
    "cuda error: unknown error",
    "cudaerrorunknown",
    "cuda_error_unknown",
    "sticky error",
    "illegal instruction",
    "invalid device context",
    "device-side assert",
    "cudaerrorillegaladdress",
    "an illegal memory access",
)


def is_unrecoverable_cuda_error(exc: BaseException) -> bool:
    """True when the GPU context is poisoned and in-process recovery will not work.

    Typical after Windows sleep / WSL GPU reset: the next CUDA call raises
    CUDA_ERROR_UNKNOWN (999) from cuStreamIsCapturing and every later call fails.
    """
    combined = f"{type(exc).__name__} {exc}".lower()
    return any(marker in combined for marker in _UNRECOVERABLE_MARKERS)


def raise_if_cuda_lost(exc: BaseException) -> None:
    """Re-raise UnrecoverableGpuError when *exc* means the CUDA context is dead."""
    if is_unrecoverable_cuda_error(exc):
        raise UnrecoverableGpuError(
            "CUDA context lost (GPU reset or host sleep). "
            "The process will exit so Docker can restart."
        ) from exc
