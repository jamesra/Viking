"""The part of a VikingXML volume description the server needs to read a section's tileset itself.

A tileset is a section's assembled mosaic cut into a grid of equal tiles at each downsample, already in the
section's mosaic space, so a 1024 cell is stitched from whole tiles without any transform. Image pyramids (raw
camera tiles placed by a ``.mosaic`` transform) are not read here: the client draws those.

Mirrors the Viking client's parser (Clients/VolumeModel: Volume.cs, Section.cs, TileGridMapping.cs,
TileGridMappingBase.cs): attributes are matched case-insensitively, the tile file name is
``{FilePrefix}X{iX}_Y{iY}{FilePostfix}`` and the level folder is the downsample, both formatted with
``CoordFormat`` (default three digits). A tile at downsample ``ds`` covers mosaic X from ``iX*TileXDim*ds``, Y from
``iY*TileYDim*ds``, and its PNG row 0 is at its low Y.
"""

from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from typing import Dict, Optional
from urllib.parse import urljoin

DEFAULT_COORD_FORMAT = "03d"


def _attr(element: ET.Element, name: str) -> Optional[str]:
    """The attribute ``name`` matched case-insensitively, as the client's GetAttributeCaseInsensitive does."""
    wanted = name.lower()
    for key, value in element.attrib.items():
        if key.lower() == wanted:
            return value
    return None


def _local(element: ET.Element) -> str:
    tag = element.tag
    return tag.rsplit("}", 1)[-1] if isinstance(tag, str) else ""


def _python_format(coord_format: Optional[str]) -> str:
    """A Python format spec for the tile and level numbers. Accepts the Python style the volumes use ("03d")
    and the .NET style the client converts it to ("D3")."""
    if not coord_format:
        return DEFAULT_COORD_FORMAT
    match = re.fullmatch(r"[dD](\d+)", coord_format)
    if match:
        return f"0{int(match.group(1))}d"
    return coord_format


def _join(*parts: str) -> str:
    return "/".join(part.strip("/") for part in parts if part and part.strip("/"))


@dataclass(frozen=True)
class TilesetLevel:
    downsample: int
    grid_x: int
    grid_y: int


@dataclass(frozen=True)
class Tileset:
    name: str
    path: str
    tile_x: int
    tile_y: int
    prefix: str
    postfix: str
    coord_format: str
    levels: Dict[int, TilesetLevel]

    def tile_path(self, section_path: str, downsample: int, ix: int, iy: int) -> str:
        """The tile's path below the volume host."""
        fmt = self.coord_format
        name = f"{self.prefix}X{format(ix, fmt)}_Y{format(iy, fmt)}{self.postfix}"
        return _join(section_path, self.path, format(downsample, fmt), name)


@dataclass(frozen=True)
class VolumeSection:
    number: int
    path: str
    tilesets: Dict[str, Tileset]


@dataclass(frozen=True)
class VikingVolume:
    name: str
    # Tile URLs are host + "/" + path below it, as the client builds them.
    host: str
    sections: Dict[int, VolumeSection] = field(default_factory=dict)

    def tile_url(self, section: VolumeSection, tileset: Tileset, downsample: int, ix: int, iy: int) -> str:
        return self.host.rstrip("/") + "/" + tileset.tile_path(section.path, downsample, ix, iy)


def _parse_tileset(element: ET.Element) -> Optional[Tileset]:
    try:
        levels = {}
        for level in element:
            if _local(level) != "Level":
                continue
            ds = int(_attr(level, "Downsample"))
            levels[ds] = TilesetLevel(ds, int(_attr(level, "GridDimX")), int(_attr(level, "GridDimY")))
        if not levels:
            return None
        return Tileset(
            name=_attr(element, "name") or "",
            path=_attr(element, "path") or "",
            tile_x=int(_attr(element, "TileXDim")),
            tile_y=int(_attr(element, "TileYDim")),
            prefix=_attr(element, "FilePrefix") or "",
            postfix=_attr(element, "FilePostfix") or "",
            coord_format=_python_format(element.attrib.get("CoordFormat")),
            levels=levels,
        )
    except (TypeError, ValueError):
        return None


def _parse_section(element: ET.Element) -> Optional[VolumeSection]:
    number = _attr(element, "number")
    if number is None:
        return None
    tilesets = {}
    for child in element:
        if _local(child).lower() == "tileset":
            tileset = _parse_tileset(child)
            if tileset is not None:
                tilesets[tileset.name] = tileset
    return VolumeSection(number=int(number), path=_attr(element, "path") or "", tilesets=tilesets)


def parse_viking_xml(data: bytes, url: str) -> VikingVolume:
    """Parse a VikingXML document read from ``url``.

    The host is the volume's ``path`` attribute; without one it is the folder the document was read from, as the
    client does for a local volume.

    Raises:
        ValueError: The document is not a VikingXML volume.
    """
    try:
        root = ET.fromstring(data)
    except ET.ParseError as e:
        raise ValueError(f"VikingXML at {url} does not parse: {e}") from e
    if _local(root) != "Volume":
        raise ValueError(f"VikingXML at {url} has root <{_local(root)}>, not <Volume>")
    host = (_attr(root, "path") or urljoin(url, ".")).rstrip("/")
    sections: Dict[int, VolumeSection] = {}
    for child in root:
        tag = _local(child).lower()
        candidates = [child] if tag == "section" else [c for c in child if _local(c).lower() == "section"] if tag == "sections" else []
        for element in candidates:
            section = _parse_section(element)
            if section is not None:
                sections[section.number] = section
    return VikingVolume(name=_attr(root, "name") or "", host=host, sections=sections)
