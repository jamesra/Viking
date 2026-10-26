"""Segment an ordinary picture through the tile-grid protocol: the demo page and the example client use it.

A picture is laid on a synthetic ``TileGrid`` of its own (volume ``picture-<digest>``, MOSAIC space, downsample 1): the
picture's top-left pixel is at the top-left of the grid's top row of tiles, so image row ``y`` (down) is grid pixel row
``rows * 1024 - 1 - y`` (up), and image column ``x`` is grid column ``x``. Tiles past the picture's edge are padded
with black; tiles wholly outside it are answered as unavailable. The conversation is the one a Viking client has:
``start``, then an upload and an answer for every ``TilesNeeded``, then the result, whose logits are thresholded and
placed back on the picture.

The transport is passed in, so the same code drives an in-process servicer (the demo page) and a gRPC stub (the
example client).
"""

from __future__ import annotations

import asyncio
import hashlib
from dataclasses import dataclass
from typing import Any, AsyncIterator, Awaitable, Callable, List, Optional, Sequence, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray

from segmentation_grpc import (
    CoordinateSpace,
    Feature,
    GridCell,
    SegmentationResponse,
    SegmentResult,
    SegmentTilesRequest,
    SegmentTilesStreamRequest,
    SegmentTilesStreamResponse,
    TileGrid,
    TilesAnswer,
    UploadTileRequest,
    Vector2,
)

TILE = 1024
DEFAULT_MASK_THRESHOLD = 1.0

Upload = Callable[[UploadTileRequest], Awaitable[Any]]
Converse = Callable[[AsyncIterator[SegmentTilesStreamRequest]], AsyncIterator[SegmentTilesStreamResponse]]


@dataclass(frozen=True)
class PictureClick:
    """A click on the picture: image pixel ``(x, y)``, Y down, and 1 for object or 0 for background."""

    x: int
    y: int
    label: int


@dataclass(frozen=True)
class PictureSegment:
    """One island's mask on the picture: the set pixels of ``mask`` (Y down) start at image pixel ``(x, y)``."""

    island: int
    score: float
    x: int
    y: int
    mask: NDArray[np.bool_]


