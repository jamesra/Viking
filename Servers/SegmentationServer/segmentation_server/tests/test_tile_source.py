"""Reading a local context's tileset: the VikingXML parser, the cell assembly and encoding, and the Identity list."""

from __future__ import annotations

import json
from typing import Dict, List, Optional

import cv2
import numpy as np
import pytest

from segmentation_grpc import CoordinateSpace
from segmentation_server.capture_encoder import encode_capture, stretch_range
from segmentation_server.identity_client import IdentityAssignments, IdentityOptions
from segmentation_server.tile_ref import TileRef
from segmentation_server.tile_source import VikingTilesetSource, is_plain_mosaic_cell
from segmentation_server.viking_volume import parse_viking_xml

XML_URL = "http://storage.example/Rabbit/Rabbit.vikingxml"

VIKING_XML = b"""<?xml version="1.0"?>
<Volume name="Rabbit" num_stos="0" num_sections="2" path="http://storage.example/Rabbit/">
  <Section number="30" path="0030">
    <transform name="grid.mosaic" path="grid.mosaic" UseForVolume="true" FilePrefix="0030" FilePostfix=".png"/>
    <Pyramid name="8-bit" path="8-bit"><Level Downsample="1" path="001"/></Pyramid>
    <Tileset name="mosaic" path="mosaic" FilePrefix="0030_" FilePostfix=".png" TileXDim="512" TileYDim="512">
      <Level Downsample="1" GridDimX="3" GridDimY="3" path="001"/>
      <Level Downsample="4" GridDimX="1" GridDimY="1" path="004"/>
    </Tileset>
  </Section>
  <Sections><section Number="31"/></Sections>
</Volume>
"""


def test_the_parser_reads_tilesets_and_builds_the_clients_tile_urls() -> None:
    volume = parse_viking_xml(VIKING_XML, XML_URL)

    assert volume.name == "Rabbit"
    assert set(volume.sections) == {30, 31}
    section = volume.sections[30]
    tileset = section.tilesets["mosaic"]
    assert (tileset.tile_x, tileset.tile_y) == (512, 512)
    assert set(tileset.levels) == {1, 4}
    assert volume.tile_url(section, tileset, 4, 3, 5) == "http://storage.example/Rabbit/0030/mosaic/004/0030_X003_Y005.png"
    assert "8-bit" not in section.tilesets
    assert volume.sections[31].tilesets == {}


def test_without_a_path_the_host_is_the_documents_folder() -> None:
    volume = parse_viking_xml(b'<Volume name="V"><Section number="1"/></Volume>', XML_URL)

    assert volume.host == "http://storage.example/Rabbit"


def test_a_document_that_is_not_a_volume_is_refused() -> None:
    with pytest.raises(ValueError):
        parse_viking_xml(b"<Other/>", XML_URL)


def test_the_stretch_matches_the_clients_percentiles() -> None:
    values = np.concatenate([np.full(100, 10), np.arange(256).repeat(40), np.full(100, 250)]).astype(np.uint8)

    lo, hi = stretch_range(values)

    histogram = np.bincount(values, minlength=256)
    cumulative = np.cumsum(histogram)
    assert lo == int(np.argmax(cumulative >= int(np.ceil(values.size * 0.01))))
    assert hi == int(np.argmax(cumulative >= int(np.ceil(values.size * 0.99))))


def test_a_flat_image_keeps_its_value() -> None:
    decoded = cv2.imdecode(np.frombuffer(encode_capture(np.full((8, 8), 77, np.uint8)), np.uint8), cv2.IMREAD_UNCHANGED)

    assert decoded.ndim == 2 and decoded.dtype == np.uint8
    assert np.all(decoded == 77)


MOSAIC = CoordinateSpace.COORDINATE_SPACE_MOSAIC
VOLUME = CoordinateSpace.COORDINATE_SPACE_VOLUME
UNSPECIFIED = CoordinateSpace.COORDINATE_SPACE_UNSPECIFIED


