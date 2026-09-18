"""Unit tests for GetServerStatus load accounting."""

from __future__ import annotations

from segmentation_server.server import RequestLoadTracker


def test_load_tracker_in_flight_and_ewma() -> None:
    tracker = RequestLoadTracker()
    assert tracker.snapshot() == (0, 0.0)

    tracker.begin()
    tracker.begin()
    in_flight, _ = tracker.snapshot()
    assert in_flight == 2

    tracker.end(0.100)
    in_flight, latency = tracker.snapshot()
    assert in_flight == 1
    assert latency == 100.0

    tracker.end(0.200)
    in_flight, latency = tracker.snapshot()
    assert in_flight == 0
    assert latency == 0.3 * 200.0 + 0.7 * 100.0
