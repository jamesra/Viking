"""Process-wide logging tweaks for the segmentation server.

Tile-mode work tags each line with the mosaic cell. Grid x is the column and
grid y is the row. Mosaic Y increases upward, so y is the same index Viking
uses for the cell's row.

SAM2's numpy layout note is dropped. The server always passes HWC arrays, and
that line is emitted on every set_image().
"""

from __future__ import annotations

import logging
import queue
from logging.handlers import QueueHandler, QueueListener
from contextlib import contextmanager
from contextvars import ContextVar, Token
from typing import Iterator, Optional, Tuple

_Tile = Tuple[str, int, int, int]
_current: ContextVar[Optional[_Tile]] = ContextVar("segmentation_tile_work", default=None)

# sam2.sam2_image_predictor.set_image logs this via logging.info on every call.
_SAM2_NUMPY_LAYOUT_NOTE = "For numpy array image, we assume (HxWxC) format"


class _DropSam2NumpyLayoutNote(logging.Filter):
    """Drop SAM2's HWC layout note, including when a tile prefix was prepended."""

    def filter(self, record: logging.LogRecord) -> bool:
        return _SAM2_NUMPY_LAYOUT_NOTE not in record.getMessage()


def install_sam2_log_filter() -> None:
    """Keep SAM2's per-set_image layout note out of the server log.

    The note is logged on the root logger. A filter there runs after the tile
    prefix is applied, so a substring match covers both forms.
    """
    root = logging.getLogger()
    if any(isinstance(existing, _DropSam2NumpyLayoutNote) for existing in root.filters):
        return
    root.addFilter(_DropSam2NumpyLayoutNote())


def install_queued_console_logging(level: int = logging.INFO, fmt: str = "%(asctime)s %(levelname)s %(name)s: %(message)s") -> QueueListener:
    """Send root-logger output to stderr through a queue and a writer thread.

    The console handler blocks whenever the terminal does (a Windows QuickEdit selection, a slow
    redirected pipe). Called directly it would freeze whichever thread logged, including the asyncio
    event loop that serves every stream. Here callers only enqueue; the returned listener owns the
    blocking write. The record is formatted at enqueue time, so the tile prefix added by
    :func:`install_tile_work_logging` is kept. The caller stops the listener at shutdown to flush it.
    """
    log_queue: queue.SimpleQueue[logging.LogRecord] = queue.SimpleQueue()
    console = logging.StreamHandler()
    console.setFormatter(logging.Formatter(fmt))
    listener = QueueListener(log_queue, console)
    root = logging.getLogger()
    root.setLevel(level)
    root.addHandler(QueueHandler(log_queue))
    listener.start()
    return listener


@contextmanager
def tile_work(volume: str, section: int, col: int, row: int) -> Iterator[None]:
    """Mark logs on this thread as working on one grid cell. Z is the section number."""
    token: Token[Optional[_Tile]] = _current.set((volume, int(section), int(col), int(row)))
    try:
        yield
    finally:
        _current.reset(token)


_installed = False


def install_tile_work_logging() -> None:
    """Prefix every log record created while tile_work is active on that thread.

    The record is built in the logger that emitted it, before any handler runs,
    so SAM2's own loggers pick up the cell too.
    """
    global _installed
    if _installed:
        return
    _installed = True
    previous = logging.getLogRecordFactory()

    def factory(*args: object, **kwargs: object) -> logging.LogRecord:
        record = previous(*args, **kwargs)
        current = _current.get()
        if current is not None:
            volume, section, col, row = current
            record.msg = f"volume={volume} z={section} x={col} y={row} {record.msg}"
        return record

    logging.setLogRecordFactory(factory)
