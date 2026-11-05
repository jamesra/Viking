"""Check a native build of Viking.SegmentationPrompts against the .NET tests' golden request.

The library is only shared if every build gives the same bytes: the golden input's request must have the SHA-256 the
.NET tests pin (PromptBuilderTests.GoldenDigest). Run where the native library will be used, e.g. in the server image:

    python check_native.py out/Viking.SegmentationPrompts.Native.so ../Viking.SegmentationPrompts.Tests
"""

from __future__ import annotations

import ctypes
import hashlib
import re
import sys
from pathlib import Path
from typing import Sequence, Tuple

_BYTES = ctypes.POINTER(ctypes.c_uint8)


def _load(path: str) -> ctypes.CDLL:
    library = ctypes.CDLL(path)
    out = [ctypes.POINTER(_BYTES), ctypes.POINTER(ctypes.c_ssize_t)]
    library.sp_build_start.argtypes = [ctypes.c_char_p, ctypes.c_ssize_t, *out]
    library.sp_reach.argtypes = [ctypes.c_char_p, ctypes.c_ssize_t, *out]
    library.sp_background_for.argtypes = [
        ctypes.c_char_p, ctypes.c_ssize_t, ctypes.c_char_p, ctypes.c_ssize_t,
        ctypes.POINTER(ctypes.c_int64), ctypes.c_ssize_t, *out,
    ]
    library.sp_free.argtypes = [_BYTES]
    for name in ("sp_build_start", "sp_reach", "sp_background_for"):
        getattr(library, name).restype = ctypes.c_int
    library.sp_free.restype = None
    return library


def _call(library: ctypes.CDLL, name: str, *args) -> Tuple[int, bytes]:
    buffer, length = _BYTES(), ctypes.c_ssize_t()
    status = getattr(library, name)(*args, ctypes.byref(buffer), ctypes.byref(length))
    try:
        return status, ctypes.string_at(buffer, length.value)
    finally:
        library.sp_free(buffer)


def main(argv: Sequence[str]) -> int:
    library = _load(argv[1])
    tests = Path(argv[2])
    golden = bytes.fromhex((tests / "Fixtures" / "golden_prompt_input.hex").read_text().strip())
    expected = re.search(r'GoldenDigest = "([0-9A-F]{64})"', (tests / "PromptBuilderTests.cs").read_text()).group(1)

    status, start = _call(library, "sp_build_start", golden, len(golden))
    digest = hashlib.sha256(start).hexdigest().upper()
    print(f"sp_build_start: status {status}, {len(start)} bytes, sha256 {digest}")
    failures = []
    if status != 0 or digest != expected:
        failures.append(f"the golden request differs from the .NET build's ({expected})")

    status, reach = _call(library, "sp_reach", golden, len(golden))
    if status != 0 or not reach:
        failures.append(f"sp_reach: status {status}")

    sent = (ctypes.c_int64 * 1)(101)
    status, _ = _call(library, "sp_background_for", golden, len(golden), b"", 0, sent, 1)
    if status != 0:
        failures.append(f"sp_background_for: status {status}")

    status, message = _call(library, "sp_build_start", b"", 0)
    print(f"empty input: status {status}, {message.decode('utf-8')!r}")
    if status != 1:
        failures.append("an empty input is not reported as an invalid argument")

    for failure in failures:
        print(f"FAILED: {failure}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
