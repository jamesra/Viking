"""One tile of a grid: a ``viking.geometry.v1.TileGrid`` and a ``GridCell``, as the server keys and reads it."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from segmentation_grpc import CoordinateSpace
from segmentation_server.image_cache import TileCacheKey


def space_name(space: int) -> str:
    """The grid's coordinate space as the cache key names it: ``"mosaic"`` or ``"volume"``.

    UNSPECIFIED never gets here: every RPC refuses it.
    """
    return "mosaic" if space == CoordinateSpace.COORDINATE_SPACE_MOSAIC else "volume"


def valid_space(space: int) -> bool:
    return space in (CoordinateSpace.COORDINATE_SPACE_MOSAIC, CoordinateSpace.COORDINATE_SPACE_VOLUME)


@dataclass(frozen=True)
class TileRef:
    """A tile: what every cell of its grid shares, and its row and column (row along Y, column along X)."""

    volume: str
    section: int
    channel: str
    transform: str
    space: int
    downsample: int
    row: int
    col: int

    @classmethod
    def of(cls, grid: Any, row: int, col: int) -> "TileRef":
        """The tile at ``(row, col)`` of a ``TileGrid``."""
        return cls(
            volume=grid.volume,
            section=int(grid.section),
            channel=grid.channel,
            transform=grid.transform,
            space=int(grid.space),
            downsample=int(grid.downsample),
            row=int(row),
            col=int(col),
        )

    @property
    def key(self) -> TileCacheKey:
        """The image cache and tile store key."""
        return (
            self.volume, self.section, self.channel, self.transform, space_name(self.space), self.downsample,
            self.row, self.col,
        )
