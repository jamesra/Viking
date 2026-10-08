"""Debug dump is opt-in, round-trips masks and prompts, and keeps only the newest files."""

import json
from collections import namedtuple
from types import SimpleNamespace

import numpy as np

from segmentation_server import debug_dump


Tile = namedtuple("Tile", "row col")


def _tile(row: int, col: int) -> Tile:
    return Tile(row, col)


def _call(directory):
    mask = np.zeros((4, 4), dtype=np.bool_)
    mask[1:3, 1:3] = True
    return debug_dump.dump_growth(
        "abcd1234",
        fused_mask=mask,
        origin=(100, 200),
        cells={
            _tile(45, 41): SimpleNamespace(
                core=mask,
                score=0.5,
                raw=np.ones((4, 4), dtype=np.bool_),
                kept=mask,
                logits=np.arange(4, dtype=np.float32).reshape(2, 2) - 1.0,
            ),
            _tile(45, 42): SimpleNamespace(core=mask, score=0.4, raw=None, kept=None, logits=None),
        },
        uploaded=[_tile(45, 41)],
        requested=[_tile(46, 41)],
        foreground=[(10, 20)],
        background=[(30, 40)],
        score=0.7,
        directory=directory,
        margin_logit_min=1.5,
    )


def test_disabled_by_default_writes_nothing(tmp_path, monkeypatch) -> None:
    monkeypatch.delenv(debug_dump.ENV_ENABLE, raising=False)

    assert _call(tmp_path) is None
    assert list(tmp_path.iterdir()) == []


def test_enabled_round_trips_masks_and_prompts(tmp_path, monkeypatch) -> None:
    monkeypatch.setenv(debug_dump.ENV_ENABLE, "1")

    path = _call(tmp_path)

    assert path is not None and path.exists()
    with np.load(path) as data:
        assert data["fused"].sum() == 4
        assert data["cell_r45_c41"].shape == (4, 4)
        assert data["raw_r45_c41"].sum() == 16
        assert data["kept_r45_c41"].sum() == 4
        assert "raw_r45_c42" not in data.files
        assert data["logits_r45_c41"].dtype == np.float16
        assert data["logits_r45_c41"].tolist() == [[-1.0, 0.0], [1.0, 2.0]]
        assert "logits_r45_c42" not in data.files
        meta = json.loads(str(data["meta"]))
    assert meta["margin_logit_min"] == 1.5
    assert meta["origin"] == [100, 200]
    assert meta["requested"] == [[46, 41]]
    assert meta["foreground"] == [[10, 20]]
    assert meta["background"] == [[30, 40]]
    assert meta["cell_scores"] == {"cell_r45_c41": 0.5, "cell_r45_c42": 0.4}


def test_prune_keeps_only_the_newest(tmp_path) -> None:
    import os

    for i in range(5):
        f = tmp_path / f"dump-{i}.npz"
        f.write_bytes(b"x")
        os.utime(f, (1000 + i, 1000 + i))

    debug_dump._prune(tmp_path, keep=2)

    assert sorted(p.name for p in tmp_path.iterdir()) == ["dump-3.npz", "dump-4.npz"]


def test_failure_is_swallowed(monkeypatch, tmp_path) -> None:
    monkeypatch.setenv(debug_dump.ENV_ENABLE, "1")
    blocker = tmp_path / "file"
    blocker.write_text("x")

    assert _call(blocker / "sub") is None
