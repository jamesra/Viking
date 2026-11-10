"""location_source: SegmentLocation's annotations read from OData and handed to the prompt provider."""

from __future__ import annotations

from typing import Any, Dict, List, Optional, Sequence
from urllib.parse import unquote

import grpc
import pytest

from segmentation_grpc import CacheMode, Circle, LocationSet, LocationStart, SegmentOptions, Shape, Vector2
from segmentation_grpc import prompt_input_pb2 as prompt_pb
from segmentation_grpc import segmentation_pb2 as pb
from segmentation_server.identity_client import LocalContext
from segmentation_server.location_source import (
    LocationError,
    LocationPrompts,
    last_modified_ticks,
    odata_base,
    record_from_entity,
)
from segmentation_server.prompt_provider import PromptRejected
from segmentation_server.viking_volume import Tileset, TilesetLevel, VikingVolume, VolumeSection, parse_viking_xml

BASE = "https://websvc.example/RC2/OData"


def _volume(**fields: Any) -> VikingVolume:
    tileset = Tileset("TEM", "TEM", 256, 256, "", ".png", "03d", {1: TilesetLevel(1, 10, 10)})
    values: Dict[str, Any] = dict(
        name="RC2", host="http://h/RC2", sections={536: VolumeSection(536, "0536", {"TEM": tileset})},
        default_tileset="TEM", annotation_endpoint="https://websvc.example/RC2/Annotation/Service.svc",
    )
    values.update(fields)
    return VikingVolume(**values)


class _Volumes:
    def __init__(self, volume: Optional[VikingVolume] = None, known: bool = True) -> None:
        self.volume = volume if volume is not None else _volume()
        self.known = known

    async def context_volume(self, annotation_context_id: int):
        return (LocalContext(annotation_context_id, "RC2", "http://h/RC2.VikingXML"), self.volume) if self.known else None


class _Provider:
    """Reach is the targets' bounds; the request carries the targets as locations, as the Viking provider does."""

    name = "fake"

    def __init__(self, reject: bool = False) -> None:
        self.reject = reject
        self.inputs: List[prompt_pb.PromptInput] = []

    def reach(self, prompt_input: prompt_pb.PromptInput) -> prompt_pb.PromptReach:
        target = prompt_input.targets[0]
        return prompt_pb.PromptReach(reach=dict(
            x_min=target.x - 100, y_min=target.y - 100, x_max=target.x + 100, y_max=target.y + 100,
        ))

    def build_start(self, prompt_input: prompt_pb.PromptInput) -> pb.SegmentTilesRequest:
        if self.reject:
            raise PromptRejected("a point is not a prompt")
        self.inputs.append(prompt_input)
        return pb.SegmentTilesRequest(
            locations=[pb.LocationRef(id=t.id, last_modified_ticks=t.last_modified_ticks) for t in prompt_input.targets],
            refresh=prompt_input.refresh,
        )

    def background_for(self, prompt_input, needed, sent_ids: Sequence[int]) -> prompt_pb.PromptBackground:
        return prompt_pb.PromptBackground(sent_ids=[9])


def _entity(id: int, x: float, y: float, *, structure: int = 100, type_code: int = 1, wkt: str = "") -> Dict[str, Any]:
    return {
        "ID": id, "ParentID": structure, "TypeCode": type_code, "Z": 536, "X": x, "Y": y, "Radius": 20.0,
        "MosaicShape": {"Geometry": {"WellKnownText": wkt or f"CURVEPOLYGON(CIRCULARSTRING({x + 20} {y}, {x} {y + 20}))"}},
        "LastModified": "2026-11-04T12:00:00.1234567-06:00",
        "Parent": {"ID": structure, "TypeID": 1, "ParentID": 0, "Type": {"ID": 1, "ParentID": 0}},
    }


class _OData:
    """Answers the three queries from fixed rows and records each URL."""

    def __init__(self, targets: List[Dict[str, Any]], neighbours: List[Dict[str, Any]], structures: List[Dict[str, Any]]):
        self.targets, self.neighbours, self.structures = targets, neighbours, structures
        self.urls: List[str] = []

    def __call__(self, url: str) -> Dict[str, Any]:
        self.urls.append(unquote(url))
        if "/Structures?" in url:
            return {"value": self.structures}
        if "Z eq 536" in unquote(url):
            return {"value": self.neighbours}
        return {"value": self.targets}


def _start(**fields: Any) -> LocationStart:
    values: Dict[str, Any] = dict(annotation_context_id=42, location_id=1)
    values.update(fields)
    return LocationStart(**values)


