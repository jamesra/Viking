"""Growth-walk and seam housekeeping from the round-2 review, plus health check and disk blob checks."""

from __future__ import annotations

from collections import Counter

import numpy as np
import pytest

from cell_world import World, circle_clicks, ellipse
from segmentation_server.cell_grid import CELL_SIZE, CORE_SIZE, Cell, cell_of_point, mosaic_to_window
from segmentation_server.seams import Side, band_depths, edge_coordinates, edge_runs
from segmentation_server.tile_growth import GrowthWalk, clip_box_to_window, window_box


class _CountingWalk(GrowthWalk):
    """Counts how often each cell is looked at for work."""

    def __init__(self, *args, **kwargs) -> None:
        super().__init__(*args, **kwargs)
        self.looked: Counter = Counter()

    def _attempts(self, cell):
        self.looked[cell] += 1
        yield from super()._attempts(cell)


def test_a_cell_the_cell_budget_blocks_is_looked_at_once() -> None:
    world = World(ellipse(2048, 2048, 900, 900, 0.0))
    walk = _CountingWalk(circle_clicks(2048, 2048, 100), [], world.predict, max_cells=2)

    walk.advance()

    assert walk._blocked, "a 900 px disc must reach more than two cells"
    assert all(walk.looked[cell] == 1 for cell in walk._blocked)
    assert not (walk._blocked & set(walk._states))


def test_a_blocked_cell_is_not_queued_again() -> None:
    world = World(ellipse(2048, 2048, 900, 900, 0.0))
    walk = GrowthWalk(circle_clicks(2048, 2048, 100), [], world.predict, max_cells=2)
    walk.advance()
    blocked = next(iter(walk._blocked))

    walk._enqueue(blocked)

    assert blocked not in walk._queued


def test_an_edge_whose_range_was_crossed_does_not_queue_the_neighbor_again() -> None:
    world = World(ellipse(2048, 2048, 700, 700, 0.0))
    walk = GrowthWalk(circle_clicks(2048, 2048, 100), [], world.predict)
    walk.advance()
    crossed = next(iter(walk._graph.edges.items()))
    (row_a, col_a, row_b, col_b), ranges = crossed
    cell, neighbor = Cell(row_a, col_a), Cell(row_b, col_b)
    side = next(s for s in Side if (cell.row + s.step[0], cell.col + s.step[1]) == (neighbor.row, neighbor.col))
    core = walk._canvas.core(cell)

    covered = all(
        any(start <= c0 and c1 <= end for start, end in ranges)
        for c0, c1 in (edge_coordinates(cell, side, run) for run in edge_runs(core, side))
    )
    if not covered:
        pytest.skip("this walk left part of the edge uncrossed; nothing to assert")

    assert walk._has_fresh_range(cell, core, side, neighbor) is False


def test_window_box_picks_the_same_box_whatever_order_equal_areas_arrive_in() -> None:
    cell = Cell(2, 2)
    left_box = (cell.col * 512 + 300, cell.row * 512 + 300, cell.col * 512 + 399, cell.row * 512 + 399)
    right_box = (cell.col * 512 + 400, cell.row * 512 + 300, cell.col * 512 + 499, cell.row * 512 + 399)

    assert window_box(cell, [left_box, right_box]) == window_box(cell, [right_box, left_box])
    assert window_box(cell, [left_box, right_box]) is not None


def test_clip_box_to_window_agrees_with_what_window_box_returns() -> None:
    cell = Cell(1, 1)
    inside = (cell.col * 512 + 256, cell.row * 512 + 256, cell.col * 512 + 600, cell.row * 512 + 600)

    assert clip_box_to_window(cell, inside) == window_box(cell, [inside])


def test_a_box_outside_the_window_clips_to_nothing() -> None:
    far = (50_000, 50_000, 50_100, 50_100)

    assert clip_box_to_window(Cell(0, 0), far) is None
    assert window_box(Cell(0, 0), [far]) is None


def test_a_box_that_hangs_over_the_window_edge_is_cut_to_the_window() -> None:
    cell = Cell(0, 0)
    x0, y0 = 0, 0
    left, top = mosaic_to_window(cell, x0, y0 + 100)
    box = (x0 - 400, y0 - 400, x0 + 100, y0 + 100)

    clipped = clip_box_to_window(cell, box)

    assert clipped is not None
    assert min(clipped) >= 0 and max(clipped) <= CELL_SIZE - 1
    assert left >= 0 and top >= 0


@pytest.mark.parametrize("index", [0, CORE_SIZE - 1])
def test_band_depths_over_the_first_and_last_row_of_an_edge(index: int) -> None:
    core = np.ones((CORE_SIZE, CORE_SIZE), dtype=np.bool_)

    depths = band_depths(core, Side.WEST, 40, rows=[(index, index)])

    assert depths[index] == 40
    assert int(depths.sum()) == 40, "only the requested position is measured"


