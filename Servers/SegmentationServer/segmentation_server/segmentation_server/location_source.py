"""The annotations of a SegmentLocation call, read from the volume's OData service and turned into a request.

For each call the server reads the target location(s), the section's cell-level neighbours and the structures the
neighbour rule walks, as a client would, and hands them to the served model's prompt provider (:mod:`prompt_provider`)
as one ``PromptInput``. Reading them here, not taking them from the client, makes every client's request for a location
the same and keys the result cache on the LastModified the server itself read.

The OData root is ``SEGMENTATION_ODATA_TEMPLATE`` with ``{volume}`` when set, else the context's VikingXML
``VolumeToEndpoint`` annotation service with its ``/Annotation/Service.svc`` replaced by ``/OData``. Reads are
anonymous, as OData allows. The grid is the section's tileset named by the VikingXML (``DefaultTileset``, else the
first by name), MOSAIC at downsample 1.

A section's neighbours are read once and kept for :data:`SECTION_SECONDS`; structures for :data:`STRUCTURE_SECONDS`.
"""

from __future__ import annotations

import asyncio
import json
import logging
import os
import re
import time
from collections import OrderedDict
from concurrent import futures
from dataclasses import dataclass
from datetime import date
from typing import Any, Callable, Dict, Iterable, Iterator, List, Optional, Sequence, Tuple
from urllib.parse import quote, urljoin, urlparse
from urllib.request import Request, urlopen

import grpc

from segmentation_grpc import prompt_input_pb2 as prompt_pb
from segmentation_grpc import segmentation_pb2 as pb
from segmentation_server.prompt_provider import PromptProvider, PromptRejected, PromptTooLarge
from segmentation_server.viking_volume import VikingVolume

logger = logging.getLogger(__name__)

SELECT = "ID,ParentID,TypeCode,Z,X,Y,MosaicShape,LastModified,Radius"
EXPAND = "Parent($select=ID,TypeID,ParentID;$expand=Type($select=ID,ParentID))"
PAGE_SIZE = 500
# The neighbours a prompt may use (the Viking provider's rule): circles, polygons and curve polygons of structures whose
# type is Cell (1) or a child type of Cell, which is what the model saw as negatives in training.
NEIGHBOUR_FILTER = "(TypeCode eq 1 or TypeCode eq 4 or TypeCode eq 6) and (Parent/TypeID eq 1 or Parent/Type/ParentID eq 1)"
LOCATIONS_PER_QUERY = 50
STRUCTURES_PER_QUERY = 50
# Candidates are kept when their bounds meet the provider's reach grown by this much (mosaic pixels): bounds here come
# from the WKT control points, the provider's from its curve fit, and the provider filters exactly afterwards.
REACH_MARGIN = 64.0
SECTION_SECONDS = 60.0
SECTIONS_KEPT = 16
STRUCTURE_SECONDS = 600.0
_TIMEOUT_SECONDS = 60.0
_SERVICE_SUFFIX = re.compile(r"/Annotation/[^/]+\.svc/?$", re.IGNORECASE)

# url -> decoded JSON document. Injected in tests.
GetJson = Callable[[str], Dict[str, Any]]


def http_get_json(url: str) -> Dict[str, Any]:
    """One OData page, at most :data:`PAGE_SIZE` rows."""
    headers = {"Accept": "application/json", "Prefer": f"odata.maxpagesize={PAGE_SIZE}"}
    with urlopen(Request(url, headers=headers), timeout=_TIMEOUT_SECONDS) as response:
        payload = json.loads(response.read().decode("utf-8"))
    if not isinstance(payload, dict):
        raise ValueError("OData response is not an object")
    return payload


class LocationError(Exception):
    """A SegmentLocation start the server cannot build a request for, with the status the call ends with."""

    code: grpc.StatusCode

    def __init__(self, code: grpc.StatusCode, message: str) -> None:
        super().__init__(message)
        self.code = code