def _prompts(odata: _OData, provider: Optional[_Provider] = None, volumes: Optional[_Volumes] = None) -> LocationPrompts:
    return LocationPrompts(volumes or _Volumes(), provider or _Provider(), "v5", get_json=odata, environ={})


@pytest.mark.asyncio
async def test_the_input_holds_the_target_the_neighbours_in_reach_and_the_structure_ancestors() -> None:
    near = _entity(2, 1050.0, 1000.0, structure=200)
    far = _entity(3, 5000.0, 5000.0, structure=300)
    child = dict(_entity(4, 1020.0, 1000.0, structure=400))
    child["Parent"] = {"ID": 400, "TypeID": 1, "ParentID": 100, "Type": {"ID": 1, "ParentID": 0}}
    target = _entity(1, 1000.0, 1000.0)
    target["Parent"] = {"ID": 100, "TypeID": 1, "ParentID": 50, "Type": {"ID": 1, "ParentID": 0}}
    odata = _OData([target], [target, near, far, child], [{"ID": 50, "ParentID": None, "TypeID": 7, "Type": {"ParentID": 0}}])
    provider = _Provider()

    prepared = await _prompts(odata, provider).prepare(_start(options=SegmentOptions(mask_threshold=0.5, job_id=3)))

    prompt_input = provider.inputs[0]
    assert (prompt_input.volume, prompt_input.section, prompt_input.tileset) == ("RC2", 536, "TEM")
    assert (prompt_input.model_id, prompt_input.mask_threshold, prompt_input.job_id) == ("v5", 0.5, 3)
    assert [(t.id, t.x, t.y, t.radius) for t in prompt_input.targets] == [(1, 1000.0, 1000.0, 20.0)]
    assert sorted(c.id for c in prompt_input.candidates) == [1, 2, 4]
    assert sorted(s.id for s in prompt_input.structures) == [50, 100, 200, 400]
    assert prepared.readable
    assert prepared.request.locations[0].last_modified_ticks == last_modified_ticks("2026-11-04T18:00:00.1234567Z")
    assert all(url.startswith(BASE) for url in odata.urls)


@pytest.mark.asyncio
async def test_a_section_is_read_once_for_several_locations() -> None:
    odata = _OData([_entity(1, 1000.0, 1000.0)], [_entity(2, 1050.0, 1000.0)], [])
    prompts = _prompts(odata)

    await prompts.prepare(_start())
    await prompts.prepare(_start())

    assert sum("Z eq 536" in url for url in odata.urls) == 1


@pytest.mark.asyncio
@pytest.mark.parametrize("mode, kept, refresh", [
    (CacheMode.Value("CACHE_MODE_USE"), True, False),
    (CacheMode.Value("CACHE_MODE_REFRESH"), True, True),
    (CacheMode.Value("CACHE_MODE_BYPASS"), False, False),
])
async def test_the_cache_mode_decides_whether_the_request_names_its_locations(mode, kept, refresh) -> None:
    odata = _OData([_entity(1, 1000.0, 1000.0)], [], [])

    prepared = await _prompts(odata).prepare(_start(options=SegmentOptions(cache_mode=mode)))

    assert bool(prepared.request.locations) == kept
    assert prepared.request.refresh == refresh


@pytest.mark.asyncio
async def test_a_shape_override_takes_the_targets_place_and_is_never_cached() -> None:
    odata = _OData([_entity(1, 1000.0, 1000.0, type_code=4, wkt="POLYGON((0 0, 10 0, 10 10, 0 0))")], [], [])
    provider = _Provider()
    override = Shape(circle=Circle(center=Vector2(x=5.0, y=4.0), radius=3.0))

    prepared = await _prompts(odata, provider).prepare(_start(shape_override=override))

    [target] = provider.inputs[0].targets
    assert (target.id, target.type_code, target.mosaic_wkt, target.x, target.y, target.radius) == (1, 1, "", 5.0, 4.0, 3.0)
    assert not prepared.request.locations


@pytest.mark.asyncio
async def test_a_merged_group_is_one_input_with_every_target() -> None:
    odata = _OData([_entity(1, 1000.0, 1000.0), _entity(2, 1100.0, 1000.0)], [], [])
    provider = _Provider()

    await _prompts(odata, provider).prepare(_start(location_id=0, locations=LocationSet(ids=[2, 1])))

    assert [t.id for t in provider.inputs[0].targets] == [1, 2]


