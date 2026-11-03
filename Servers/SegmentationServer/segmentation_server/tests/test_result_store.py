"""The result cache: finished segmentations kept by location, answered again without tiles or the model."""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pytest

from protocol import cache as _cache
from protocol import circle, context, converse, grid, polygon, put_tile, result, servicer, start
from segmentation_grpc import (
    CreateJobRequest,
    DeleteJobRequest,
    ListJobsRequest,
    LocationRef,
    PutReviewedOutlinesRequest,
    ReviewedOutline,
    SegmentTilesRequest,
)
from segmentation_server.result_store import ResultStore, cache_key, location_key


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
        lambda r: r.__setattr__("mask_threshold", 0.3),
    ],
)
def test_anything_that_changes_the_answer_changes_the_key(change) -> None:
    base = _request()
    changed = _request()
    change(changed)
    assert _key(base) != _key(changed)


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
    store.store("RC1", key, b"answer")
    hit = store.lookup("RC1", key)
    assert hit is not None and hit.response == b"answer"
    assert store.lookup("RC2", key) is None, "volumes do not share rows"


def test_store_replaces_the_row_for_a_key(tmp_path: Path) -> None:
    store = ResultStore(tmp_path)
    key = _key(_request())
    store.store("RC1", key, b"first")
    store.store("RC1", key, b"second")
    assert store.lookup("RC1", key).response == b"second"


def test_eviction_drops_unlinked_rows_least_recently_hit_first(tmp_path: Path) -> None:
    store = ResultStore(tmp_path, max_bytes=250)
    keys = [_key(_request(locations=_refs((i, 1)))) for i in range(1, 5)]
    job = store.create_job("RC1", "Convert structure 1", "james", "", "convert")
    store.store("RC1", keys[0], b"x" * 100, job_id=job.job_id)
    for key in keys[1:]:
        store.store("RC1", key, b"x" * 100)
    store.lookup("RC1", keys[1])  # the most recent hit of the unlinked rows

    store.evict("RC1")

    assert store.lookup("RC1", keys[0]) is not None, "a row linked to a job is never evicted"
    assert store.lookup("RC1", keys[1]) is not None
    assert store.lookup("RC1", keys[2]) is None
    assert store.lookup("RC1", keys[3]) is None


def test_deleting_a_job_makes_its_rows_evictable(tmp_path: Path) -> None:
    store = ResultStore(tmp_path, max_bytes=50)
    key = _key(_request())
    job = store.create_job("RC1", "t", "james", "", "convert")
    store.store("RC1", key, b"x" * 100, job_id=job.job_id)
    store.evict("RC1")
    assert store.lookup("RC1", key) is not None
    assert [row.linked for row in store.list_jobs("RC1")] == [1]

    assert store.delete_job("RC1", job.job_id)
    store.evict("RC1")
    assert store.lookup("RC1", key) is None
    assert not store.delete_job("RC1", job.job_id)


def test_a_reviewed_outline_is_valid_only_until_the_location_is_edited(tmp_path: Path) -> None:
    from segmentation_server.result_store import StoredOutline

    store = ResultStore(tmp_path, max_bytes=1)
    store.put_reviewed("RC1", [StoredOutline(7, 100, b"shape", "retraced", 5, "james")], [])
    store.evict("RC1")
    assert store.reviewed("RC1", 7, 100).state == "retraced"
    assert store.reviewed("RC1", 7, 101) is None
    assert store.put_reviewed("RC1", [], [7]) == (0, 1)
    assert store.reviewed("RC1", 7, 100) is None


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


@pytest.mark.asyncio
async def test_a_reviewed_outline_rides_along_with_its_location(tmp_path: Path) -> None:
    target = await _cached_servicer(tmp_path)
    outline = polygon([(0, 0), (10, 0), (10, 10)])
    response = await target.PutReviewedOutlines(
        PutReviewedOutlinesRequest(
            volume=grid().volume,
            outlines=[ReviewedOutline(location_id=7, last_modified_ticks=100, outline=outline, state="retraced")],
        ),
        context(),
    )
    assert response.stored == 1

    fresh = result(await converse(target, _request()))
    cached = result(await converse(target, _request()))
    edited = result(await converse(target, _request(locations=_refs((7, 101)))))

    assert fresh.reviewed.state == "retraced" and fresh.reviewed.outline == outline
    assert cached.HasField("cache") and cached.reviewed.state == "retraced"
    assert not edited.HasField("reviewed"), "an edit since the review hides the outline"


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