@dataclass(frozen=True)
class LocationRecord:
    """One location as the provider takes it. ``x``, ``y`` and ``radius`` matter for a circle only."""

    id: int
    structure_id: int
    z: int
    type_code: int
    wkt: str
    x: float
    y: float
    radius: float
    last_modified_ticks: int
    structure_parent_id: int
    structure_type_id: int
    structure_type_parent_id: int

    def annotation(self) -> "prompt_pb.PromptAnnotation":
        """The location as a provider input: the raw MosaicShape WKT (the provider fits curves itself), and for a
        circle its X, Y and TRUE mosaic radius (its WKT is a rim ring whose first point is not the centre)."""
        annotation = prompt_pb.PromptAnnotation(
            id=self.id, type_code=self.type_code, mosaic_wkt=self.wkt, structure_id=self.structure_id,
            last_modified_ticks=self.last_modified_ticks,
        )
        if self.type_code == 1:
            annotation.x, annotation.y, annotation.radius = self.x, self.y, self.radius
        return annotation

    def bounds(self) -> Optional[Tuple[float, float, float, float]]:
        """(min_x, min_y, max_x, max_y) in mosaic pixels, or None without geometry."""
        if self.type_code == 1:
            return self.x - self.radius, self.y - self.radius, self.x + self.radius, self.y + self.radius
        numbers = [float(n) for n in _NUMBER.findall(self.wkt)]
        if len(numbers) < 2:
            return None
        xs, ys = numbers[0::2], numbers[1::2]
        return min(xs), min(ys), max(xs), max(ys)


@dataclass(frozen=True)
class PreparedLocation:
    """A SegmentLocation start turned into the request the walk runs, and what answers its background later."""

    request: "pb.SegmentTilesRequest"
    prompt_input: "prompt_pb.PromptInput"
    # The server reads the section's tiles itself.
    readable: bool


_NUMBER = re.compile(r"-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?")
_TIMESTAMP = re.compile(r"(\d{4})-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:\.(\d+))?(Z|[+-]\d\d:\d\d)?$")
_TICKS_PER_SECOND = 10_000_000
_TICKS_PER_DAY = 86_400 * _TICKS_PER_SECOND


def last_modified_ticks(text: str) -> int:
    """An OData ``LastModified`` (ISO 8601 with an offset) as UTC .NET ticks, the protocol's
    ``LocationRef.last_modified_ticks``. Integer arithmetic on the text, so all seven digits of a .NET timestamp
    survive (``datetime`` keeps six). A text without an offset is taken as UTC; an unreadable one is 0."""
    match = _TIMESTAMP.match((text or "").strip())
    if match is None:
        return 0
    year, month, day, hour, minute, second = (int(match.group(i)) for i in range(1, 7))
    fraction = ((match.group(7) or "") + "0000000")[:7]
    ticks = (date(year, month, day).toordinal() - 1) * _TICKS_PER_DAY
    ticks += ((hour * 60 + minute) * 60 + second) * _TICKS_PER_SECOND + int(fraction)
    offset = match.group(8)
    if offset and offset != "Z":
        sign = 1 if offset[0] == "+" else -1
        ticks -= sign * (int(offset[1:3]) * 60 + int(offset[4:6])) * 60 * _TICKS_PER_SECOND
    return ticks


def _wkt(shape: object) -> str:
    """The WellKnownText of an OData MosaicShape payload, or "" when it has none."""
    if isinstance(shape, str):
        return shape
    if not isinstance(shape, dict):
        return ""
    geometry = shape.get("Geometry") or shape.get("geometry") or shape
    if isinstance(geometry, str):
        return geometry
    if isinstance(geometry, dict):
        text = geometry.get("WellKnownText") or geometry.get("wellKnownText")
        if isinstance(text, str):
            return text
    return ""


def _int(value: Any) -> int:
    return 0 if value in (None, "") else int(value)


def _float(value: Any) -> float:
    return 0.0 if value in (None, "") else float(value)


def record_from_entity(entity: Dict[str, Any]) -> Optional[LocationRecord]:
    """The location in one OData row, or None when the row has no id, type or section."""
    if entity.get("ID") is None or entity.get("TypeCode") in (None, "") or entity.get("Z") in (None, ""):
        return None
    parent = entity.get("Parent") or {}
    parent_type = parent.get("Type") or {}
    return LocationRecord(
        id=int(entity["ID"]),
        structure_id=_int(entity.get("ParentID", parent.get("ID"))),
        z=int(entity["Z"]),
        type_code=int(entity["TypeCode"]),
        wkt=_wkt(entity.get("MosaicShape")),
        x=_float(entity.get("X")),
        y=_float(entity.get("Y")),
        radius=_float(entity.get("Radius")),
        last_modified_ticks=last_modified_ticks(str(entity.get("LastModified") or "")),
        structure_parent_id=_int(parent.get("ParentID")),
        structure_type_id=_int(parent.get("TypeID")),
        structure_type_parent_id=_int(parent_type.get("ParentID")),
    )


