"""Which grid tiles a stream has already asked its client for.

Two segmentation sessions that grow into the same unseen cell would each ask their own client to
render, encode and upload it. The second upload is wasted work: the tile lands in the shared
image cache under one key whoever sends it. The registry lets the second stream notice that a
request for the tile is already in flight and wait for that upload instead.

Everything here runs on the event loop thread, so there is no locking. A requester is any
hashable token that names one stream; the registry never looks inside it.
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass, field
from typing import Dict, Hashable, List, Optional, Set, Tuple

from segmentation_server.image_cache import TileCacheKey

@dataclass
class _Flight:
    """One tile with at least one stream asking its client for it, and the streams waiting on that."""

    requesters: Set[Hashable] = field(default_factory=set)
    waiters: List[Tuple[Hashable, "asyncio.Future[bool]"]] = field(default_factory=list)


class TileRequestRegistry:
    """Tracks tiles that some stream has asked for and the cache has not received yet.

    A subscription is a future that resolves ``True`` when the tile reaches the cache
    (:meth:`arrived`) and ``False`` when every stream that was asking gives up
    (:meth:`release`, :meth:`release_all`). A subscriber that sees ``False``, or whose own timeout
    passes first, asks its own client; it never waits on a second subscription for the same tile.
    """

    def __init__(self) -> None:
        self._flights: Dict[TileCacheKey, _Flight] = {}
        self._held: Dict[Hashable, Set[TileCacheKey]] = {}

    def __len__(self) -> int:
        return len(self._flights)

    def in_flight(self, key: TileCacheKey) -> bool:
        """True while some stream has asked for ``key`` and it has not arrived or been given up."""
        flight = self._flights.get(key)
        return flight is not None and bool(flight.requesters)

    def waiter_count(self, key: TileCacheKey) -> int:
        """How many streams are subscribed to ``key`` right now (for logs and tests)."""
        flight = self._flights.get(key)
        return 0 if flight is None else sum(1 for _owner, sub in flight.waiters if not sub.done())

    def claim(self, key: TileCacheKey, requester: Hashable) -> Optional["asyncio.Future[bool]"]:
        """Ask for ``key``, or find out that someone already did.

        Returns None when nobody else is asking: ``requester`` is now the one asking and must
        request the tile from its own client. Returns a subscription when another stream is
        asking: ``requester`` should wait on it instead. Must be called on the event loop.
        """
        flight = self._flights.get(key)
        if flight is None or not flight.requesters or requester in flight.requesters:
            self.join(key, requester)
            return None
        subscription: "asyncio.Future[bool]" = asyncio.get_running_loop().create_future()
        flight.waiters.append((requester, subscription))
        return subscription

    def join(self, key: TileCacheKey, requester: Hashable) -> None:
        """Record that ``requester`` is asking its own client for ``key`` (after a timeout, or when sharing is off)."""
        self._flights.setdefault(key, _Flight()).requesters.add(requester)
        self._held.setdefault(requester, set()).add(key)

    def arrived(self, key: TileCacheKey) -> int:
        """The tile is in the cache. Wakes every subscriber and ends the flight.

        Returns how many subscribers were woken. Safe to call for a tile nobody asked for.
        """
        flight = self._flights.pop(key, None)
        if flight is None:
            return 0
        for requester in flight.requesters:
            self._forget(requester, key)
        woken = 0
        for _requester, subscription in flight.waiters:
            if not subscription.done():
                subscription.set_result(True)
                woken += 1
        return woken

    def release(self, key: TileCacheKey, requester: Hashable) -> None:
        """``requester`` is no longer asking for ``key`` (its client said unavailable, or the stream ended).

        When nobody else is asking either, the flight ends and subscribers are told ``False``.
        """
        flight = self._flights.get(key)
        if flight is None:
            return
        flight.requesters.discard(requester)
        self._forget(requester, key)
        if flight.requesters:
            return
        del self._flights[key]
        for _requester, subscription in flight.waiters:
            if not subscription.done():
                subscription.set_result(False)

    def abandon(self, key: TileCacheKey, subscription: "asyncio.Future[bool]") -> None:
        """A subscriber stopped waiting (its timeout passed). The flight carries on without it."""
        flight = self._flights.get(key)
        if flight is not None:
            flight.waiters = [item for item in flight.waiters if item[1] is not subscription]
        if not subscription.done():
            subscription.cancel()

    def release_all(self, requester: Hashable) -> None:
        """Drop everything ``requester`` holds or waits on. Call when its stream ends, however it ends."""
        for key in list(self._held.get(requester, ())):
            self.release(key, requester)
        self._held.pop(requester, None)
        for flight in self._flights.values():
            kept: List[Tuple[Hashable, "asyncio.Future[bool]"]] = []
            for owner, subscription in flight.waiters:
                if owner == requester:
                    if not subscription.done():
                        subscription.cancel()
                else:
                    kept.append((owner, subscription))
            flight.waiters = kept

    def _forget(self, requester: Hashable, key: TileCacheKey) -> None:
        held = self._held.get(requester)
        if held is None:
            return
        held.discard(key)
        if not held:
            del self._held[requester]
