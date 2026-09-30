"""Tile-mode logs name the mosaic cell while that cell is being worked on."""

from __future__ import annotations

import logging

from segmentation_server.tile_work import install_sam2_log_filter, install_tile_work_logging, tile_work


def test_tile_work_prefixes_volume_section_and_grid() -> None:
    install_tile_work_logging()
    seen: list[str] = []

    class Capture(logging.Handler):
        def emit(self, record: logging.LogRecord) -> None:
            seen.append(record.getMessage())

    handler = Capture()
    root = logging.getLogger()
    root.addHandler(handler)
    log = logging.getLogger("segmentation_server.test_tile_work")
    log.setLevel(logging.INFO)
    try:
        log.info("plain")
        with tile_work("RPC2", 986, 4, 9):
            log.info("segment %s", "1024x1024")
        log.info("after")
    finally:
        root.removeHandler(handler)

    assert seen == [
        "plain",
        "volume=RPC2 z=986 x=4 y=9 segment 1024x1024",
        "after",
    ]


def test_sam2_numpy_layout_note_is_dropped() -> None:
    install_tile_work_logging()
    install_sam2_log_filter()
    seen: list[str] = []

    class Capture(logging.Handler):
        def emit(self, record: logging.LogRecord) -> None:
            seen.append(record.getMessage())

    handler = Capture()
    root = logging.getLogger()
    previous_level = root.level
    root.setLevel(logging.INFO)
    root.addHandler(handler)
    try:
        logging.info("For numpy array image, we assume (HxWxC) format")
        logging.info("Computing image embeddings for the provided image...")
        with tile_work("RPC2", 986, 4, 9):
            logging.info("For numpy array image, we assume (HxWxC) format")
            logging.info("kept")
    finally:
        root.removeHandler(handler)
        root.setLevel(previous_level)

    assert seen == [
        "Computing image embeddings for the provided image...",
        "volume=RPC2 z=986 x=4 y=9 kept",
    ]
