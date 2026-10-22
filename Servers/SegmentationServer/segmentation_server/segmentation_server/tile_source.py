"""Grid cells the server reads itself, for annotation contexts it is local to, instead of asking the client.

Only what the server can draw exactly as the client would is read:

* a plain greyscale channel (a ``TileCoord.channel`` with no ``[...]`` suffix of blend, colour or view options),
* in the section's mosaic space (``TileCoord.transform`` is ``"|<tile transform>"``: no volume transform),
* whose channel names a VikingXML tileset of the section at the cell's downsample.

A cell is stitched from the tileset tiles it overlaps, flipped so its first row is its high mosaic Y (the capture's
orientation), and encoded as the client encodes a capture (:mod:`capture_encoder`). Tiles outside the grid, or
missing on storage, are black. Anything else returns None and the stream asks the client, as before.
"""

from __future__ import annotations

import asyncio
import logging
import time
import urllib.error
import urllib.request
from concurrent import futures
from typing import Callable, Dict, List, Optional, Protocol, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray

from segmentation_grpc import TileCoord
from segmentation_server.capture_encoder import encode_capture
from segmentation_server.identity_client import IdentityAssignments, IdentityOptions
from segmentation_server.viking_volume import VikingVolume, parse_viking_xml

logger = logging.getLogger(__name__)

CELL = 1024
DEFAULT_VOLUME_REFRESH_SECONDS = 3600.0
DEFAULT_FETCH_WORKERS = 16
_TIMEOUT_SECONDS = 30.0

# url -> body, or None when the server answers 404 (a hole in the tileset). Raises on any other failure.
HttpGet = Callable[[str], Optional[bytes]]


class TileSource(Protocol):
    async def fetch(self, annotation_context_id: int, coord: TileCoord) -> Optional[bytes]:
        """The cell's encoded image, or None when this source cannot draw it."""
        ...


def _http_get(url: str) -> Optional[bytes]:
    try:
        with urllib.request.urlopen(url, timeout=_TIMEOUT_SECONDS) as response:
            return response.read()
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return None
        raise


def is_plain_mosaic_cell(coord: TileCoord) -> bool:
    """True for a plain greyscale channel in mosaic space: the only cells a server draws as the client would."""
    volume_transform = coord.transform.partition("|")[0]
    return bool(coord.channel) and "[" not in coord.channel and volume_transform == "" and coord.downsample > 0


def _luma(data: bytes) -> Optional[NDArray[np.uint8]]:
    """A tile's 8-bit grey values: the first byte of each pixel, as the client's texture reader keeps."""
    image = cv2.imdecode(np.frombuffer(data, dtype=np.uint8), cv2.IMREAD_UNCHANGED)
    if image is None or image.dtype != np.uint8:
        return None
    return image if image.ndim == 2 else image[:, :, 0]