class PictureGrid:
    """A decoded picture (``height x width``, Y down) on its own tile grid."""

    def __init__(self, pixels: NDArray[np.uint8], name: Optional[str] = None) -> None:
        self.pixels = np.ascontiguousarray(pixels)
        self.height, self.width = int(self.pixels.shape[0]), int(self.pixels.shape[1])
        self.rows = max(1, -(-self.height // TILE))
        self.cols = max(1, -(-self.width // TILE))
        if name is None:
            name = "picture-" + hashlib.sha256(self.pixels.tobytes()).hexdigest()[:16]
        self.grid = TileGrid(
            volume=name,
            section=0,
            channel="picture",
            transform="",
            downsample=1,
            space=CoordinateSpace.COORDINATE_SPACE_MOSAIC,
        )

    def grid_point(self, x: float, y: float) -> Tuple[float, float]:
        """The grid coordinate (Y up) of image coordinate ``(x, y)`` (Y down, continuous)."""
        return float(x), float(self.rows * TILE - y)

    def click_point(self, click: PictureClick) -> Vector2:
        """The grid point at the centre of the clicked image pixel."""
        gx, gy = self.grid_point(click.x + 0.5, click.y + 0.5)
        return Vector2(x=gx, y=gy)

    def cell_of(self, click: PictureClick) -> Tuple[int, int]:
        """``(row, col)`` of the tile holding the clicked pixel."""
        gy = self.rows * TILE - 1 - click.y
        return gy // TILE, click.x // TILE

    def tile_png(self, row: int, col: int) -> Optional[bytes]:
        """The tile at ``(row, col)`` as a 1024 x 1024 PNG, Y down, black past the picture; None when it is outside."""
        if not (0 <= row < self.rows and 0 <= col < self.cols):
            return None
        # Tile row r covers grid rows [r * 1024, (r + 1) * 1024), which are image rows counted down from the top.
        top = (self.rows - 1 - row) * TILE
        left = col * TILE
        tile = np.zeros((TILE, TILE) + self.pixels.shape[2:], dtype=self.pixels.dtype)
        piece = self.pixels[top:top + TILE, left:left + TILE]
        tile[:piece.shape[0], :piece.shape[1]] = piece
        if tile.ndim == 3:
            tile = cv2.cvtColor(tile, cv2.COLOR_RGB2BGR)
        ok, encoded = cv2.imencode(".png", tile)
        if not ok:
            raise ValueError("could not encode a picture tile as PNG")
        return encoded.tobytes()

    def segment_mask(self, segment: SegmentResult, threshold: float) -> Optional[PictureSegment]:
        """A result's logits upsampled, thresholded and placed on the picture, or None when nothing of it is on it."""
        width, height = segment.logits_size.width, segment.logits_size.height
        if width <= 0 or height <= 0:
            return None
        scale = int(round(segment.logits_scale)) or 1
        logits = np.frombuffer(segment.logits, dtype="<f2").astype(np.float32).reshape(height, width)
        up = cv2.resize(logits, (width * scale, height * scale), interpolation=cv2.INTER_LINEAR) > threshold
        # Up-sampled pixel (0, 0) is the grid pixel logits_origin (its top-left), so its image row is counted down
        # from the grid's top.
        x0 = int(segment.logits_origin.x)
        y0 = self.rows * TILE - 1 - int(segment.logits_origin.y)
        left, top = max(0, x0), max(0, y0)
        right, bottom = min(self.width, x0 + up.shape[1]), min(self.height, y0 + up.shape[0])
        if right <= left or bottom <= top:
            return None
        mask = up[top - y0:bottom - y0, left - x0:right - x0]
        if not mask.any():
            return None
        ys, xs = np.nonzero(mask)
        crop = mask[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        return PictureSegment(
            island=int(segment.island), score=float(segment.score),
            x=left + int(xs.min()), y=top + int(ys.min()), mask=crop,
        )


async def segment_picture(
    picture: PictureGrid,
    clicks: Sequence[PictureClick],
    upload: Upload,
    converse: Converse,
    mask_threshold: float = DEFAULT_MASK_THRESHOLD,
    model_id: Optional[str] = None,
) -> Tuple[SegmentationResponse, List[PictureSegment]]:
    """Run one SegmentTilesStream for ``clicks`` on ``picture``; the server's response and its masks on the picture.

    Raises whatever ``upload`` or ``converse`` raise (a gRPC error, or the demo's abort).
    """
    foreground = [click for click in clicks if click.label == 1]
    background = [click for click in clicks if click.label != 1]
    cells = sorted({picture.cell_of(click) for click in foreground})
    start = SegmentTilesRequest(
        grid=picture.grid,
        tiles=[GridCell(row=row, col=col) for row, col in cells],
        mask_threshold=mask_threshold,
        foreground_feature=Feature(points=[picture.click_point(click) for click in foreground]),
        background_feature=Feature(points=[picture.click_point(click) for click in background]),
    )
    if model_id:
        start.model_id = model_id

    inbox: "asyncio.Queue[Optional[SegmentTilesStreamRequest]]" = asyncio.Queue()
    await inbox.put(SegmentTilesStreamRequest(start=start))

    async def requests() -> AsyncIterator[SegmentTilesStreamRequest]:
        while True:
            item = await inbox.get()
            if item is None:
                return
            yield item

    response: Optional[SegmentationResponse] = None
    try:
        async for update in converse(requests()):
            if update.WhichOneof("update") == "result":
                response = update.result
                break
            unavailable: List[GridCell] = []
            for cell in update.needed.tiles:
                png = picture.tile_png(cell.row, cell.col)
                if png is None:
                    unavailable.append(GridCell(row=cell.row, col=cell.col))
                    continue
                await upload(UploadTileRequest(grid=picture.grid, cell=cell, image_data=png))
            # A plain picture has no neighbouring annotations: background_cells are answered with no shapes.
            await inbox.put(SegmentTilesStreamRequest(answer=TilesAnswer(unavailable=unavailable)))
    finally:
        await inbox.put(None)
    if response is None:
        raise RuntimeError("the segmentation stream ended without a result")
    segments = [
        found for found in (picture.segment_mask(segment, mask_threshold) for segment in response.segments)
        if found is not None
    ]
    return response, segments