@pytest.mark.parametrize(
    "channel, space, plain",
    [
        ("mosaic", MOSAIC, True),
        ("mosaic", VOLUME, False),
        ("mosaic", UNSPECIFIED, False),
        ("mosaic[color=255,0,0,255]", MOSAIC, False),
        ("", MOSAIC, False),
    ],
)
def test_only_plain_mosaic_cells_are_read(channel: str, space: int, plain: bool) -> None:
    assert is_plain_mosaic_cell(_coord(channel=channel, space=space)) is plain


class _Assignments:
    def __init__(self, contexts: Dict[int, str]) -> None:
        self.contexts = contexts

    async def get(self, annotation_context_id: int):
        from segmentation_server.identity_client import LocalContext

        endpoint = self.contexts.get(annotation_context_id)
        return None if endpoint is None else LocalContext(annotation_context_id, "Rabbit", endpoint)


def _png(image: np.ndarray) -> bytes:
    ok, encoded = cv2.imencode(".png", image)
    assert ok
    return encoded.tobytes()


def _coord(row: int = 0, col: int = 0, channel: str = "mosaic", space: int = MOSAIC, ds: int = 1) -> TileRef:
    return TileRef(
        volume="Rabbit", section=30, channel=channel, transform="|grid.mosaic", space=space, downsample=ds, row=row, col=col
    )


def _storage(tiles: Dict[str, bytes]) -> "tuple[List[str], callable]":
    requested: List[str] = []

    def get(url: str) -> Optional[bytes]:
        requested.append(url)
        if url == XML_URL:
            return VIKING_XML
        return tiles.get(url)

    return requested, get


def _tile_url(ix: int, iy: int, ds: int = 1) -> str:
    return f"http://storage.example/Rabbit/0030/mosaic/{ds:03d}/0030_X{ix:03d}_Y{iy:03d}.png"


@pytest.mark.asyncio
async def test_a_cell_is_stitched_from_its_tiles_with_its_first_row_at_high_mosaic_y() -> None:
    # Tile (ix, iy) is flat grey 40*(1 + ix + 2*iy); a missing tile is black.
    tiles = {_tile_url(ix, iy): _png(np.full((512, 512), 40 * (1 + ix + 2 * iy), np.uint8)) for ix in range(2) for iy in range(2)}
    del tiles[_tile_url(1, 1)]
    requested, get = _storage(tiles)
    source = VikingTilesetSource(_Assignments({5: XML_URL}), http_get=get)

    data = await source.fetch(5, _coord())

    image = cv2.imdecode(np.frombuffer(data, np.uint8), cv2.IMREAD_UNCHANGED)
    assert image.shape == (1024, 1024)
    # Top-left of the capture is the high-Y, low-X quadrant: tile (0, 1). Bottom-left is tile (0, 0).
    top_left, top_right, bottom_left, bottom_right = image[0, 0], image[0, 1023], image[1023, 0], image[1023, 1023]
    assert top_right == 0  # tile (1, 1) is missing: black, stretched to 0
    assert bottom_left < bottom_right < top_left
    assert sorted(requested[1:]) == sorted(_tile_url(ix, iy) for ix in range(2) for iy in range(2))
    source.close()


@pytest.mark.asyncio
async def test_tiles_outside_the_grid_are_not_requested() -> None:
    requested, get = _storage({_tile_url(2, 2): _png(np.full((512, 512), 9, np.uint8))})
    source = VikingTilesetSource(_Assignments({5: XML_URL}), http_get=get)

    assert await source.fetch(5, _coord(row=1, col=1)) is not None
    assert requested[1:] == [_tile_url(2, 2)]
    source.close()


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "context_id, coord",
    [
        (6, _coord()),  # not a local context
        (5, _coord(channel="8-bit")),  # a pyramid, not a tileset
        (5, _coord(ds=2)),  # no tileset level at that downsample
        (5, TileRef(volume="Other", section=30, channel="mosaic", transform="|", space=MOSAIC, downsample=1, row=0, col=0)),
        (5, _coord(space=VOLUME)),
        (5, _coord(space=UNSPECIFIED)),
    ],
)
async def test_cells_the_source_cannot_draw_are_left_to_the_client(context_id: int, coord: TileRef) -> None:
    _requested, get = _storage({})
    source = VikingTilesetSource(_Assignments({5: XML_URL}), http_get=get)

    assert await source.fetch(context_id, coord) is None
    assert not await source.can_read(context_id, coord)
    source.close()


