"""The result cache: finished segmentations kept by location, answered again without tiles or the model."""

from __future__ import annotations

import sqlite3
from contextlib import contextmanager
from pathlib import Path
from types import SimpleNamespace
from typing import Iterator

import grpc
import numpy as np
import pytest

from protocol import cache as _cache
from protocol import circle, context, converse, grid, polygon, put_tile, result, servicer, start
from segmentation_grpc import (
    CreateJobRequest,
    DeleteJobRequest,
    ForgetLocationsRequest,
    ListJobsRequest,
    LocationRef,
    ManualTrace,
    PutManualTracesRequest,
    SegmentTilesRequest,
)
from segmentation_server.result_store import ResultStore, StoredTrace, cache_key, location_key


def _refs(*pairs):
    return [LocationRef(id=location_id, last_modified_ticks=lm) for location_id, lm in pairs]


def _request(**fields) -> SegmentTilesRequest:
    request = start([(512, 512)], shapes=[circle(512, 512, 40)], **fields)
    if not request.locations:
        request.locations.extend(_refs((7, 100)))
    return request


def _key(request: SegmentTilesRequest, model: str = "v5.4", server: str = "1.0"):
    return cache_key(request, "-", model, server)


# -- the key -----------------------------------------------------------------------------------------------------------


def test_one_location_is_its_plain_id_and_a_group_is_a_hash_of_the_sorted_ids() -> None:
    assert location_key(_refs((7, 100))) == ("7", 100)
    forward, lm = location_key(_refs((3, 10), (9, 20)))
    backward, _ = location_key(_refs((9, 20), (3, 10)))
    assert forward == backward
    assert forward.startswith("h:") and lm == 0
    assert location_key(_refs((3, 11), (9, 20)))[0] != forward, "a member's edit changes the group key"


def test_no_locations_means_nothing_is_cached() -> None:
    assert cache_key(start([(1, 1)]), "-", "v5.4", "1.0") is None


def test_the_key_ignores_click_order_and_float_noise() -> None:
    a = start([(10.0, 10.0), (30.0, 30.0)], locations=_refs((7, 1)))
    b = start([(30.0001, 30.0), (10.0, 9.9999)], locations=_refs((7, 1)))
    assert _key(a) == _key(b)


def test_a_polygon_key_ignores_where_the_ring_starts_and_which_way_it_turns() -> None:
    square = [(0, 0), (100, 0), (100, 100), (0, 100)]
    turned = [(100, 100), (100, 0), (0, 0), (0, 100)]
    a = start(shapes=[polygon(square)], locations=_refs((7, 1)))
    b = start(shapes=[polygon(turned)], locations=_refs((7, 1)))
    assert _key(a) == _key(b)


@pytest.mark.parametrize(
    "change",
    [
        lambda r: r.locations[0].__setattr__("last_modified_ticks", 101),
        lambda r: r.neighbors.extend(_refs((8, 5))),
        lambda r: r.grid.__setattr__("downsample", 4),
        lambda r: r.grid.__setattr__("transform", "other|"),
        lambda r: r.grid.__setattr__("space", 1 if r.grid.space != 1 else 2),
        lambda r: r.foreground_feature.shapes[0].circle.__setattr__("radius", 60),
    ],
)
def test_anything_that_changes_the_answer_changes_the_key(change) -> None:
    base = _request()
    changed = _request()
    change(changed)
    assert _key(base) != _key(changed)


def test_the_threshold_is_not_part_of_the_key() -> None:
    """A row records the threshold it grew at instead, and answers every threshold at or above it."""
    assert _key(_request(mask_threshold=0.3)) == _key(_request(mask_threshold=1.5)) == _key(_request())


def test_another_model_or_server_version_is_another_key() -> None:
    request = _request()
    assert _key(request) != _key(request, model="v5.5")
    assert _key(request) != _key(request, server="1.1")


def test_listed_neighbours_stand_for_their_shapes() -> None:
    """Two clients that map a neighbour slightly differently agree when they list it."""
    a = _request(neighbors=_refs((8, 5)), background_shapes=[circle(100, 100, 20)])
    b = _request(neighbors=_refs((8, 5)), background_shapes=[circle(103, 100, 20)])
    assert _key(a) == _key(b)


