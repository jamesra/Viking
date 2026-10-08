"""Shutdown waits for in-flight SAM2 work before cancelling what is left."""

from __future__ import annotations

import threading
import time
from concurrent import futures

from segmentation_server.server import drain_executor


def test_running_and_queued_work_finishes_before_the_executor_is_released() -> None:
    executor = futures.ThreadPoolExecutor(max_workers=1)
    done: list[int] = []

    def job(n: int) -> None:
        time.sleep(0.05)
        done.append(n)

    for n in range(3):
        executor.submit(job, n)

    assert drain_executor(executor, timeout_seconds=10) is True
    assert done == [0, 1, 2]


def test_work_still_running_after_the_timeout_is_not_waited_on_and_queued_work_is_cancelled() -> None:
    executor = futures.ThreadPoolExecutor(max_workers=1)
    release = threading.Event()
    ran: list[str] = []

    def blocker() -> None:
        release.wait(10)
        ran.append("blocker")

    executor.submit(blocker)
    queued = executor.submit(lambda: ran.append("queued"))

    started = time.monotonic()
    result = drain_executor(executor, timeout_seconds=0.2)
    elapsed = time.monotonic() - started
    release.set()

    assert result is False
    assert elapsed < 5
    assert queued.cancelled()
    time.sleep(0.2)
    assert ran == ["blocker"]


def test_an_idle_executor_drains_at_once() -> None:
    executor = futures.ThreadPoolExecutor(max_workers=2)
    started = time.monotonic()
    assert drain_executor(executor, timeout_seconds=5) is True
    assert time.monotonic() - started < 2