def test_band_depths_stops_at_the_first_unset_pixel() -> None:
    core = np.ones((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
    core[CORE_SIZE - 1, 7] = False

    depths = band_depths(core, Side.WEST, 40, rows=[(CORE_SIZE - 1, CORE_SIZE - 1)])

    assert depths[CORE_SIZE - 1] == 7


# --- health check --------------------------------------------------------------------------------


def test_the_health_check_reads_the_port_the_server_recorded(monkeypatch, tmp_path) -> None:
    from segmentation_server import healthcheck

    monkeypatch.setattr(healthcheck, "port_file", lambda: tmp_path / "port")
    monkeypatch.setenv("SEGMENTATION_TLS_PORT", "8443")

    assert healthcheck.tls_port() == 8443
    healthcheck.record_listening_port(9443)
    assert healthcheck.tls_port() == 9443
    healthcheck.forget_listening_port()
    assert healthcheck.tls_port() == 8443


def test_a_garbled_port_record_falls_back_to_the_environment(monkeypatch, tmp_path) -> None:
    from segmentation_server import healthcheck

    monkeypatch.setattr(healthcheck, "port_file", lambda: tmp_path / "port")
    (tmp_path / "port").write_text("not a port")
    monkeypatch.delenv("SEGMENTATION_TLS_PORT", raising=False)

    assert healthcheck.tls_port() == healthcheck.DEFAULT_PORT


def _trust_roots_offered(monkeypatch, cert_path: str) -> list:
    """Run the health check against a closed port and return the roots it tried to trust, in order."""
    from segmentation_server import healthcheck

    offered: list = []
    real = healthcheck.grpc.ssl_channel_credentials

    def record(root_certificates=None, **kwargs):
        offered.append(root_certificates)
        return real(root_certificates=root_certificates, **kwargs)

    monkeypatch.setattr(healthcheck.grpc, "ssl_channel_credentials", record)
    reason = healthcheck.check(1, cert_path, timeout=0.2)
    assert reason, "nothing listens on port 1"
    return offered


def test_a_lets_encrypt_chain_is_never_offered_as_a_trust_root(monkeypatch, tmp_path) -> None:
    pytest.importorskip("cryptography")
    from segmentation_server.dev_cert import generate_self_signed

    live = tmp_path / "live" / "example.org"
    cert, _key = generate_self_signed(live / "fullchain.pem", live / "privkey.pem")

    assert _trust_roots_offered(monkeypatch, str(cert)) == [None]


def test_a_self_signed_certificate_is_the_fallback_trust_root(monkeypatch, tmp_path) -> None:
    pytest.importorskip("cryptography")
    from segmentation_server.dev_cert import generate_self_signed

    cert, _key = generate_self_signed(tmp_path / "cert.pem", tmp_path / "key.pem")

    assert _trust_roots_offered(monkeypatch, str(cert)) == [None, cert.read_bytes()]


# --- disk embedding blob validation ------------------------------------------------------------


def _blob(torch, **overrides):
    payload = {
        "format_version": torch.tensor([1], dtype=torch.int64),
        "image_embed": torch.zeros(1, 4, 8, 8),
        "orig_hw": torch.tensor([64, 64], dtype=torch.int64),
        "n_high": torch.tensor([2], dtype=torch.int64),
        "high_res_0": torch.zeros(1, 2, 16, 16),
        "high_res_1": torch.zeros(1, 2, 8, 8),
    }
    payload.update(overrides)
    return payload


class _Predictor:
    device = "cpu"


@pytest.mark.parametrize(
    "change",
    [
        {"n_high": "zero"},
        {"n_high": "too many"},
        {"orig_hw": "huge"},
        {"orig_hw": "negative"},
        {"image_embed": "flat"},
        {"high_res_0": "no batch"},
        {"high_res_1": "empty"},
    ],
)
def test_a_corrupt_disk_blob_is_refused_before_it_is_installed(change: dict) -> None:
    torch = pytest.importorskip("torch")
    pytest.importorskip("sam2")
    from segmentation_server.segmentation_service import SegmentationModel

    changes = {
        ("n_high", "zero"): {"n_high": torch.tensor([0], dtype=torch.int64)},
        ("n_high", "too many"): {"n_high": torch.tensor([99], dtype=torch.int64)},
        ("orig_hw", "huge"): {"orig_hw": torch.tensor([10**7, 64], dtype=torch.int64)},
        ("orig_hw", "negative"): {"orig_hw": torch.tensor([-1, 64], dtype=torch.int64)},
        ("image_embed", "flat"): {"image_embed": torch.zeros(4, 8, 8)},
        ("high_res_0", "no batch"): {"high_res_0": torch.zeros(2, 2, 16, 16)},
        ("high_res_1", "empty"): {"high_res_1": torch.zeros(1, 0, 8, 8)},
    }
    ((name, kind),) = change.items()
    predictor = _Predictor()

    with pytest.raises(ValueError):
        SegmentationModel._install_disk_features(predictor, _blob(torch, **changes[(name, kind)]))

    assert not hasattr(predictor, "_features"), "nothing is installed from a refused blob"


def test_a_well_formed_disk_blob_is_installed() -> None:
    torch = pytest.importorskip("torch")
    pytest.importorskip("sam2")
    from segmentation_server.segmentation_service import SegmentationModel

    predictor = _Predictor()

    SegmentationModel._install_disk_features(predictor, _blob(torch))

    assert predictor._orig_hw == [(64, 64)]
    assert len(predictor._features["high_res_feats"]) == 2
