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
    assert len(first) == 32


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

def _path_of(store: EmbeddingStore, digest: str, mode: str):
    return store._path(digest, mode)


def test_a_file_deleted_behind_the_stores_back_stops_counting_toward_the_cap(tmp_path) -> None:
    store = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=1000)
    digest = "ab" + ("3" * 62)
    store.save(digest, b"x" * 400, "eager")
    assert store.total_bytes == 400
    _path_of(store, digest, "eager").unlink()

    assert store.try_load(digest, "eager") is None

    assert store.total_bytes == 0
    store.save("cd" + ("4" * 62), b"y" * 900, "eager")
    assert store.total_bytes == 900


def test_touching_a_file_that_is_gone_drops_it_from_the_index(tmp_path) -> None:
    store = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=None)
    digest = "ab" + ("5" * 62)
    store.save(digest, b"z" * 77, "eager")
    _path_of(store, digest, "eager").unlink()

    store.touch(digest, "eager")

    assert store.total_bytes == 0


def test_a_file_that_cannot_be_read_leaves_the_index(tmp_path, monkeypatch) -> None:
    store = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=None)
    digest = "ab" + ("6" * 62)
    store.save(digest, b"q" * 50, "eager")

    def unreadable(self):
        raise PermissionError("denied")

    monkeypatch.setattr("pathlib.Path.read_bytes", unreadable)

    assert store.try_load(digest, "eager") is None
    assert store.total_bytes == 0


def test_a_blob_bigger_than_the_whole_cap_is_not_stored(tmp_path) -> None:
    store = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=100)
    small = "aa" + ("7" * 62)
    huge = "bb" + ("8" * 62)
    assert store.save(small, b"s" * 60, "eager") is True

    assert store.save(huge, b"h" * 101, "eager") is False

    assert store.try_load(huge, "eager") is None
    assert not _path_of(store, huge, "eager").exists()
    assert store.try_load(small, "eager") == b"s" * 60
    assert store.total_bytes == 60


def test_the_store_never_stays_over_the_cap_after_a_save(tmp_path) -> None:
    store = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=100)
    for index in range(8):
        digest = f"{index:02d}" + ("9" * 62)
        store.save(digest, b"b" * 40, "eager")
        assert store.total_bytes <= 100


def test_a_file_far_bigger_than_any_embedding_is_not_read_into_memory(tmp_path, monkeypatch) -> None:
    monkeypatch.setattr("segmentation_server.embedding_store.MAX_BLOB_BYTES", 10)
    store = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=None)
    digest = "ab" + ("a" * 62)
    store.save(digest, b"w" * 50, "eager")

    assert store.try_load(digest, "eager") is None
    assert store.total_bytes == 0


def test_touch_and_eviction_do_not_corrupt_the_index_when_they_race(tmp_path) -> None:
    import threading

    store = EmbeddingStore(str(tmp_path), stamp="abc", max_bytes=300)
    digests = [f"{index:02d}" + ("b" * 62) for index in range(12)]
    errors: list[BaseException] = []

    def writer() -> None:
        try:
            for digest in digests:
                store.save(digest, b"r" * 50, "eager")
        except BaseException as error:  # noqa: BLE001
            errors.append(error)

    def toucher() -> None:
        try:
            for _ in range(200):
                for digest in digests:
                    store.touch(digest, "eager")
                    store.try_load(digest, "eager")
        except BaseException as error:  # noqa: BLE001
            errors.append(error)

    threads = [threading.Thread(target=writer), threading.Thread(target=toucher), threading.Thread(target=toucher)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join(30)

    assert errors == []
    on_disk = sum(path.stat().st_size for path in tmp_path.rglob("*.pt"))
    assert store.total_bytes == on_disk <= 300
