"""TileRequestRegistry: one stream waits for another stream's upload instead of asking again."""

from __future__ import annotations

import pytest

from segmentation_server.tile_requests import TileRequestRegistry

KEY = ("RC2", 1305, "TEM", "SliceToVolume1|", 1, 0, 0)
OTHER = ("RC2", 1305, "TEM", "SliceToVolume1|", 1, 0, 1)


@pytest.mark.asyncio
async def test_the_first_stream_to_ask_asks_and_the_second_subscribes() -> None:
    registry = TileRequestRegistry()

    assert registry.claim(KEY, "a") is None
    subscription = registry.claim(KEY, "b")

    assert subscription is not None and not subscription.done()
    assert registry.in_flight(KEY)
    assert registry.waiter_count(KEY) == 1


@pytest.mark.asyncio
async def test_a_stream_that_already_asks_is_not_told_to_wait_on_itself() -> None:
    registry = TileRequestRegistry()
    assert registry.claim(KEY, "a") is None

    assert registry.claim(KEY, "a") is None


@pytest.mark.asyncio
async def test_arrival_wakes_every_subscriber_with_true_and_ends_the_flight() -> None:
    registry = TileRequestRegistry()
    registry.claim(KEY, "a")
    first = registry.claim(KEY, "b")
    second = registry.claim(KEY, "c")

    assert registry.arrived(KEY) == 2

    assert first.result() is True and second.result() is True
    assert not registry.in_flight(KEY)
    assert len(registry) == 0


@pytest.mark.asyncio
async def test_a_tile_nobody_asked_for_arriving_is_a_no_op() -> None:
    registry = TileRequestRegistry()

    assert registry.arrived(KEY) == 0
    assert len(registry) == 0


@pytest.mark.asyncio
async def test_a_tile_that_arrived_is_asked_for_again_by_the_next_stream() -> None:
    registry = TileRequestRegistry()
    registry.claim(KEY, "a")
    registry.arrived(KEY)

    assert registry.claim(KEY, "b") is None


@pytest.mark.asyncio
async def test_subscribers_are_told_false_only_when_every_asking_stream_gives_up() -> None:
    registry = TileRequestRegistry()
    registry.claim(KEY, "a")
    registry.join(KEY, "b")
    subscription = registry.claim(KEY, "c")

    registry.release(KEY, "a")
    assert not subscription.done()
    assert registry.in_flight(KEY)

    registry.release(KEY, "b")
    assert subscription.result() is False
    assert len(registry) == 0


@pytest.mark.asyncio
async def test_release_all_gives_up_every_tile_the_stream_held_and_drops_its_subscriptions() -> None:
    registry = TileRequestRegistry()
    registry.claim(KEY, "a")
    registry.claim(OTHER, "a")
    waiting_on_a = registry.claim(KEY, "b")
    held_by_b = ("RC2", 1305, "TEM", "SliceToVolume1|", 1, 5, 5)
    registry.claim(held_by_b, "b")
    a_waiting_on_b = registry.claim(held_by_b, "a")

    registry.release_all("a")

    assert waiting_on_a.result() is False
    assert a_waiting_on_b.cancelled()
    assert not registry.in_flight(KEY) and not registry.in_flight(OTHER)
    assert registry.in_flight(held_by_b)
    registry.release_all("b")
    assert len(registry) == 0


@pytest.mark.asyncio
async def test_an_abandoned_subscription_is_not_woken_and_the_flight_continues() -> None:
    registry = TileRequestRegistry()
    registry.claim(KEY, "a")
    subscription = registry.claim(KEY, "b")

    registry.abandon(KEY, subscription)

    assert subscription.cancelled()
    assert registry.waiter_count(KEY) == 0
    assert registry.in_flight(KEY)
    assert registry.arrived(KEY) == 0


@pytest.mark.asyncio
async def test_a_stream_that_timed_out_can_join_the_flight_it_waited_on() -> None:
    registry = TileRequestRegistry()
    registry.claim(KEY, "a")
    subscription = registry.claim(KEY, "b")
    registry.abandon(KEY, subscription)

    registry.join(KEY, "b")
    registry.release(KEY, "a")

    assert registry.in_flight(KEY)
    registry.release(KEY, "b")
    assert len(registry) == 0
