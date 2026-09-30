"""Process-wide logging tweaks for the segmentation server.

Tile-mode work tags each line with the mosaic cell. Grid x is the column and
grid y is the row. Mosaic Y increases upward, so y is the same index Viking
uses for the cell's row.

SAM2's numpy layout note is dropped. The server always passes HWC arrays, and
that line is emitted on every set_image().
"""

from __future__ import annotations

import logging
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
