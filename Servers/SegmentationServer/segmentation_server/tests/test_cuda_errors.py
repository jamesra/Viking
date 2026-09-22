"""GPU-error classification used when CUDA context is poisoned."""

from __future__ import annotations

from segmentation_server.cuda_errors import (
    UnrecoverableGpuError,
    is_unrecoverable_cuda_error,
    raise_if_cuda_lost,
)


def test_unknown_sticky_error_is_unrecoverable() -> None:
    exc = RuntimeError(
        "CUDA error: unknown error\n"
        "Sticky error detected\n"
        "Returning 999 (CUDA_ERROR_UNKNOWN) from cuStreamIsCapturing"
    )
    assert is_unrecoverable_cuda_error(exc) is True


def test_accelerator_error_name_is_unrecoverable() -> None:
    class AcceleratorError(RuntimeError):
        pass

    exc = AcceleratorError("CUDA error: unknown error")
    assert is_unrecoverable_cuda_error(exc) is True


def test_decode_and_oom_are_recoverable() -> None:
    assert is_unrecoverable_cuda_error(ValueError("Could not decode image_data")) is False
    assert is_unrecoverable_cuda_error(RuntimeError("CUDA out of memory")) is False


def test_raise_if_cuda_lost_wraps_unknown() -> None:
    try:
        raise RuntimeError("CUDA error: unknown error")
    except RuntimeError as exc:
        try:
            raise_if_cuda_lost(exc)
        except UnrecoverableGpuError as wrapped:
            assert wrapped.__cause__ is exc
        else:
            raise AssertionError("expected UnrecoverableGpuError")