# -- the store ---------------------------------------------------------------------------------------------------------


def test_a_stored_answer_is_found_again(tmp_path: Path) -> None:
    store = ResultStore(tmp_path)
    key = _key(_request())
    store.store("RC1", key, b"answer", 0.0)
    hit = store.lookup("RC1", key, 0.0)
    assert hit is not None and hit.response == b"answer" and hit.growth_threshold == 0.0
    assert store.lookup("RC2", key, 0.0) is None, "volumes do not share rows"


def test_a_row_answers_its_threshold_and_any_higher_one_only(tmp_path: Path) -> None:
    store = ResultStore(tmp_path)
    key = _key(_request())
    store.store("RC1", key, b"grown at 0.5", 0.5)
    assert store.lookup("RC1", key, 0.5) is not None
    assert store.lookup("RC1", key, 2.0).growth_threshold == 0.5
    assert store.lookup("RC1", key, 0.25) is None, "a lower threshold may reach past where the walk stopped"


def test_a_lower_threshold_replaces_the_row_and_a_higher_one_never_does(tmp_path: Path) -> None:
    store = ResultStore(tmp_path)
    key = _key(_request())
    store.store("RC1", key, b"first", 0.5)
    store.store("RC1", key, b"lower", 0.0)
    store.store("RC1", key, b"higher", 1.0)
    hit = store.lookup("RC1", key, 1.0)
    assert hit.response == b"lower" and hit.growth_threshold == 0.0


def test_eviction_drops_unlinked_rows_least_recently_hit_first(tmp_path: Path) -> None:
    store = ResultStore(tmp_path, max_bytes=250)
    keys = [_key(_request(locations=_refs((i, 1)))) for i in range(1, 5)]
    job = store.create_job("RC1", "Convert structure 1", "james", "", "convert")
    store.store("RC1", keys[0], b"x" * 100, 0.0, job_id=job.job_id)
    for key in keys[1:]:
        store.store("RC1", key, b"x" * 100, 0.0)
    store.lookup("RC1", keys[1], 0.0)  # the most recent hit of the unlinked rows

    store.evict("RC1")

    assert store.lookup("RC1", keys[0], 0.0) is not None, "a row linked to a job is never evicted"
    assert store.lookup("RC1", keys[1], 0.0) is not None
    assert store.lookup("RC1", keys[2], 0.0) is None
    assert store.lookup("RC1", keys[3], 0.0) is None


def test_deleting_a_job_makes_its_rows_evictable(tmp_path: Path) -> None:
    store = ResultStore(tmp_path, max_bytes=50)
    key = _key(_request())
    job = store.create_job("RC1", "t", "james", "", "convert")
    store.store("RC1", key, b"x" * 100, 0.0, job_id=job.job_id)
    store.evict("RC1")
    assert store.lookup("RC1", key, 0.0) is not None
    assert [row.linked for row in store.list_jobs("RC1")] == [1]

    assert store.delete_job("RC1", job.job_id)
    store.evict("RC1")
    assert store.lookup("RC1", key, 0.0) is None
    assert not store.delete_job("RC1", job.job_id)


def test_a_manual_trace_answers_only_until_the_location_is_edited_and_is_never_evicted(tmp_path: Path) -> None:
    store = ResultStore(tmp_path, max_bytes=1, max_age_days=1e-12)
    store.put_traces("RC1", [StoredTrace(7, 100, b"shape", "james", 5)], [])
    model = _key(_request(locations=_refs((8, 100))))
    store.store("RC1", model, b"x" * 100, 0.0)  # evicts: the store is over both limits
    store.evict("RC1")
    assert store.lookup("RC1", model, 0.0) is None, "the model row goes, the trace stays"

    trace = store.trace("RC1", 7, 100)
    assert (trace.outline, trace.tracer, trace.traced_ticks) == (b"shape", "james", 5)
    assert store.trace("RC1", 7, 101) is None
    assert store.lookup("RC1", _key(_request()), 0.0) is None, "a trace is never a model answer"
    assert store.put_traces("RC1", [], [7]) == (0, 1)
    assert store.trace("RC1", 7, 100) is None