class ODataReader:
    """The three reads a location prompt needs, page by page. Blocking."""

    _get_json: GetJson

    def __init__(self, get_json: GetJson = http_get_json) -> None:
        self._get_json = get_json

    def _pages(self, url: str) -> Iterator[Dict[str, Any]]:
        """Every page of an answer: follow ``@odata.nextLink`` until a page leaves it out."""
        next_url: Optional[str] = url
        while next_url:
            document = self._get_json(next_url)
            yield document
            link = document.get("@odata.nextLink")
            if not link:
                return
            next_url = str(link) if urlparse(str(link)).scheme else urljoin(next_url, str(link))

    def _locations(self, base: str, filter_expression: str) -> List[LocationRecord]:
        url = (
            f"{base.rstrip('/')}/Locations?$filter={quote(filter_expression, safe='()/')}"
            f"&$orderby=ID&$select={quote(SELECT, safe=',')}&$expand={quote(EXPAND, safe='();$,')}"
        )
        records = []
        for page in self._pages(url):
            for entity in page.get("value") or []:
                record = record_from_entity(entity) if isinstance(entity, dict) else None
                if record is not None:
                    records.append(record)
        return records

    def locations(self, base: str, ids: Sequence[int]) -> List[LocationRecord]:
        """The locations with these ids, in id order (fewer when some do not exist)."""
        unique = sorted(set(int(i) for i in ids))
        found: List[LocationRecord] = []
        for start in range(0, len(unique), LOCATIONS_PER_QUERY):
            clause = " or ".join(f"ID eq {i}" for i in unique[start:start + LOCATIONS_PER_QUERY])
            found.extend(self._locations(base, f"({clause})"))
        return found

    def section_neighbours(self, base: str, z: int) -> List[LocationRecord]:
        """Every location on section ``z`` the neighbour rule could accept."""
        return self._locations(base, f"Z eq {int(z)} and {NEIGHBOUR_FILTER}")

    def structures(self, base: str, ids: Sequence[int]) -> List["prompt_pb.PromptStructure"]:
        """The structures with these ids (fewer when some do not exist)."""
        unique = sorted(set(int(i) for i in ids))
        found = []
        for start in range(0, len(unique), STRUCTURES_PER_QUERY):
            clause = " or ".join(f"ID eq {i}" for i in unique[start:start + STRUCTURES_PER_QUERY])
            url = (
                f"{base.rstrip('/')}/Structures?$filter={quote(clause, safe='()/')}"
                f"&$select=ID,ParentID,TypeID&$expand={quote('Type($select=ParentID)', safe='();$,')}"
            )
            for page in self._pages(url):
                for entity in page.get("value") or []:
                    if isinstance(entity, dict) and entity.get("ID") is not None:
                        found.append(prompt_pb.PromptStructure(
                            id=int(entity["ID"]), parent_id=_int(entity.get("ParentID")),
                            type_id=_int(entity.get("TypeID")),
                            type_parent_id=_int((entity.get("Type") or {}).get("ParentID")),
                        ))
        return found


def odata_base(volume: VikingVolume, environ: Optional[Dict[str, str]] = None) -> str:
    """The volume's OData root, or "" when neither the template nor the VikingXML gives one."""
    template = (environ if environ is not None else os.environ).get("SEGMENTATION_ODATA_TEMPLATE", "").strip()
    if template:
        return template.replace("{volume}", quote(volume.name, safe="")).rstrip("/")
    endpoint = volume.annotation_endpoint.strip()
    if endpoint and _SERVICE_SUFFIX.search(endpoint):
        return _SERVICE_SUFFIX.sub("/OData", endpoint)
    return ""


def _meets(bounds: Tuple[float, float, float, float], rect: Any) -> bool:
    x0, y0, x1, y1 = bounds
    return (
        x0 <= rect.x_max + REACH_MARGIN and x1 >= rect.x_min - REACH_MARGIN
        and y0 <= rect.y_max + REACH_MARGIN and y1 >= rect.y_min - REACH_MARGIN
    )


