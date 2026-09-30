"""Disk embedding store: keying, touch-on-read, and byte cap."""

from __future__ import annotations

import os
import time

from segmentation_server.embedding_store import EmbeddingStore, checkpoint_stamp


def test_checkpoint_stamp_changes_when_the_file_changes(tmp_path) -> None:
    checkpoint = tmp_path / "model.pt"
    checkpoint.write_bytes(b"aaa")
    first = checkpoint_stamp(str(checkpoint), "configs/sam2.1/sam2.1_hiera_l.yaml")
    later = checkpoint.stat().st_mtime_ns + 1_000_000
    os.utime(checkpoint, ns=(later, later))
    checkpoint.write_bytes(b"aaaa")
    second = checkpoint_stamp(str(checkpoint), "configs/sam2.1/sam2.1_hiera_l.yaml")
    assert first != second
    assert len(first) == 16


def test_roundtrip_is_scoped_to_encoder_mode(tmp_path) -> None:
    store = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=None)
    digest = "ab" + ("1" * 62)
    store.save(digest, b"eager-bytes", "eager")
    assert store.try_load(digest, "eager") == b"eager-bytes"
    assert store.try_load(digest, "compiled") is None
    store.save(digest, b"compiled-bytes", "compiled")
    assert store.try_load(digest, "compiled") == b"compiled-bytes"
    assert store.try_load(digest, "eager") == b"eager-bytes"


def test_invalid_digest_is_rejected(tmp_path) -> None:
    store = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=None)
    assert store.try_load("../etc/passwd", "eager") is None
    try:
        store.save("not-a-hash", b"x", "eager")
    except ValueError:
        return
    raise AssertionError("expected ValueError")


def test_cap_evicts_the_least_recently_used_blob(tmp_path) -> None:
    store = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=120)
    older = "aa" + ("0" * 62)
    newer = "bb" + ("1" * 62)
    kept = "cc" + ("2" * 62)
    store.save(older, b"o" * 50, "eager")
    time.sleep(0.02)
    store.save(newer, b"n" * 50, "eager")
    time.sleep(0.02)
    assert store.try_load(older, "eager") == b"o" * 50
    store.save(kept, b"k" * 50, "eager")
    assert store.try_load(older, "eager") == b"o" * 50
    assert store.try_load(newer, "eager") is None
    assert store.try_load(kept, "eager") == b"k" * 50
    assert store.total_bytes <= 120


def test_reindex_sees_files_from_a_previous_process(tmp_path) -> None:
    digest = "dd" + ("3" * 62)
    first = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=10_000)
    first.save(digest, b"persisted", "compiled")
    second = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=10_000)
    assert second.try_load(digest, "compiled") == b"persisted"
    assert second.total_bytes == first.total_bytes