class VikingTilesetSource:
    """Reads tileset tiles over HTTP from the VikingXML of each local annotation context."""

    def __init__(
        self,
        assignments: IdentityAssignments,
        http_get: HttpGet = _http_get,
        volume_refresh_seconds: float = DEFAULT_VOLUME_REFRESH_SECONDS,
        fetch_workers: int = DEFAULT_FETCH_WORKERS,
        time_fn: Callable[[], float] = time.monotonic,
    ) -> None:
        self._assignments = assignments
        self._http_get = http_get
        self._volume_refresh_seconds = volume_refresh_seconds
        self._time_fn = time_fn
        self._volumes: Dict[str, Tuple[VikingVolume, float]] = {}
        self._volume_locks: Dict[str, asyncio.Lock] = {}
        self._pool = futures.ThreadPoolExecutor(max_workers=max(1, fetch_workers), thread_name_prefix="tile-source")

    def close(self) -> None:
        self._pool.shutdown(wait=False, cancel_futures=True)

    async def _get(self, url: str) -> Optional[bytes]:
        return await asyncio.get_running_loop().run_in_executor(self._pool, self._http_get, url)

    async def _volume(self, url: str) -> Optional[VikingVolume]:
        """The VikingXML at ``url``, read again after ``volume_refresh_seconds``. None when it cannot be read."""
        cached = self._volumes.get(url)
        if cached is not None and self._time_fn() - cached[1] < self._volume_refresh_seconds:
            return cached[0]
        lock = self._volume_locks.setdefault(url, asyncio.Lock())
        async with lock:
            cached = self._volumes.get(url)
            if cached is not None and self._time_fn() - cached[1] < self._volume_refresh_seconds:
                return cached[0]
            try:
                data = await self._get(url)
                if data is None:
                    raise ValueError("not found")
                volume = await asyncio.get_running_loop().run_in_executor(self._pool, parse_viking_xml, data, url)
            except Exception as e:
                logger.warning("VikingXML %s could not be read (%s)", url, e)
                return cached[0] if cached is not None else None
            self._volumes[url] = (volume, self._time_fn())
            logger.info("VikingXML %s: volume %s, %d sections", url, volume.name, len(volume.sections))
            return volume

    async def fetch(self, annotation_context_id: int, coord: TileCoord) -> Optional[bytes]:
        if annotation_context_id <= 0 or not is_plain_mosaic_cell(coord):
            return None
        local = await self._assignments.get(annotation_context_id)
        if local is None:
            return None
        volume = await self._volume(local.endpoint)
        if volume is None or volume.name != coord.volume:
            return None
        section = volume.sections.get(coord.section)
        tileset = section.tilesets.get(coord.channel) if section is not None else None
        level = tileset.levels.get(coord.downsample) if tileset is not None else None
        if level is None:
            return None

        # The cell in level pixels; row increases with mosaic Y, as tile rows do.
        x0, y0 = coord.col * CELL, coord.row * CELL
        tiles: List[Tuple[int, int]] = [
            (ix, iy)
            for iy in range(y0 // tileset.tile_y, (y0 + CELL - 1) // tileset.tile_y + 1)
            for ix in range(x0 // tileset.tile_x, (x0 + CELL - 1) // tileset.tile_x + 1)
            if 0 <= ix < level.grid_x and 0 <= iy < level.grid_y
        ]
        try:
            bodies = await asyncio.gather(
                *(self._get(volume.tile_url(section, tileset, coord.downsample, ix, iy)) for ix, iy in tiles)
            )
        except Exception as e:
            logger.warning("Tileset read for row=%s col=%s of %s failed (%s)", coord.row, coord.col, volume.name, e)
            return None

        canvas = np.zeros((CELL, CELL), dtype=np.uint8)
        for (ix, iy), body in zip(tiles, bodies):
            if body is None:
                continue
            luma = _luma(body)
            if luma is None:
                logger.warning("Tileset tile X%s_Y%s of %s is not an 8-bit image; the client draws this cell", ix, iy, volume.name)
                return None
            top, left = iy * tileset.tile_y - y0, ix * tileset.tile_x - x0
            rows = slice(max(0, top), min(CELL, top + luma.shape[0]))
            cols = slice(max(0, left), min(CELL, left + luma.shape[1]))
            canvas[rows, cols] = luma[rows.start - top : rows.stop - top, cols.start - left : cols.stop - left]

        loop = asyncio.get_running_loop()
        return await loop.run_in_executor(self._pool, encode_capture, np.ascontiguousarray(np.flipud(canvas)))


def open_tile_source() -> Optional[VikingTilesetSource]:
    """The tile source the environment configures, or None (every cell is asked of the client).

    Needs ``IdentityServer__Endpoint``, ``IdentityServer__ClientSecret`` and ``SEGMENTATION_SERVICE_NAME``;
    ``IdentityServer__ApiEndpoint`` and ``IdentityServer__ClientId`` are optional.
    """
    options = IdentityOptions.from_env()
    if options is None:
        logger.info("Tile source off: Identity is not configured, so clients upload every cell")
        return None
    logger.info("Tile source on: reading tilesets of the contexts local to %s", options.service_name)
    return VikingTilesetSource(IdentityAssignments(options))
