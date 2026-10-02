"""Remember SAM2 cell results for one prompt set and reuse them on the next SegmentTiles call.

Growth is deterministic: the same clicks and the same predictions give the same walk. A
later call for the same clicks, for example after the client uploads a tile the walk asked
for, therefore replays the earlier cells from this cache and only calls SAM2 for cells it
has not seen. The cache also remembers which cells a session visited so the server can
pin the aligned tiles those cells need, even when the client omits them from the call.

Host RAM for cached results is capped. When a store would pass the cap, the
least-recently-used prompt session is dropped until it fits. Evicting a GPU embedding does
not drop a result; a walk that needs a new SAM2 call on a cell whose tiles are gone asks
the client for them again.
"""

from __future__ import annotations

import threading
from dataclasses import dataclass, field
from typing import Callable, Iterable, Mapping, Optional, Sequence, Set, Tuple

import numpy as np

from segmentation_server.cell_grid import Cell, TileIndex, tiles_for_cell
from segmentation_server.tile_growth import CellPredict, GrowthResult, grow_segmentation

MASK_MEMORY_CAP_BYTES = 2 * 1024 * 1024 * 1024

Point = Tuple[int, int]
# volume, section, channel, transform, downsample, multimask, foreground, background
SessionKey = Tuple[str, int, str, str, int, bool, Tuple[Point, ...], Tuple[Point, ...]]
_PredictKey = Tuple[int, int, Tuple[Point, ...], Tuple[int, ...]]
_StoredPredict = Tuple[np.ndarray, Optional[np.ndarray], float]


def prompt_key(
    volume: str,
    section: int,
    channel: str,
    transform: str,
    downsample: int,
    multimask: bool,
    foreground: Sequence[Point],
    background: Sequence[Point],
) -> SessionKey:
    """Identity of one click set. Point order does not matter."""
    return (
        volume,
        int(section),
        channel,
        transform,
        int(downsample),
        bool(multimask),
        tuple(sorted((int(x), int(y)) for x, y in foreground)),
        tuple(sorted((int(x), int(y)) for x, y in background)),
    )


@dataclass
class RememberedGrowth:
    """One walk plus how many SAM2 calls were served from the cache."""

    result: GrowthResult
    reused: int
    predicted: int


@dataclass
class _Session:
    predicts: dict[_PredictKey, _StoredPredict] = field(default_factory=dict)
    cores: dict[Cell, np.ndarray] = field(default_factory=dict)
    held: Set[TileIndex] = field(default_factory=set)
    nbytes: int = 0
    last_used: int = 0