@pytest.mark.asyncio
@pytest.mark.parametrize("prompts, start, code", [
    (lambda: _prompts(_OData([], [], [])), _start(), grpc.StatusCode.NOT_FOUND),
    (lambda: _prompts(_OData([_entity(1, 0, 0)], [], []), volumes=_Volumes(known=False)), _start(), grpc.StatusCode.NOT_FOUND),
    (lambda: _prompts(_OData([_entity(1, 0, 0)], [], []), _Provider(reject=True)), _start(), grpc.StatusCode.INVALID_ARGUMENT),
    (lambda: _prompts(_OData([_entity(1, 0, 0)], [], [])), LocationStart(annotation_context_id=42), grpc.StatusCode.INVALID_ARGUMENT),
    (
        lambda: _prompts(_OData([_entity(1, 0, 0)], [], []), volumes=_Volumes(_volume(annotation_endpoint=""))),
        _start(),
        grpc.StatusCode.FAILED_PRECONDITION,
    ),
])
async def test_a_start_that_cannot_be_built_says_why(prompts, start, code) -> None:
    with pytest.raises(LocationError) as raised:
        await prompts().prepare(start)

    assert raised.value.code == code


@pytest.mark.asyncio
async def test_a_section_without_its_tileset_at_full_resolution_is_not_readable() -> None:
    odata = _OData([_entity(1, 1000.0, 1000.0)], [], [])
    tileset = Tileset("TEM", "TEM", 256, 256, "", ".png", "03d", {2: TilesetLevel(2, 5, 5)})
    volumes = _Volumes(_volume(sections={536: VolumeSection(536, "0536", {"TEM": tileset})}))

    prepared = await _prompts(odata, volumes=volumes).prepare(_start())

    assert not prepared.readable


@pytest.mark.asyncio
async def test_a_named_tileset_is_the_one_the_prompt_is_built_on() -> None:
    leveled = Tileset("TEM.Leveled", "TEM/Leveled", 256, 256, "", ".png", "03d", {1: TilesetLevel(1, 10, 10)})
    plain = Tileset("TEM", "TEM", 256, 256, "", ".png", "03d", {1: TilesetLevel(1, 10, 10)})
    volume = _volume(sections={536: VolumeSection(536, "0536", {"TEM": plain, "TEM.Leveled": leveled})})
    provider = _Provider()
    odata = _OData([_entity(1, 1000.0, 1000.0)], [], [])

    await _prompts(odata, provider, _Volumes(volume)).prepare(_start(tileset="TEM.Leveled"))
    await _prompts(odata, provider, _Volumes(volume)).prepare(_start())

    assert [item.tileset for item in provider.inputs] == ["TEM.Leveled", "TEM"]


@pytest.mark.asyncio
async def test_a_named_tileset_the_section_lacks_is_refused_not_replaced() -> None:
    odata = _OData([_entity(1, 1000.0, 1000.0)], [], [])

    with pytest.raises(LocationError) as raised:
        await _prompts(odata).prepare(_start(tileset="TEM.Leveled"))

    assert raised.value.code == grpc.StatusCode.FAILED_PRECONDITION


def test_the_odata_root_comes_from_the_template_else_the_annotation_service() -> None:
    assert odata_base(_volume(), {}) == BASE
    assert odata_base(_volume(), {"SEGMENTATION_ODATA_TEMPLATE": "https://o/{volume}/OData/"}) == "https://o/RC2/OData"
    assert odata_base(_volume(annotation_endpoint="https://x/other"), {}) == ""
    # RC1's older VikingXML names the service by its own file, not Service.svc.
    legacy = _volume(annotation_endpoint="https://websvc.codepharm.net/RC1/Annotation/Annotation.AnnotateService.svc")
    assert odata_base(legacy, {}) == "https://websvc.codepharm.net/RC1/OData"


def test_a_row_without_a_section_is_skipped_and_a_circle_keeps_its_own_centre() -> None:
    assert record_from_entity({"ID": 1, "TypeCode": 1}) is None
    record = record_from_entity(_entity(1, 7.0, 8.0))
    annotation = record.annotation()
    assert (annotation.x, annotation.y, annotation.radius) == (7.0, 8.0, 20.0)
    assert record.bounds() == (-13.0, -12.0, 27.0, 28.0)


def test_the_viking_xml_names_its_default_tileset_and_annotation_service() -> None:
    volume = parse_viking_xml(
        b'<Volume Name="RC2" path="http://h/RC2"><DefaultTileset name="TEM"/>'
        b'<VolumeToEndpoint Endpoint="https://websvc.example/RC2/Annotation/Service.svc"/></Volume>',
        "http://h/RC2/SliceToVolume.VikingXML",
    )

    assert (volume.default_tileset, volume.annotation_endpoint) == ("TEM", "https://websvc.example/RC2/Annotation/Service.svc")
