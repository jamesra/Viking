"""The disk embedding format: lossless round trip, a version stamp, and the format-1 cache migration."""

from __future__ import annotations

import os
from pathlib import Path

import pytest

torch = pytest.importorskip("torch")

from segmentation_server import embedding_codec  # noqa: E402
from segmentation_server.migrate_embedding_cache import migrate_file  # noqa: E402


def _payload(format_version: int = embedding_codec.FORMAT) -> dict:
    generator = torch.Generator().manual_seed(3)
    return {
        "format_version": torch.tensor([format_version], dtype=torch.int64),
        "image_embed": torch.randn(1, 8, 4, 4, generator=generator),
        "orig_hw": torch.tensor([1024, 768], dtype=torch.int64),
        "n_high": torch.tensor([2], dtype=torch.int64),
        "high_res_0": torch.randn(1, 2, 16, 16, generator=generator).to(torch.bfloat16),
        "high_res_1": torch.randn(1, 4, 8, 8, generator=generator).to(torch.bfloat16),
    }


def test_round_trip_is_bit_exact_and_keeps_dtypes() -> None:
    payload = _payload()
    restored = embedding_codec.decode(embedding_codec.encode(payload))
    for name in ("image_embed", "high_res_0", "high_res_1"):
        assert restored[name].dtype == payload[name].dtype
        assert torch.equal(restored[name], payload[name])
    assert restored["orig_hw"].tolist() == [1024, 768]
    assert int(restored["n_high"].item()) == 2
    assert int(restored["format_version"].item()) == embedding_codec.FORMAT


def test_a_real_sized_embedding_compresses() -> None:
    """Smooth fp32 data, like a real embedding, ends up well below its raw size."""
    payload = _payload()
    payload["image_embed"] = torch.linspace(0, 1, 256 * 64 * 64).reshape(1, 256, 64, 64)
    assert len(embedding_codec.encode(payload)) < payload["image_embed"].numel() * 4 // 2


@pytest.mark.parametrize(
    "corrupt",
    [
        lambda blob: b"NOTABLOB" + blob[8:],
        lambda blob: blob[:-10],
        lambda blob: blob + b"x",
        lambda blob: blob.replace(b'"format":2', b'"format":3'),
        lambda blob: blob.replace(b"zlib1-byteshuffle", b"zlib9-byteshuffle"),
        lambda blob: b"",
    ],
)
def test_anything_but_a_whole_blob_of_this_format_is_refused(corrupt) -> None:
    with pytest.raises(ValueError):
        embedding_codec.decode(corrupt(embedding_codec.encode(_payload())))


def test_migration_rewrites_a_format_1_file_keeping_its_mtime(tmp_path: Path) -> None:
    old = tmp_path / "ab" / "abcd.pt"
    old.parent.mkdir()
    payload = _payload(format_version=1)
    torch.save(payload, old)
    os.utime(old, ns=(1_700_000_000_000_000_000, 1_700_000_000_000_000_000))

    outcome, _, new_bytes = migrate_file(str(old))

    new = old.with_suffix(".emb")
    assert outcome == "migrated" and not old.exists() and new.stat().st_size == new_bytes
    assert new.stat().st_mtime_ns == 1_700_000_000_000_000_000
    restored = embedding_codec.decode(new.read_bytes())
    assert torch.equal(restored["image_embed"], payload["image_embed"])
    assert torch.equal(restored["high_res_0"], payload["high_res_0"])


def test_migration_drops_an_unreadable_file(tmp_path: Path) -> None:
    old = tmp_path / "bad.pt"
    old.write_bytes(b"not a torch file")

    outcome, _, _ = migrate_file(str(old))

    assert outcome == "dropped" and not old.exists() and not old.with_suffix(".emb").exists()