def test_a_new_trace_replaces_the_old_one(tmp_path: Path) -> None:
    store = ResultStore(tmp_path)
    store.put_traces("RC1", [StoredTrace(7, 100, b"old", "a", 5)], [])
    store.put_traces("RC1", [StoredTrace(7, 200, b"new", "b", 6)], [])
    assert store.trace("RC1", 7, 100) is None
    assert store.trace("RC1", 7, 200).outline == b"new"


def test_forgetting_a_location_drops_its_results_its_groups_and_its_trace(tmp_path: Path) -> None:
    store = ResultStore(tmp_path)
    own, group, other = (_key(_request(locations=refs)) for refs in (_refs((7, 100)), _refs((7, 100), (8, 1)), _refs((9, 1))))
    for key in (own, group, other):
        store.store("RC1", key, b"x", 0.0)
    store.put_traces("RC1", [StoredTrace(7, 100, b"shape", "james", 5)], [])

    assert store.forget("RC1", [7]) == 3
    assert store.lookup("RC1", own, 0.0) is None and store.lookup("RC1", group, 0.0) is None
    assert store.trace("RC1", 7, 100) is None
    assert store.lookup("RC1", other, 0.0) is not None
    assert store.forget("RC1", [7]) == 0


def test_a_retraced_reviewed_outline_becomes_a_manual_trace(tmp_path: Path) -> None:
    """A file from before manual traces: its retraced outline is kept as a trace, the approved one is dropped."""
    with sqlite3.connect(str(tmp_path / "RC1.sqlite")) as connection:
        connection.executescript(
            """
            CREATE TABLE segmentation (
                location_key TEXT NOT NULL, location_lm INTEGER NOT NULL, model_version TEXT NOT NULL,
                server_version TEXT NOT NULL, space INTEGER NOT NULL, transform TEXT NOT NULL, channel TEXT NOT NULL,
                downsample INTEGER NOT NULL, context_hash TEXT NOT NULL, prompt_hash TEXT NOT NULL,
                growth_threshold REAL NOT NULL, created_ticks INTEGER NOT NULL, last_hit_ticks INTEGER NOT NULL,
                response BLOB NOT NULL,
                PRIMARY KEY (location_key, location_lm, model_version, server_version, space, transform, channel,
                             downsample, context_hash, prompt_hash));
            CREATE TABLE reviewed_outline (
                location_id INTEGER PRIMARY KEY, last_modified_ticks INTEGER NOT NULL, outline BLOB NOT NULL,
                state TEXT NOT NULL, reviewed_ticks INTEGER NOT NULL, reviewer TEXT NOT NULL);
            INSERT INTO reviewed_outline VALUES (7, 100, X'01', 'retraced', 5, 'james');
            INSERT INTO reviewed_outline VALUES (8, 100, X'02', 'approved', 5, 'james');
            """
        )

    store = ResultStore(tmp_path)

    assert store.trace("RC1", 7, 100) == StoredTrace(7, 100, b"\x01", "james", 5)
    assert store.trace("RC1", 8, 100) is None
    with sqlite3.connect(str(tmp_path / "RC1.sqlite")) as connection:
        tables = {row[0] for row in connection.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    assert "reviewed_outline" not in tables


# -- the stream --------------------------------------------------------------------------------------------------------


def _square_logits():
    mask = np.zeros((1024, 1024), dtype=np.bool_)
    mask[400:624, 400:624] = True
    logits = np.full((256, 256), -5.0, dtype=np.float32)
    logits[100:156, 100:156] = 5.0
    return mask, logits


async def _cached_servicer(tmp_path: Path):
    image_cache = _cache()
    await put_tile(image_cache, 0, 0)
    mask, logits = _square_logits()
    return servicer(image_cache, mask=mask, logits=logits, result_store=ResultStore(tmp_path))


@pytest.mark.asyncio
async def test_a_second_request_for_the_same_location_is_answered_from_the_cache(tmp_path: Path) -> None:
    target = await _cached_servicer(tmp_path)
    first = result(await converse(target, _request(request_id=1)))
    calls = target.model.predict_ephemeral.call_count + target.model.predict_tile.call_count
    assert not first.HasField("cache")

    updates = await converse(target, _request(request_id=2))

    assert [update.WhichOneof("update") for update in updates] == ["result"], "no tiles are asked for"
    second = updates[0].result
    assert second.HasField("cache") and second.cache.model_version
    assert second.request_id == 2
    assert list(second.segments) == list(first.segments)
    assert target.model.predict_ephemeral.call_count + target.model.predict_tile.call_count == calls


@pytest.mark.asyncio
async def test_a_higher_threshold_is_answered_by_a_walk_grown_lower_and_a_lower_one_grows_anew(tmp_path: Path) -> None:
    target = await _cached_servicer(tmp_path)

    def predictions() -> int:
        return target.model.predict_ephemeral.call_count + target.model.predict_tile.call_count

    grown = result(await converse(target, _request(mask_threshold=0.5)))
    assert grown.growth_threshold == pytest.approx(0.5) and not grown.HasField("cache")
    calls = predictions()

    higher = result(await converse(target, _request(mask_threshold=2.0)))
    assert higher.HasField("cache") and higher.growth_threshold == pytest.approx(0.5)
    assert list(higher.segments) == list(grown.segments)
    assert predictions() == calls

    lower = result(await converse(target, _request(mask_threshold=-1.0)))
    assert not lower.HasField("cache") and lower.growth_threshold == pytest.approx(-1.0)
    assert predictions() > calls
    calls = predictions()

    again = result(await converse(target, _request(mask_threshold=0.5)))
    assert again.HasField("cache") and again.growth_threshold == pytest.approx(-1.0), "the row moved down"
    assert predictions() == calls


@pytest.mark.asyncio
async def test_a_request_without_a_threshold_grows_at_zero(tmp_path: Path) -> None:
    target = await _cached_servicer(tmp_path)
    response = result(await converse(target, _request()))
    assert response.HasField("growth_threshold") and response.growth_threshold == 0.0


@pytest.mark.asyncio
async def test_refresh_segments_anew(tmp_path: Path) -> None:
    target = await _cached_servicer(tmp_path)
    await converse(target, _request())
    calls = target.model.predict_ephemeral.call_count + target.model.predict_tile.call_count

    response = result(await converse(target, _request(refresh=True)))

    assert not response.HasField("cache")
    assert target.model.predict_ephemeral.call_count + target.model.predict_tile.call_count > calls


@pytest.mark.asyncio
async def test_a_merge_chain_replays_from_the_cache(tmp_path: Path) -> None:
    """a, then {a, b}, then {a, b, c}: the second pass is all cache hits with the same answers."""
    target = await _cached_servicer(tmp_path)
    chain = [_refs((1, 10)), _refs((2, 20), (1, 10)), _refs((3, 30), (1, 10), (2, 20))]
    first = [result(await converse(target, _request(locations=refs))) for refs in chain]
    calls = target.model.predict_ephemeral.call_count + target.model.predict_tile.call_count

    again = [result(await converse(target, _request(locations=list(reversed(refs))))) for refs in chain]

    assert all(response.HasField("cache") for response in again)
    assert [list(r.segments) for r in again] == [list(r.segments) for r in first]
    assert target.model.predict_ephemeral.call_count + target.model.predict_tile.call_count == calls


@contextmanager
def _context_volume(target, volume: str) -> Iterator[None]:
    """Let the servicer resolve annotation context 1 to ``volume`` (as SegmentLocation does) for the cache RPCs."""

    async def context_volume(annotation_context_id: int):
        return (SimpleNamespace(annotation_context_id=1), SimpleNamespace(name=volume)) if annotation_context_id == 1 else None

    kept = target._tile_source
    target._tile_source = SimpleNamespace(context_volume=context_volume)
    try:
        yield
    finally:
        target._tile_source = kept


@pytest.mark.asyncio
async def test_a_manual_trace_is_its_locations_answer_until_the_location_is_edited(tmp_path: Path) -> None:
    target = await _cached_servicer(tmp_path)
    outline = polygon([(0, 0), (10, 0), (10, 10)])
    trace = ManualTrace(location_id=7, last_modified_ticks=100, outline=outline, tracer="james", traced_ticks=5)
    with _context_volume(target, grid().volume):
        response = await target.PutManualTraces(PutManualTracesRequest(annotation_context_id=1, traces=[trace]), context())
    assert response.stored == 1
    calls = target.model.predict_ephemeral.call_count + target.model.predict_tile.call_count

    traced = result(await converse(target, _request(request_id=3, mask_threshold=5.0)))
    edited = result(await converse(target, _request(locations=_refs((7, 101)))))

    assert traced.manual_trace == trace and not traced.segments and traced.request_id == 3
    assert traced.cache.created_ticks == 5 and not traced.HasField("growth_threshold")
    assert not edited.HasField("manual_trace") and edited.segments, "an edit since the trace asks the model"
    assert target.model.predict_ephemeral.call_count + target.model.predict_tile.call_count > calls


@pytest.mark.asyncio
async def test_a_trace_must_be_a_polygon_of_a_known_context(tmp_path: Path) -> None:
    target = await _cached_servicer(tmp_path)
    with _context_volume(target, grid().volume):
        refused = context()
        await target.PutManualTraces(
            PutManualTracesRequest(
                annotation_context_id=1, traces=[ManualTrace(location_id=7, outline=circle(1, 1, 1))]
            ),
            refused,
        )
        unknown = context()
        await target.ForgetLocations(ForgetLocationsRequest(annotation_context_id=2, location_ids=[7]), unknown)
    assert refused.abort.call_args[0][0] == grpc.StatusCode.INVALID_ARGUMENT
    assert unknown.abort.call_args[0][0] == grpc.StatusCode.NOT_FOUND


@pytest.mark.asyncio
async def test_forget_locations_makes_the_next_request_ask_the_model(tmp_path: Path) -> None:
    target = await _cached_servicer(tmp_path)
    await converse(target, _request())
    with _context_volume(target, grid().volume):
        await target.PutManualTraces(
            PutManualTracesRequest(
                annotation_context_id=1,
                traces=[ManualTrace(location_id=7, last_modified_ticks=100, outline=polygon([(0, 0), (9, 0), (9, 9)]))],
            ),
            context(),
        )
        forgotten = await target.ForgetLocations(ForgetLocationsRequest(annotation_context_id=1, location_ids=[7]), context())

    again = result(await converse(target, _request()))

    assert forgotten.removed == 2, "the model's row and the trace"
    assert not again.HasField("cache") and not again.HasField("manual_trace")


@pytest.mark.asyncio
async def test_jobs_over_rpc_protect_what_their_requests_store(tmp_path: Path) -> None:
    target = await _cached_servicer(tmp_path)
    volume = grid().volume
    job = await target.CreateJob(CreateJobRequest(volume=volume, title="Convert", username="james", kind="convert"), context())
    assert job.job_id > 0

    await converse(target, _request(job_id=job.job_id))

    (listed,) = (await target.ListJobs(ListJobsRequest(volume=volume), context())).jobs
    assert listed.title == "Convert" and listed.linked == 1
    assert (await target.DeleteJob(DeleteJobRequest(volume=volume, job_id=job.job_id), context())).deleted


@pytest.mark.asyncio
async def test_without_a_store_nothing_is_cached_and_the_job_rpcs_refuse(tmp_path: Path) -> None:
    image_cache = _cache()
    await put_tile(image_cache, 0, 0)
    mask, logits = _square_logits()
    target = servicer(image_cache, mask=mask, logits=logits)
    target._results = None

    await converse(target, _request())
    second = result(await converse(target, _request()))
    assert not second.HasField("cache")

    ctx = context()
    await target.CreateJob(CreateJobRequest(volume="RC1", title="t"), ctx)
    ctx.abort.assert_awaited()