class LocationPrompts:
    """Builds SegmentLocation requests. Runs on the server's event loop; OData and the provider run on ``executor``."""

    _volumes: Any
    _provider: PromptProvider
    _model_id: str
    _odata: ODataReader
    _executor: Optional[futures.Executor]
    _clock: Callable[[], float]
    _environ: Optional[Dict[str, str]]
    _sections: "OrderedDict[Tuple[str, int], Tuple[float, List[LocationRecord]]]"
    _structures: Dict[Tuple[str, int], Tuple[float, prompt_pb.PromptStructure]]

    def __init__(
        self,
        volumes: Any,
        provider: PromptProvider,
        model_id: str = "",
        *,
        get_json: GetJson = http_get_json,
        executor: Optional[futures.Executor] = None,
        clock: Callable[[], float] = time.monotonic,
        environ: Optional[Dict[str, str]] = None,
    ) -> None:
        """
        Args:
            volumes: The tile source (:class:`tile_source.VikingTilesetSource`), or anything with its
                ``await context_volume(id)`` -> ``(LocalContext, VikingVolume or None)`` or None.
            provider: The served model's prompt provider.
            model_id: Written into each request, so the walk's model check passes.
        """
        self._volumes = volumes
        self._provider = provider
        self._model_id = model_id
        self._odata = ODataReader(get_json)
        self._executor = executor
        self._clock = clock
        self._environ = environ
        self._sections = OrderedDict()
        self._structures = {}

    @property
    def provider(self) -> PromptProvider:
        return self._provider

    async def _run(self, work: Callable[..., Any], *args: Any) -> Any:
        return await asyncio.get_running_loop().run_in_executor(self._executor, work, *args)

    async def _section(self, base: str, z: int) -> List[LocationRecord]:
        key = (base, z)
        held = self._sections.get(key)
        if held is not None and self._clock() - held[0] < SECTION_SECONDS:
            self._sections.move_to_end(key)
            return held[1]
        records = await self._run(self._odata.section_neighbours, base, z)
        self._sections[key] = (self._clock(), records)
        self._sections.move_to_end(key)
        while len(self._sections) > SECTIONS_KEPT:
            self._sections.popitem(last=False)
        return records

    async def _structures_for(self, base: str, records: Iterable[LocationRecord]) -> List["prompt_pb.PromptStructure"]:
        """The records' structures with their ancestors, reading only the ancestors not already known."""
        now = self._clock()
        known: Dict[int, prompt_pb.PromptStructure] = {}
        for record in records:
            if record.structure_id:
                known[record.structure_id] = prompt_pb.PromptStructure(
                    id=record.structure_id, parent_id=record.structure_parent_id, type_id=record.structure_type_id,
                    type_parent_id=record.structure_type_parent_id,
                )
        missing = {s.parent_id for s in known.values() if s.parent_id and s.parent_id not in known}
        while missing:
            fresh = {}
            for structure_id in missing:
                held = self._structures.get((base, structure_id))
                if held is not None and now - held[0] < STRUCTURE_SECONDS:
                    fresh[structure_id] = held[1]
            unread = sorted(missing - set(fresh))
            if unread:
                for structure in await self._run(self._odata.structures, base, unread):
                    fresh[structure.id] = structure
                    self._structures[(base, structure.id)] = (now, structure)
            known.update(fresh)
            # A parent OData does not return ends the walk there, as an unknown structure does in the provider.
            missing = {s.parent_id for s in fresh.values() if s.parent_id and s.parent_id not in known}
        return [known[i] for i in sorted(known)]

    async def prepare(self, start: "pb.LocationStart") -> PreparedLocation:
        """The request for ``start``.

        Raises:
            LocationError: The context, a location or the volume's description cannot be found or read, or the
                provider refuses the annotations.
        """
        target = start.WhichOneof("target")
        ids = [start.location_id] if target == "location_id" else list(start.locations.ids) if target == "locations" else []
        if not ids or any(i <= 0 for i in ids):
            raise LocationError(grpc.StatusCode.INVALID_ARGUMENT, "LocationStart needs a location_id or locations.")
        if start.HasField("shape_override") and len(ids) != 1:
            raise LocationError(grpc.StatusCode.INVALID_ARGUMENT, "shape_override needs a single location_id.")

        found = await self._volumes.context_volume(int(start.annotation_context_id))
        if found is None:
            raise LocationError(
                grpc.StatusCode.NOT_FOUND, f"Annotation context {start.annotation_context_id} is not assigned to this server."
            )
        context, volume = found
        if volume is None:
            raise LocationError(
                grpc.StatusCode.FAILED_PRECONDITION, f"The VikingXML of {context.name or context.annotation_context_id} cannot be read."
            )
        base = odata_base(volume, self._environ)
        if not base:
            raise LocationError(
                grpc.StatusCode.FAILED_PRECONDITION, f"No OData root for {volume.name}: set SEGMENTATION_ODATA_TEMPLATE."
            )

        try:
            records = await self._run(self._odata.locations, base, ids)
        except Exception as e:
            raise LocationError(grpc.StatusCode.UNAVAILABLE, f"The annotation service of {volume.name} did not answer: {e}") from e
        missing = sorted(set(ids) - {r.id for r in records})
        if missing:
            raise LocationError(grpc.StatusCode.NOT_FOUND, f"{volume.name} has no location {', '.join(map(str, missing))}.")
        sections = {r.z for r in records}
        if len(sections) != 1:
            raise LocationError(grpc.StatusCode.INVALID_ARGUMENT, "The locations of one request must share a section.")
        z = sections.pop()

        section = volume.sections.get(z)
        tileset = volume.tileset_for(section) if section is not None else None
        readable = tileset is not None and 1 in tileset.levels
        tileset_name = tileset.name if tileset is not None else volume.default_tileset
        if not tileset_name:
            raise LocationError(grpc.StatusCode.FAILED_PRECONDITION, f"Section {z} of {volume.name} has no tileset.")

        options = start.options
        prompt_input = prompt_pb.PromptInput(
            volume=volume.name, section=z, tileset=tileset_name, annotation_context_id=int(start.annotation_context_id),
            request_id=ids[0], refresh=options.cache_mode == pb.CACHE_MODE_REFRESH, model_id=self._model_id,
        )
        if options.HasField("mask_threshold"):
            prompt_input.mask_threshold = options.mask_threshold
        if options.HasField("job_id"):
            prompt_input.job_id = options.job_id
        targets = [record.annotation() for record in records]
        if start.HasField("shape_override"):
            override = start.shape_override
            if override.WhichOneof("kind") != "circle" or override.circle.radius <= 0:
                raise LocationError(grpc.StatusCode.INVALID_ARGUMENT, "shape_override must be a circle with a radius.")
            # In the location's place, so its id still keeps the location itself out of the background.
            targets[0].type_code, targets[0].mosaic_wkt = 1, ""
            targets[0].x, targets[0].y = override.circle.center.x, override.circle.center.y
            targets[0].radius = override.circle.radius
        prompt_input.targets.extend(targets)

        try:
            reach = (await self._run(self._provider.reach, prompt_input)).reach
            try:
                neighbours = await self._section(base, z)
            except Exception as e:
                raise LocationError(grpc.StatusCode.UNAVAILABLE, f"The annotation service of {volume.name} did not answer: {e}") from e
            candidates = [r for r in neighbours if (b := r.bounds()) is not None and _meets(b, reach)]
            prompt_input.candidates.extend(r.annotation() for r in candidates)
            prompt_input.structures.extend(await self._structures_for(base, [*records, *candidates]))
            request = await self._run(self._provider.build_start, prompt_input)
        except PromptRejected as e:
            raise LocationError(grpc.StatusCode.INVALID_ARGUMENT, f"The prompt provider refused the location: {e}") from e
        except PromptTooLarge as e:
            raise LocationError(grpc.StatusCode.RESOURCE_EXHAUSTED, str(e)) from e
        if options.cache_mode == pb.CACHE_MODE_BYPASS or start.HasField("shape_override"):
            # Without locations the walk neither reads nor stores a cached result; a shape override is not the
            # location's own prompt, so it is never stored as the location's result.
            del request.locations[:]
        return PreparedLocation(request=request, prompt_input=prompt_input, readable=readable)

    async def background(
        self, prepared: PreparedLocation, cells: Sequence[Any], sent_ids: List[int]
    ) -> List[Any]:
        """The background shapes for ``cells`` not yet sent; ``sent_ids`` grows by the ones returned."""
        needed = pb.TilesNeeded(background_cells=cells)
        answer = await self._run(self._provider.background_for, prepared.prompt_input, needed, list(sent_ids))
        sent_ids.extend(answer.sent_ids)
        return list(answer.shapes)
