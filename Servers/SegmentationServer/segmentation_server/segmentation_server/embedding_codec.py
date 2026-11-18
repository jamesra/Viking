"""On-disk encoding of a SAM2 image embedding: lossless, compressed, with a version stamp.

An embedding is three float tensors (``image_embed`` fp32, two ``high_res`` maps in bf16), about 10 MiB as
raw bytes. Floats compress poorly as they lie in memory, because each value's sign/exponent byte sits next to
noisy mantissa bytes. Storing byte 0 of every value, then byte 1, and so on ("byte shuffle", as in Blosc and
HDF5) puts the slowly varying exponent bytes together, and zlib then shrinks the entry to about 5.8 MiB.
The values come back bit for bit, so masks decoded from a stored embedding equal those from a fresh one
(measured 2026-11-18 on 720 masks; rounding ``image_embed`` to bf16 instead changed about 1% of them).

Layout, all little-endian::

    MAGIC (8 bytes) | header length (uint32) | JSON header | compressed tensor streams, in header order

The header carries ``format`` (:data:`FORMAT`), the codec name, ``orig_hw`` and each tensor's name, dtype,
shape and compressed length. A blob with another magic, format or codec is rejected with ``ValueError`` so
the caller discards it; there is no reader for older formats (the cache is migrated instead, see
:mod:`segmentation_server.migrate_embedding_cache`). Bump :data:`FORMAT` whenever the layout or the codec
changes, so a future refinement (another compressor, quantization) can tell its own files apart.
"""

from __future__ import annotations

import json
import struct
import zlib
from typing import Dict, List

import numpy as np
import torch

MAGIC = b"VKSAM2E\n"
# Version stamp of this layout. 1 was the old torch.save dict (``.pt`` files); 2 is this codec.
FORMAT = 2
CODEC = "zlib1-byteshuffle"
# zlib level 1: 5.83 MiB per entry against 5.63 MiB at level 6, but about 110 ms to compress instead of 345 ms
# on the serving host, which keeps the background write thread ahead of the encoder. Decompression is ~25 ms
# at any level, about half of what torch.load of the old 10 MiB file took.
_ZLIB_LEVEL = 1
_HEADER_LENGTH = struct.Struct("<I")
# A header is a few hundred bytes; anything far larger is a corrupt or foreign file.
_MAX_HEADER_BYTES = 64 * 1024
_DTYPES: Dict[str, torch.dtype] = {
    "float32": torch.float32,
    "bfloat16": torch.bfloat16,
    "float16": torch.float16,
}
_DTYPE_NAMES = {dtype: name for name, dtype in _DTYPES.items()}


def _shuffle(raw: bytes, item_size: int) -> bytes:
    """Byte ``k`` of every value together, for k = 0 .. item_size-1."""
    return np.frombuffer(raw, dtype=np.uint8).reshape(-1, item_size).T.tobytes()


def _unshuffle(shuffled: bytes, item_size: int) -> bytes:
    """Inverse of :func:`_shuffle`."""
    return np.frombuffer(shuffled, dtype=np.uint8).reshape(item_size, -1).T.tobytes()


def tensor_names(payload: dict) -> List[str]:
    """The float tensors of an embedding payload, in storage order."""
    count = int(payload["n_high"].item())
    return ["image_embed", *(f"high_res_{index}" for index in range(count))]


def encode(payload: dict) -> bytes:
    """``payload`` (the CPU tensor dict the server builds from a predictor) as blob bytes."""
    entries = []
    streams = []
    for name in tensor_names(payload):
        tensor = payload[name].detach().to("cpu").contiguous()
        dtype_name = _DTYPE_NAMES.get(tensor.dtype)
        if dtype_name is None:
            raise ValueError(f"embedding tensor {name} has unsupported dtype {tensor.dtype}")
        raw = tensor.view(torch.uint8).numpy().tobytes()
        stream = zlib.compress(_shuffle(raw, tensor.element_size()), _ZLIB_LEVEL)
        entries.append({"name": name, "dtype": dtype_name, "shape": list(tensor.shape), "bytes": len(stream)})
        streams.append(stream)
    header = json.dumps(
        {
            "format": FORMAT,
            "codec": CODEC,
            "orig_hw": [int(side) for side in payload["orig_hw"].tolist()],
            "tensors": entries,
        },
        separators=(",", ":"),
    ).encode("utf-8")
    return b"".join([MAGIC, _HEADER_LENGTH.pack(len(header)), header, *streams])


def decode(blob: bytes) -> dict:
    """Blob bytes back to the payload dict :func:`encode` was given (``format_version`` set to :data:`FORMAT`).

    Raises ``ValueError`` for anything that is not a complete blob of this format.
    """
    view = memoryview(blob)
    prefix = len(MAGIC) + _HEADER_LENGTH.size
    if len(view) < prefix or bytes(view[: len(MAGIC)]) != MAGIC:
        raise ValueError("not an embedding blob (bad magic)")
    (header_length,) = _HEADER_LENGTH.unpack_from(view, len(MAGIC))
    if header_length > _MAX_HEADER_BYTES or prefix + header_length > len(view):
        raise ValueError(f"embedding blob header length {header_length} is implausible")
    try:
        header = json.loads(bytes(view[prefix : prefix + header_length]).decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError(f"embedding blob header unreadable: {error}") from error
    if header.get("format") != FORMAT:
        raise ValueError(f"embedding blob format {header.get('format')} is not {FORMAT}")
    if header.get("codec") != CODEC:
        raise ValueError(f"embedding blob codec {header.get('codec')!r} is not {CODEC!r}")
    entries = header.get("tensors") or []
    payload: Dict[str, torch.Tensor] = {
        "format_version": torch.tensor([FORMAT], dtype=torch.int64),
        "orig_hw": torch.tensor([int(side) for side in header.get("orig_hw", [])], dtype=torch.int64),
        "n_high": torch.tensor([max(len(entries) - 1, 0)], dtype=torch.int64),
    }
    offset = prefix + header_length
    for entry in entries:
        dtype = _DTYPES.get(entry.get("dtype"))
        if dtype is None:
            raise ValueError(f"embedding blob tensor {entry.get('name')} has unknown dtype {entry.get('dtype')}")
        length = int(entry["bytes"])
        if length < 0 or offset + length > len(view):
            raise ValueError(f"embedding blob tensor {entry.get('name')} is truncated")
        item_size = torch.empty((), dtype=dtype).element_size()
        shape = [int(side) for side in entry["shape"]]
        expected = item_size * int(np.prod(shape))
        try:
            raw = _unshuffle(zlib.decompress(view[offset : offset + length]), item_size)
        except zlib.error as error:
            raise ValueError(f"embedding blob tensor {entry.get('name')} does not decompress: {error}") from error
        if len(raw) != expected:
            raise ValueError(f"embedding blob tensor {entry.get('name')} has {len(raw)} bytes, expected {expected}")
        payload[str(entry["name"])] = torch.frombuffer(bytearray(raw), dtype=torch.uint8).view(dtype).reshape(shape)
        offset += length
    if offset != len(view):
        raise ValueError("embedding blob has trailing bytes")
    return payload
