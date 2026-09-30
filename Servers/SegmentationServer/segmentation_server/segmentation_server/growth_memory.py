"""Remember tile masks for one prompt set and reuse them on the next SegmentTiles call.

Viking draws only the last response, and that response used to contain only the
cells named in that call. A later call that omits a cell therefore cut the mask
at the tile edge. This store puts those cells back into the composite and skips
SAM2 when the same cell is asked the same points and labels again.

Host RAM for remembered masks and cached SAM2 results together is capped. When
a store would pass the cap, the least-recently-used prompt session is dropped
until it fits. Evicting a GPU embedding does not drop a mask; a later walk that
needs a new SAM2 call on a cell whose embedding is gone asks for that cell again.
"""

from __future__ import annotations

import threading
from dataclasses import dataclass, field
from typing import Callable, Optional, Sequence, Tuple

import numpy as np

from segmentation_server.tile_growth import (
    GrowthCancelled,
    GrowthResult,
    SeamGraph,
    TileIndex,
    TilePredict,
    TilePrediction,
    grow_segmentation,
)

MASK_MEMORY_CAP_BYTES = 2 * 1024 * 1024 * 1024

Point = Tuple[int, int]
# volume, section, channel, transform, downsample, multimask, foreground, background
SessionKey = Tuple[str, int, str, str, int, bool, Tuple[Point, ...], Tuple[Point, ...]]
_PredictKey = Tuple[int, int, Tuple[Point, ...], Tuple[int, ...]]
_SeamKey = Tuple[int, int, int, int, str, Tuple[Point, ...], Tuple[int, ...]]
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
    tiles: dict[TileIndex, TilePrediction] = field(default_factory=dict)
    predicts: dict[_PredictKey, _StoredPredict] = field(default_factory=dict)
    seams: dict[_SeamKey, _StoredPredict] = field(default_factory=dict)
    graph: SeamGraph = field(default_factory=SeamGraph)
    nbytes: int = 0
    last_used: int = 0


