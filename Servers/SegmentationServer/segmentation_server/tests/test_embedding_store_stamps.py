"""Disk embedding store: folders left by other checkpoint stamps are removed at startup."""

from __future__ import annotations

from pathlib import Path

from segmentation_server.embedding_store import EmbeddingStore

CURRENT = "59f3ad4c9b1f5452"
OLD = "3e8ee331c44a22f7"
OLDER = "cc5695e81dbee78f"


def _write_blob(root: Path, stamp: str, mode: str, size: int) -> Path:
    path = root / stamp / mode / "ab" / ("ab" + "0" * 62 + ".pt")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"x" * size)
    return path


def test_old_stamps_are_removed_with_their_files(tmp_path) -> None:
    old_blob = _write_blob(tmp_path, OLD, "compiled", 100)
    older_blob = _write_blob(tmp_path, OLDER, "eager", 200)

    EmbeddingStore(str(tmp_path), stamp=CURRENT, max_bytes=None)

    assert not old_blob.exists()
    assert not older_blob.exists()
    assert not (tmp_path / OLD).exists()
    assert not (tmp_path / OLDER).exists()


def test_current_stamp_keeps_its_files_and_they_are_the_only_ones_counted(tmp_path) -> None:
    kept = _write_blob(tmp_path, CURRENT, "compiled", 50)
    _write_blob(tmp_path, OLD, "compiled", 1000)

    store = EmbeddingStore(str(tmp_path), stamp=CURRENT, max_bytes=None)

    assert kept.read_bytes() == b"x" * 50
    assert store.total_bytes == 50


def test_folders_that_do_not_look_like_a_stamp_are_left_alone(tmp_path) -> None:
    notes = tmp_path / "notes"
    notes.mkdir()
    (notes / "keep.txt").write_text("keep")
    short = tmp_path / "abc"
    short.mkdir()
    (short / "keep.txt").write_text("keep")
    upper = tmp_path / "3E8EE331C44A22F7"
    upper.mkdir()
    (upper / "keep.txt").write_text("keep")
    loose = tmp_path / OLD.replace("3", "4")
    loose.write_text("a file named like a stamp is not a folder")

    EmbeddingStore(str(tmp_path), stamp=CURRENT, max_bytes=None)

    assert (notes / "keep.txt").read_text() == "keep"
    assert (short / "keep.txt").read_text() == "keep"
    assert (upper / "keep.txt").read_text() == "keep"
    assert loose.is_file()


def test_a_second_start_with_the_same_stamp_removes_nothing(tmp_path) -> None:
    digest = "ab" + "1" * 62
    first = EmbeddingStore(str(tmp_path), stamp=CURRENT, max_bytes=None)
    first.save(digest, b"persisted", "compiled")

    second = EmbeddingStore(str(tmp_path), stamp=CURRENT, max_bytes=None)

    assert second.try_load(digest, "compiled") == b"persisted"