class GrowthMemory:
    """LRU prompt sessions of SAM2 cell results, capped in host RAM.

    Arrays are copied on the way in and the way out so a caller cannot alias a cached
    mask. ``invalidate_tile`` drops every session's results for cells built from a tile
    whose bytes were replaced.
    """

    def __init__(self, max_bytes: int = MASK_MEMORY_CAP_BYTES) -> None:
        self._max_bytes = max_bytes
        self._sessions: dict[SessionKey, _Session] = {}
        self._clock = 0
        self._lock = threading.Lock()

    def tile_indexes(self, key: SessionKey) -> list[TileIndex]:
        """Aligned tiles this prompt set used: those held in earlier calls and those its cells need.

        The client sends only the tiles for the new round, so a cell deferred for one missing
        tile would otherwise lose the tiles it already had. Empty for a new session.
        """
        with self._lock:
            session = self._sessions.get(key)
            if session is None:
                return []
            self._touch(session)
            needed: dict[TileIndex, None] = {}
            for tile in sorted(session.held, key=lambda t: (t.row, t.col)):
                needed[tile] = None
            for cell in sorted(session.cores, key=lambda c: (c.row, c.col)):
                for tile in tiles_for_cell(cell):
                    needed[tile] = None
            return list(needed)

    def remember_held(self, key: SessionKey, tiles: Iterable[TileIndex]) -> None:
        """Add aligned tiles that were pinned for this prompt set. Tiles are indexes, not bytes."""
        with self._lock:
            session = self._session(key)
            session.held.update(tiles)
            self._touch(session)

    def lookup(
        self,
        key: SessionKey,
        row: int,
        col: int,
        points: Sequence[Point],
        labels: Sequence[int],
    ) -> Optional[_StoredPredict]:
        """Cached SAM2 result for these prompts, or None. Arrays are copies."""
        predict_key = _predict_key(row, col, points, labels)
        with self._lock:
            session = self._sessions.get(key)
            if session is None:
                return None
            stored = session.predicts.get(predict_key)
            if stored is None:
                return None
            self._touch(session)
            mask, logits, score = stored
            logits_copy = None if logits is None else np.array(logits, copy=True)
            return np.array(mask, copy=True), logits_copy, score

    def store_predict(
        self,
        key: SessionKey,
        row: int,
        col: int,
        points: Sequence[Point],
        labels: Sequence[int],
        mask: np.ndarray,
        logits: Optional[np.ndarray],
        score: float,
    ) -> None:
        """Remember one SAM2 result. Other sessions are evicted if the cap is passed."""
        predict_key = _predict_key(row, col, points, labels)
        logits_copy = None if logits is None else np.array(logits, copy=True)
        stored = (np.array(mask, copy=True), logits_copy, float(score))
        with self._lock:
            session = self._session(key)
            session.predicts[predict_key] = stored
            self._recount(session)
            self._touch(session)
            self._evict(protect=key)
            if self._total_bytes() > self._max_bytes and key in self._sessions:
                self._drop(key)

    def cores(self, key: SessionKey) -> dict[Cell, np.ndarray]:
        """Copies of the core masks (Y-down, 512x512) the last call for these clicks produced."""
        with self._lock:
            session = self._sessions.get(key)
            if session is None:
                return {}
            self._touch(session)
            return {cell: np.array(core, copy=True) for cell, core in session.cores.items()}

    def remember_cores(self, key: SessionKey, cores: Mapping[Cell, np.ndarray]) -> None:
        """Replace this session's core masks with copies of ``cores``.

        A later call for the same clicks starts from them, so the mask never shrinks between
        calls. A session that is over the cap by itself is dropped.
        """
        copied = {cell: np.array(core, dtype=np.bool_, copy=True) for cell, core in cores.items()}
        with self._lock:
            session = self._session(key)
            session.cores = copied
            self._recount(session)
            self._touch(session)
            self._evict(protect=key)
            if self._total_bytes() > self._max_bytes and key in self._sessions:
                self._drop(key)

    def invalidate_tile(
        self,
        volume: str,
        section: int,
        channel: str,
        transform: str,
        downsample: int,
        row: int,
        col: int,
    ) -> None:
        """Drop cached results and visited cells that were built from one replaced tile."""
        identity = (volume, int(section), channel, transform, int(downsample))
        tile = TileIndex(row=int(row), col=int(col))
        with self._lock:
            empty: list[SessionKey] = []
            for key, session in self._sessions.items():
                if key[:5] != identity:
                    continue
                stale = [
                    item
                    for item in session.predicts
                    if tile in tiles_for_cell(Cell(row=item[0], col=item[1]))
                ]
                for item in stale:
                    del session.predicts[item]
                session.cores = {
                    cell: core
                    for cell, core in session.cores.items()
                    if tile not in tiles_for_cell(cell)
                }
                session.held.discard(tile)
                self._recount(session)
                if not session.predicts and not session.cores and not session.held:
                    empty.append(key)
            for key in empty:
                self._drop(key)

    def _session(self, key: SessionKey) -> _Session:
        session = self._sessions.get(key)
        if session is None:
            session = _Session()
            self._sessions[key] = session
        return session

    def _touch(self, session: _Session) -> None:
        self._clock += 1
        session.last_used = self._clock

    def _total_bytes(self) -> int:
        return sum(session.nbytes for session in self._sessions.values())

    def _recount(self, session: _Session) -> None:
        total = 0
        for mask, logits, _score in session.predicts.values():
            total += _nbytes(mask) + _nbytes(logits)
        for core in session.cores.values():
            total += _nbytes(core)
        session.nbytes = total

    def _least_recent(self, protect: Optional[SessionKey]) -> Optional[SessionKey]:
        victim: Optional[SessionKey] = None
        oldest = 0
        for key, session in self._sessions.items():
            if key == protect:
                continue
            if victim is None or session.last_used < oldest:
                victim = key
                oldest = session.last_used
        return victim

    def _evict(self, protect: Optional[SessionKey]) -> None:
        while self._total_bytes() > self._max_bytes:
            victim = self._least_recent(protect)
            if victim is None:
                return
            self._drop(victim)

    def _drop(self, key: SessionKey) -> None:
        self._sessions.pop(key, None)


def grow_remembering(
    memory: GrowthMemory,
    session: SessionKey,
    foreground: Sequence[Point],
    background: Sequence[Point],
    predict: CellPredict,
    should_stop: Optional[Callable[[], bool]] = None,
    **kwargs: object,
) -> RememberedGrowth:
    """Walk, serving a cell from the cache when its points and labels were already predicted.

    ``predict`` runs only for prompts that are not stored. ``reused`` counts cache hits,
    ``predicted`` counts new SAM2 calls. ``should_stop`` returning true raises
    ``GrowthCancelled`` before the next SAM2 call. Extra keyword arguments go to
    ``grow_segmentation``.
    """
    reused = 0
    fresh = 0

    def caching_predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        nonlocal reused, fresh
        hit = memory.lookup(session, row, col, points, labels)
        if hit is not None:
            reused += 1
            return hit
        mask, logits, score = predict(row, col, points, labels)
        memory.store_predict(session, row, col, points, labels, mask, logits, score)
        fresh += 1
        return mask, logits, score

    result = grow_segmentation(
        foreground,
        background,
        caching_predict,
        should_stop=should_stop,
        remembered=memory.cores(session) or None,
        **kwargs,
    )
    memory.remember_cores(session, {cell: pred.core for cell, pred in result.cells.items()})
    return RememberedGrowth(result=result, reused=reused, predicted=fresh)


def _predict_key(
    row: int,
    col: int,
    points: Sequence[Point],
    labels: Sequence[int],
) -> _PredictKey:
    return (
        int(row),
        int(col),
        tuple((int(x), int(y)) for x, y in points),
        tuple(int(label) for label in labels),
    )


def _nbytes(array: Optional[np.ndarray]) -> int:
    if array is None:
        return 0
    return int(np.asarray(array).nbytes)