class GrowthMemory:
    """LRU prompt sessions of tile masks and SAM2 results, capped in host RAM.

    Arrays are copied on the way in and the way out so an in-walk reseed cannot
    alias a cached mask. ``invalidate_tile`` drops every session's memory of a
    cell whose bytes were replaced.
    """

    def __init__(self, max_bytes: int = MASK_MEMORY_CAP_BYTES) -> None:
        self._max_bytes = max_bytes
        self._sessions: dict[SessionKey, _Session] = {}
        self._clock = 0
        self._lock = threading.Lock()

    def tile_indexes(self, key: SessionKey) -> list[TileIndex]:
        """Cells remembered for this prompt set. Empty when the session is new."""
        with self._lock:
            session = self._sessions.get(key)
            if session is None:
                return []
            self._touch(session)
            return list(session.tiles.keys())

    def tiles(self, key: SessionKey) -> dict[TileIndex, TilePrediction]:
        """Copies of the remembered masks. The caller may mutate the copies."""
        with self._lock:
            session = self._sessions.get(key)
            if session is None:
                return {}
            self._touch(session)
            return {tile: _clone_prediction(pred) for tile, pred in session.tiles.items()}

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

    def lookup_seam(
        self,
        key: SessionKey,
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ) -> Optional[_StoredPredict]:
        """Cached half-tile result for this cut, or None. Arrays are copies."""
        seam_key = _seam_key(src_row, src_col, dst_row, dst_col, side, points, labels)
        with self._lock:
            session = self._sessions.get(key)
            if session is None:
                return None
            stored = session.seams.get(seam_key)
            if stored is None:
                return None
            self._touch(session)
            mask, logits, score = stored
            logits_copy = None if logits is None else np.array(logits, copy=True)
            return np.array(mask, copy=True), logits_copy, score

    def store_seam(
        self,
        key: SessionKey,
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
        mask: np.ndarray,
        logits: Optional[np.ndarray],
        score: float,
    ) -> None:
        """Remember one half-tile result. Other sessions are evicted if the cap is passed."""
        seam_key = _seam_key(src_row, src_col, dst_row, dst_col, side, points, labels)
        logits_copy = None if logits is None else np.array(logits, copy=True)
        stored = (np.array(mask, copy=True), logits_copy, float(score))
        with self._lock:
            session = self._session(key)
            session.seams[seam_key] = stored
            self._recount(session)
            self._touch(session)
            self._evict(protect=key)

    def remember_tiles(self, key: SessionKey, tiles: dict[TileIndex, TilePrediction]) -> None:
        """Replace this session's tile masks with copies of ``tiles``.

        The predict cache is kept. After the replacement, least-recently-used
        sessions are dropped until the store fits. A session that is over the
        cap by itself is dropped so the store does not stay above the cap.
        """
        copied = {tile: _clone_prediction(pred) for tile, pred in tiles.items()}
        with self._lock:
            session = self._session(key)
            session.tiles = copied
            self._recount(session)
            self._touch(session)
            self._evict(protect=key)
            if self._total_bytes() > self._max_bytes and key in self._sessions:
                self._drop(key)

    def seam_graph(self, key: SessionKey) -> SeamGraph:
        """A copy of the crossed borders for this click set. Empty when the session is new.

        The caller mutates the copy during a walk and writes it back with
        ``remember_seam_graph``. The stored graph is left unchanged until then.
        """
        with self._lock:
            session = self._sessions.get(key)
            if session is None:
                return SeamGraph()
            self._touch(session)
            return session.graph.copy()

    def remember_seam_graph(self, key: SessionKey, graph: SeamGraph) -> None:
        """Replace this session's crossed borders with a copy of ``graph``.

        Called by ``grow_remembering`` after a walk so the next SegmentTiles for
        the same clicks skips ranges already resolved. Intervals are not counted
        toward the mask byte cap. If the session was just evicted, the graph is
        not written back on its own.
        """
        with self._lock:
            session = self._sessions.get(key)
            if session is None:
                return
            session.graph = graph.copy()
            self._touch(session)

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
        """Drop remembered masks, cached predicts, and borders that touch one cell."""
        identity = (volume, int(section), channel, transform, int(downsample))
        cell = (int(row), int(col))
        with self._lock:
            empty: list[SessionKey] = []
            for key, session in self._sessions.items():
                if key[:5] != identity:
                    continue
                session.tiles.pop(TileIndex(row=cell[0], col=cell[1]), None)
                stale = [item for item in session.predicts if item[0] == cell[0] and item[1] == cell[1]]
                for item in stale:
                    del session.predicts[item]
                stale_seams = [
                    item
                    for item in session.seams
                    if (item[0], item[1]) == cell or (item[2], item[3]) == cell
                ]
                for item in stale_seams:
                    del session.seams[item]
                session.graph.drop_tile(cell[0], cell[1])
                self._recount(session)
                if not session.tiles and not session.predicts and not session.seams and not session.graph.edges:
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
        for pred in session.tiles.values():
            total += _nbytes(pred.mask) + _nbytes(pred.logits)
        for mask, logits, _score in session.predicts.values():
            total += _nbytes(mask) + _nbytes(logits)
        for mask, logits, _score in session.seams.values():
            total += _nbytes(mask) + _nbytes(logits)
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
    uploaded: Sequence[TileIndex],
    foreground: Sequence[Point],
    background: Sequence[Point],
    predict: TilePredict,
    should_stop: Optional[Callable[[], bool]] = None,
    **kwargs: object,
) -> RememberedGrowth:
    """Walk with remembered tiles filled in, and serve unchanged prompts from the cache.

    Remembered cells are added to the uploaded set, so a call that omits one still
    fuses it. ``predict`` runs only when the points and labels are not already
    stored. ``reused`` counts those cache hits. ``predicted`` counts new SAM2 calls.
    ``should_stop`` returning true raises ``GrowthCancelled`` before the next SAM2 call.
    A ``seam_predict`` argument is cached the same way, keyed by the two cells and the cut.
    The session's seam graph is copied in, updated with the borders this walk resolves,
    and written back so the next call skips an overlapping stretch.
    """
    remembered = memory.tiles(session)
    graph = memory.seam_graph(session)
    uploaded_set = list(uploaded)
    seen = set(uploaded_set)
    for tile in remembered:
        if tile not in seen:
            uploaded_set.append(tile)
            seen.add(tile)
    reused = 0
    fresh = 0
    raw_seam = kwargs.pop("seam_predict", None)

    def caching_predict(
        row: int,
        col: int,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        nonlocal reused, fresh
        hit = memory.lookup(session, row, col, points, labels)
        if hit is not None:
            reused += 1
            return hit
        mask, logits, score = predict(row, col, points, labels)
        memory.store_predict(session, row, col, points, labels, mask, logits, score)
        fresh += 1
        return mask, logits, score

    def caching_seam(
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        nonlocal reused, fresh
        hit = memory.lookup_seam(
            session, src_row, src_col, dst_row, dst_col, side, points, labels
        )
        if hit is not None:
            reused += 1
            return hit
        if should_stop is not None and should_stop():
            raise GrowthCancelled()
        mask, logits, score = raw_seam(src_row, src_col, dst_row, dst_col, side, points, labels)
        memory.store_seam(
            session,
            src_row,
            src_col,
            dst_row,
            dst_col,
            side,
            points,
            labels,
            mask,
            logits,
            score,
        )
        fresh += 1
        return mask, logits, score

    if raw_seam is not None:
        kwargs["seam_predict"] = caching_seam

    result = grow_segmentation(
        uploaded_set,
        foreground,
        background,
        caching_predict,
        remembered=remembered or None,
        should_stop=should_stop,
        seam_graph=graph,
        **kwargs,
    )
    memory.remember_tiles(session, result.tiles)
    memory.remember_seam_graph(session, graph)
    return RememberedGrowth(result=result, reused=reused, predicted=fresh)


def _seam_key(
    src_row: int,
    src_col: int,
    dst_row: int,
    dst_col: int,
    side: str,
    points: Sequence[Point],
    labels: Sequence[int],
) -> _SeamKey:
    return (
        int(src_row),
        int(src_col),
        int(dst_row),
        int(dst_col),
        side,
        tuple((int(x), int(y)) for x, y in points),
        tuple(int(label) for label in labels),
    )


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


def _clone_prediction(pred: TilePrediction) -> TilePrediction:
    logits = None if pred.logits is None else np.array(pred.logits, copy=True)
    return TilePrediction(
        row=int(pred.row),
        col=int(pred.col),
        mask=np.array(pred.mask, copy=True),
        logits=logits,
        score=float(pred.score),
    )


def _nbytes(array: Optional[np.ndarray]) -> int:
    if array is None:
        return 0
    return int(np.asarray(array).nbytes)