@pytest.mark.asyncio
async def test_can_read_answers_without_reading_a_tile() -> None:
    requested, get = _storage({})
    source = VikingTilesetSource(_Assignments({5: XML_URL}), http_get=get)

    assert await source.can_read(5, _coord())
    assert requested == [XML_URL]
    source.close()


@pytest.mark.asyncio
async def test_a_storage_failure_leaves_the_cell_to_the_client() -> None:
    def get(url: str) -> Optional[bytes]:
        if url == XML_URL:
            return VIKING_XML
        raise OSError("connection reset")

    source = VikingTilesetSource(_Assignments({5: XML_URL}), http_get=get)

    assert await source.fetch(5, _coord()) is None
    source.close()


def _identity(rows: list, calls: list):
    def call(url: str, form, headers) -> bytes:
        calls.append((url, form, headers))
        if url.endswith("connect/token"):
            return json.dumps({"access_token": "tok"}).encode()
        return json.dumps(rows).encode()

    return call


_OPTIONS = IdentityOptions(
    endpoint="https://identity.example/id",
    api_endpoint="https://identity.example/api",
    client_id="segmentation",
    client_secret="s3cret",
    service_name="seg utah",
)


@pytest.mark.asyncio
async def test_identity_lists_the_local_contexts_with_a_client_credentials_token() -> None:
    calls: list = []
    rows = [{"annotationContextId": 5, "name": "Rabbit", "endpoint": XML_URL}, {"annotationContextId": 6, "endpoint": None}]
    clock = [0.0]
    assignments = IdentityAssignments(_OPTIONS, http_call=_identity(rows, calls), time_fn=lambda: clock[0])

    local = await assignments.get(5)

    assert local is not None and local.endpoint == XML_URL
    assert await assignments.get(6) is None
    token_url, form, _ = calls[0]
    assert token_url == "https://identity.example/id/connect/token"
    assert form["grant_type"] == "client_credentials" and form["client_id"] == "segmentation"
    list_url, _, headers = calls[1]
    assert list_url == "https://identity.example/api/Permissions/AssignedAnnotationContexts?service=seg%20utah"
    assert headers["Authorization"] == "Bearer tok"
    # The unknown id 6 did not refresh again within the minute.
    assert len(calls) == 2


@pytest.mark.asyncio
async def test_an_unknown_context_refreshes_after_a_minute_and_a_failure_keeps_the_list() -> None:
    calls: list = []
    rows = [{"annotationContextId": 5, "endpoint": XML_URL}]
    clock = [0.0]
    assignments = IdentityAssignments(_OPTIONS, http_call=_identity(rows, calls), time_fn=lambda: clock[0])
    assert await assignments.get(5) is not None

    rows.append({"annotationContextId": 7, "endpoint": XML_URL})
    clock[0] = 61.0
    assert await assignments.get(7) is not None

    def broken(url, form, headers):
        raise OSError("identity down")

    assignments._http_call = broken
    clock[0] = 1000.0
    assert await assignments.get(5) is not None


def test_identity_options_need_endpoint_secret_and_name(monkeypatch) -> None:
    for name in ("IdentityServer__Endpoint", "IdentityServer__ClientSecret", "SEGMENTATION_SERVICE_NAME", "IdentityServer__ApiEndpoint"):
        monkeypatch.delenv(name, raising=False)
    assert IdentityOptions.from_env() is None

    monkeypatch.setenv("IdentityServer__Endpoint", "https://identity.example/")
    monkeypatch.setenv("IdentityServer__ClientSecret", "s")
    monkeypatch.setenv("SEGMENTATION_SERVICE_NAME", "seg")
    options = IdentityOptions.from_env()
    assert options is not None and options.api_endpoint == "https://identity.example/" and options.client_id == "segmentation"


def test_the_cache_key_separates_mosaic_from_volume() -> None:
    mosaic, volume = (_coord(space=space).key for space in (MOSAIC, VOLUME))

    assert mosaic != volume
    assert mosaic[4] == "mosaic" and volume[4] == "volume"
