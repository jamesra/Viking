"""Finished segmentations kept by location, so a cell already segmented is answered without tiles or the model.

A request that names its locations (``SegmentTilesRequest.locations``) is cached under a key of everything that changes
the answer; the client keeps no cache of its own. The key is exact equality on:

- the locations, sorted ascending, with their LastModified ticks (:func:`location_key`): one location is its plain id,
  a merged group is ``h:`` and a hash. An edit to any member changes the key, so the next request segments anew;
- the model version, this server's version, and the grid's space, transform, channel and downsample;
- the context (:func:`context_hash`): the neighbours sorted ascending with their LastModified, so an edited, added or
  removed neighbour misses;
- the prompt (:func:`prompt_hash`), so a polygon prompted with its inscribed seed circle (gallery scoring) is never
  served as the polygon itself (the converter).

The mask threshold is not in the key. Each row records the threshold its walk grew at (``growth_threshold``), and the
logits answer any request at that threshold or above, since a client draws its own outline from them: a request at or
above the row's threshold is a hit, one below it misses, grows anew at the lower value and replaces the row. A row's
threshold only ever goes down.

Viking merges overlapping proposals by asking again for the merged set, and every step of that chain is cached, so a
client that comes back walks the same chain from cache hits.

One SQLite file per volume (WAL) under ``SEGMENTATION_RESULTS_DIR``, shared by the model containers of a host; the key
names the model, so they never read each other's rows. It holds three other things the cache needs:

- manual traces: outlines a person traced for one location in the gallery's converter, which cannot write to the
  annotation database. A trace is a row of the ``segmentation`` table with ``manual_trace`` set, holding the outline
  (a serialized ``Shape``) instead of logits, keyed by the location and the LastModified it was traced against; it
  answers for the location until it is edited, whatever the model or threshold, and is never evicted. A row is either
  a person's polygon or the model's logits, never both;
- jobs: a reason to keep rows. A row whose location key is linked to a job is never evicted;
- size and age limits, both optional (``SEGMENTATION_RESULTS_MAX_BYTES`` per volume file and
  ``SEGMENTATION_RESULTS_MAX_AGE_DAYS``): least recently hit rows go first.
"""

from __future__ import annotations

import hashlib
import logging
import os
import re
import sqlite3
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Sequence, Tuple

logger = logging.getLogger(__name__)

# Changing how a prompt or a context is written changes this, so old rows miss instead of matching wrongly.
# v2: the outlines to correct (correction_feature), and one click per island. v3: the threshold left the hash.
PROMPT_SCHEME = "prompt-v3"
CONTEXT_SCHEME = "context-v1"

# .NET ticks (100 ns since 0001-01-01) at the Unix epoch: the protocol's timestamps are .NET ticks.
_TICKS_AT_UNIX_EPOCH = 621_355_968_000_000_000
_VOLUME_FILE = re.compile(r"[^A-Za-z0-9._-]")
# Eviction runs at most this often per volume; it is a sweep over the table, not per insert.
_EVICT_INTERVAL_SECONDS = 60.0

_SCHEMA = """
CREATE TABLE IF NOT EXISTS segmentation (
    location_key   TEXT    NOT NULL,
    location_lm    INTEGER NOT NULL,
    model_version  TEXT    NOT NULL,
    server_version TEXT    NOT NULL,
    space          INTEGER NOT NULL,
    transform      TEXT    NOT NULL,
    channel        TEXT    NOT NULL,
    downsample     INTEGER NOT NULL,
    context_hash   TEXT    NOT NULL,
    prompt_hash    TEXT    NOT NULL,
    growth_threshold REAL  NOT NULL,
    created_ticks  INTEGER NOT NULL,
    last_hit_ticks INTEGER NOT NULL,
    response       BLOB    NOT NULL,
    manual_trace   INTEGER NOT NULL DEFAULT 0,
    tracer         TEXT    NOT NULL DEFAULT '',
    PRIMARY KEY (location_key, location_lm, model_version, server_version, space, transform, channel, downsample,
                 context_hash, prompt_hash)
);
CREATE INDEX IF NOT EXISTS segmentation_last_hit ON segmentation (last_hit_ticks);
CREATE TABLE IF NOT EXISTS segmentation_member (
    location_key TEXT    NOT NULL,
    location_id  INTEGER NOT NULL,
    PRIMARY KEY (location_key, location_id)
);
CREATE INDEX IF NOT EXISTS segmentation_member_location ON segmentation_member (location_id);
CREATE TABLE IF NOT EXISTS job (
    job_id        INTEGER PRIMARY KEY AUTOINCREMENT,
    title         TEXT    NOT NULL,
    username      TEXT    NOT NULL,
    description   TEXT    NOT NULL,
    kind          TEXT    NOT NULL,
    created_ticks INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS job_link (
    job_id       INTEGER NOT NULL REFERENCES job (job_id) ON DELETE CASCADE,
    location_key TEXT    NOT NULL,
    PRIMARY KEY (job_id, location_key)
);
CREATE INDEX IF NOT EXISTS job_link_key ON job_link (location_key);
"""

# A manual trace fills the key columns it has no value for with these, so it never matches a model lookup.
_TRACE_KEY_FILLER = ("", "", 0, "", "", 0, "", "")


def now_ticks() -> int:
    """The current time, UTC, in .NET ticks."""
    return _TICKS_AT_UNIX_EPOCH + time.time_ns() // 100


def _half_pixels(value: float) -> int:
    """``value`` in whole half-pixels, ties to even, so float noise between clients does not change a hash."""
    return round(float(value) * 2)


def location_key(locations: Sequence[Any]) -> Tuple[str, int]:
    """``(key, lm)`` for ``SegmentTilesRequest.locations``.

    One location is its plain id with its LastModified as ``lm``; a group is ``h:`` and the hash of the ids sorted
    ascending with their LastModified, which then carries the times itself (``lm`` 0). The client may list the group in
    any order.
    """
    pairs = sorted((int(ref.id), int(ref.last_modified_ticks)) for ref in locations)
    if len(pairs) == 1:
        return str(pairs[0][0]), pairs[0][1]
    text = ",".join(f"{location_id}:{lm}" for location_id, lm in pairs)
    return "h:" + hashlib.sha256(text.encode("ascii")).hexdigest(), 0


def _point(point: Any) -> Tuple[int, int]:
    return _half_pixels(point.x), _half_pixels(point.y)


def _ring(points: Iterable[Any]) -> str:
    """A closed ring from its lowest (x, y) vertex, counter-clockwise: rasterising ignores where it starts and turns."""
    vertices = [_point(point) for point in points]
    if len(vertices) > 1 and vertices[0] == vertices[-1]:
        vertices.pop()
    if not vertices:
        return ""
    twice_area = sum(
        x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(vertices, vertices[1:] + vertices[:1])
    )
    if twice_area < 0:
        vertices.reverse()
    start = vertices.index(min(vertices))
    vertices = vertices[start:] + vertices[:start]
    return ";".join(f"{x},{y}" for x, y in vertices)


def _fit(shape: Any) -> str:
    fit = shape.fit
    return f"{int(fit.method)}/{int(fit.interpolations)}" if int(fit.method) else "0"


def _shape(shape: Any) -> str:
    """One Shape as text: its kind and quantised numbers."""
    kind = shape.WhichOneof("kind")
    if kind == "circle":
        cx, cy = _point(shape.circle.center)
        return f"C:{cx},{cy},{_half_pixels(shape.circle.radius)}"
    if kind == "ellipse":
        ellipse = shape.ellipse
        cx, cy = _point(ellipse.center)
        # The angle in millionths of a radian: the axes carry the size, this only orients them.
        return (
            f"E:{cx},{cy},{_half_pixels(ellipse.semi_major)},{_half_pixels(ellipse.semi_minor)},"
            f"{round(float(ellipse.rotation) * 1e6)}"
        )
    if kind == "line":
        line = shape.line
        if line.closed:
            points = _ring(line.points)
        else:
            points = ";".join(f"{x},{y}" for x, y in (_point(point) for point in line.points))
        return f"L:{int(line.closed)}:{_half_pixels(line.width)}:{_fit(line)}:{points}"
    if kind == "polygon":
        polygon = shape.polygon
        holes = sorted(_ring(hole.points) for hole in polygon.holes)
        return f"P:{_fit(polygon)}:{_ring(polygon.exterior.points)}|" + "|".join(holes)
    if kind == "point":
        x, y = _point(shape.point)
        return f"X:{x},{y}"
    return "?"


def _feature(feature: Any) -> str:
    points = sorted(_point(point) for point in feature.points)
    shapes = sorted(_shape(shape) for shape in feature.shapes)
    return "pts=" + ";".join(f"{x},{y}" for x, y in points) + "|shapes=" + "#".join(shapes)


def prompt_hash(request: Any, mask_digest: str) -> str:
    """The object prompt as a hash: mask input, painted bitmaps, clicks and shapes, and the outlines to correct.

    Not the threshold: a row answers every threshold at or above the one it grew at (see the module docstring).
    Computed here from the decoded request, so two clients that format numbers differently agree. Coordinates are grid
    pixels at the grid's downsample, which is part of the key on its own.
    """
    text = (
        f"{PROMPT_SCHEME}|mask={int(bool(request.use_mask_input))}:{mask_digest}|"
        f"{_feature(request.foreground_feature)}|correction={_feature(request.correction_feature)}"
    )
    return hashlib.sha256(text.encode("ascii")).hexdigest()


def context_hash(request: Any) -> str:
    """What the object is not, as a hash.

    Listed neighbours stand for their background shapes, whose geometry their LastModified pins, so two clients that
    map a neighbour between spaces slightly differently still agree. Without neighbours the background shapes are
    hashed themselves. Background clicks are always hashed: they belong to no location.
    """
    neighbours = sorted((int(ref.id), int(ref.last_modified_ticks)) for ref in request.neighbors)
    background = request.background_feature
    points = ";".join(f"{x},{y}" for x, y in sorted(_point(point) for point in background.points))
    if neighbours:
        shapes = "refs=" + ",".join(f"{location_id}:{lm}" for location_id, lm in neighbours)
    else:
        shapes = "shapes=" + "#".join(sorted(_shape(shape) for shape in background.shapes))
    return hashlib.sha256(f"{CONTEXT_SCHEME}|pts={points}|{shapes}".encode("ascii")).hexdigest()


@dataclass(frozen=True)
class CacheKey:
    """Everything a cached answer must match, column for column (see the module docstring)."""

    location_key: str
    location_lm: int
    model_version: str
    server_version: str
    space: int
    transform: str
    channel: str
    downsample: int
    context_hash: str
    prompt_hash: str
    # Not part of the key: the ids behind a hashed location key, for clean-up and logs.
    location_ids: Tuple[int, ...] = ()

    def columns(self) -> Tuple[Any, ...]:
        return (
            self.location_key, self.location_lm, self.model_version, self.server_version, self.space, self.transform,
            self.channel, self.downsample, self.context_hash, self.prompt_hash,
        )


def cache_key(request: Any, mask_digest: str, model_version: str, server_version: str) -> Optional[CacheKey]:
    """The key of a ``SegmentTilesRequest``, or None when it names no location (nothing is cached)."""
    if not request.locations:
        return None
    key, lm = location_key(request.locations)
    grid = request.grid
    return CacheKey(
        location_key=key,
        location_lm=lm,
        model_version=model_version,
        server_version=server_version,
        space=int(grid.space),
        transform=str(grid.transform),
        channel=str(grid.channel),
        downsample=int(grid.downsample),
        context_hash=context_hash(request),
        prompt_hash=prompt_hash(request, mask_digest),
        location_ids=tuple(sorted({int(ref.id) for ref in request.locations})),
    )


# Thresholds closer than this are the same: a float32 from the request against a REAL in the row.
_THRESHOLD_EPSILON = 1e-4


@dataclass(frozen=True)
class CachedResult:
    response: bytes
    created_ticks: int
    growth_threshold: float


@dataclass(frozen=True)
class StoredTrace:
    location_id: int
    last_modified_ticks: int
    # A serialized ``Shape`` in mosaic pixels at downsample 1.
    outline: bytes
    tracer: str
    traced_ticks: int


@dataclass(frozen=True)
class JobRow:
    job_id: int
    title: str
    username: str
    description: str
    kind: str
    created_ticks: int
    linked: int


class ResultStore:
    """The per-volume SQLite files. Thread safe; every method touches the disk, so call it off the event loop."""

    _root: Path
    _max_bytes: int
    _max_age_ticks: int
    _lock: threading.Lock
    _ready: Dict[str, Path]
    _last_evict: Dict[str, float]

    def __init__(self, root: Path, max_bytes: int = 0, max_age_days: float = 0.0) -> None:
        """
        Args:
            root: Folder of ``<volume>.sqlite`` files; created when missing.
            max_bytes: Per-volume cap on stored responses, 0 for none.
            max_age_days: Rows not hit for this long are dropped, 0 for never.
        """
        self._root = Path(root)
        self._root.mkdir(parents=True, exist_ok=True)
        self._max_bytes = max(0, int(max_bytes))
        self._max_age_ticks = int(max(0.0, float(max_age_days)) * 86_400 * 10_000_000)
        self._lock = threading.Lock()
        self._ready = {}
        self._last_evict = {}

    def _path(self, volume: str) -> Path:
        with self._lock:
            path = self._ready.get(volume)
            if path is not None:
                return path
        name = _VOLUME_FILE.sub("_", volume) or "_"
        path = self._root / f"{name}.sqlite"
        with self._connect(path) as connection:
            connection.executescript(_SCHEMA)
            self._migrate(connection)
        with self._lock:
            self._ready[volume] = path
        return path

    @staticmethod
    def _migrate(connection: sqlite3.Connection) -> None:
        """Bring a file written before manual traces up to date.

        Its reviewed outlines go: a retraced one becomes the location's manual trace, an approved one was the model's
        own answer and is dropped (its row, if any, is still cached).
        """
        columns = {str(row[1]) for row in connection.execute("PRAGMA table_info(segmentation)")}
        if "manual_trace" not in columns:
            connection.execute("ALTER TABLE segmentation ADD COLUMN manual_trace INTEGER NOT NULL DEFAULT 0")
        if "tracer" not in columns:
            connection.execute("ALTER TABLE segmentation ADD COLUMN tracer TEXT NOT NULL DEFAULT ''")
        reviewed = connection.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='reviewed_outline'"
        ).fetchone()
        if reviewed is None:
            return
        rows = connection.execute(
            "SELECT location_id, last_modified_ticks, outline, reviewer, reviewed_ticks FROM reviewed_outline "
            "WHERE state='retraced'"
        ).fetchall()
        ResultStore._write_traces(
            connection,
            [StoredTrace(int(r[0]), int(r[1]), bytes(r[2]), str(r[3]), int(r[4])) for r in rows],
        )
        connection.execute("DROP TABLE reviewed_outline")
        logger.info("Result cache: %d retraced reviewed outline(s) became manual traces", len(rows))

    @staticmethod
    def _connect(path: Path) -> sqlite3.Connection:
        # A connection per call: several model containers and threads share a file, and WAL lets readers through
        # while one of them writes.
        connection = sqlite3.connect(str(path), timeout=30.0)
        connection.execute("PRAGMA journal_mode=WAL")
        connection.execute("PRAGMA foreign_keys=ON")
        return connection

    def lookup(self, volume: str, key: CacheKey, threshold: float, job_id: int = 0) -> Optional[CachedResult]:
        """The cached answer for ``key`` when it grew at ``threshold`` or below, marking it hit (and linking it to
        ``job_id``), or None. A row grown at a higher threshold is a miss: its walk may have stopped short."""
        path = self._path(volume)
        with self._connect(path) as connection:
            row = connection.execute(
                "SELECT response, created_ticks, growth_threshold FROM segmentation WHERE location_key=? AND "
                "location_lm=? AND model_version=? AND server_version=? AND space=? AND transform=? AND channel=? AND "
                "downsample=? AND context_hash=? AND prompt_hash=?",
                key.columns(),
            ).fetchone()
            if row is None or float(row[2]) > float(threshold) + _THRESHOLD_EPSILON:
                return None
            connection.execute(
                "UPDATE segmentation SET last_hit_ticks=? WHERE location_key=? AND location_lm=? AND model_version=? "
                "AND server_version=? AND space=? AND transform=? AND channel=? AND downsample=? AND context_hash=? "
                "AND prompt_hash=?",
                (now_ticks(), *key.columns()),
            )
            self._link(connection, job_id, key.location_key)
        return CachedResult(response=bytes(row[0]), created_ticks=int(row[1]), growth_threshold=float(row[2]))

    def store(self, volume: str, key: CacheKey, response: bytes, threshold: float, job_id: int = 0) -> int:
        """Keep ``response``, grown at ``threshold``, under ``key``. It replaces a row grown at a higher threshold and
        leaves one grown at the same or a lower one (two requests that missed together). Returns the created time
        (ticks)."""
        path = self._path(volume)
        created = now_ticks()
        with self._connect(path) as connection:
            connection.execute(
                "INSERT INTO segmentation (location_key, location_lm, model_version, server_version, space, "
                "transform, channel, downsample, context_hash, prompt_hash, growth_threshold, created_ticks, "
                "last_hit_ticks, response) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) "
                "ON CONFLICT (location_key, location_lm, model_version, server_version, space, transform, channel, "
                "downsample, context_hash, prompt_hash) DO UPDATE SET growth_threshold=excluded.growth_threshold, "
                "created_ticks=excluded.created_ticks, last_hit_ticks=excluded.last_hit_ticks, "
                "response=excluded.response WHERE excluded.growth_threshold < segmentation.growth_threshold",
                (*key.columns(), float(threshold), created, created, sqlite3.Binary(response)),
            )
            connection.executemany(
                "INSERT OR IGNORE INTO segmentation_member (location_key, location_id) VALUES (?, ?)",
                [(key.location_key, location_id) for location_id in key.location_ids],
            )
            self._link(connection, job_id, key.location_key)
        self._maybe_evict(volume, path)
        return created

    @staticmethod
    def _link(connection: sqlite3.Connection, job_id: int, key: str) -> None:
        if job_id > 0:
            # An unknown job is ignored: the row is cached either way, only unprotected.
            connection.execute(
                "INSERT OR IGNORE INTO job_link (job_id, location_key) SELECT job_id, ? FROM job WHERE job_id=?",
                (key, job_id),
            )

    # -- manual traces and forgetting ------------------------------------------------------------------------------

    @staticmethod
    def _write_traces(connection: sqlite3.Connection, traces: Sequence[StoredTrace]) -> None:
        """Replace each location's trace, whatever LastModified the old one was drawn against."""
        for trace in traces:
            key = str(int(trace.location_id))
            connection.execute("DELETE FROM segmentation WHERE location_key=? AND manual_trace=1", (key,))
            connection.execute(
                "INSERT OR REPLACE INTO segmentation (location_key, location_lm, model_version, server_version, space, "
                "transform, channel, downsample, context_hash, prompt_hash, growth_threshold, created_ticks, "
                "last_hit_ticks, response, manual_trace, tracer) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, ?, ?, ?, 1, ?)",
                (key, int(trace.last_modified_ticks), *_TRACE_KEY_FILLER, int(trace.traced_ticks), now_ticks(),
                 sqlite3.Binary(trace.outline), trace.tracer),
            )
            connection.execute(
                "INSERT OR IGNORE INTO segmentation_member (location_key, location_id) VALUES (?, ?)",
                (key, int(trace.location_id)),
            )

    def trace(self, volume: str, location_id: int, last_modified_ticks: int) -> Optional[StoredTrace]:
        """The manual trace of a location not edited since it was traced, marking it hit, or None."""
        path = self._path(volume)
        key = str(int(location_id))
        with self._connect(path) as connection:
            row = connection.execute(
                "SELECT location_lm, response, tracer, created_ticks FROM segmentation "
                "WHERE location_key=? AND manual_trace=1",
                (key,),
            ).fetchone()
            if row is None or int(row[0]) != int(last_modified_ticks):
                return None
            connection.execute(
                "UPDATE segmentation SET last_hit_ticks=? WHERE location_key=? AND manual_trace=1", (now_ticks(), key)
            )
        return StoredTrace(int(location_id), int(row[0]), bytes(row[1]), str(row[2]), int(row[3]))

    def put_traces(self, volume: str, traces: Sequence[StoredTrace], remove: Sequence[int]) -> Tuple[int, int]:
        """Store and remove manual traces. Returns (stored, removed)."""
        path = self._path(volume)
        with self._connect(path) as connection:
            self._write_traces(connection, traces)
            removed = 0
            for location_id in remove:
                removed += connection.execute(
                    "DELETE FROM segmentation WHERE location_key=? AND manual_trace=1", (str(int(location_id)),)
                ).rowcount
            if removed:
                self._drop_orphan_members(connection)
        return len(traces), removed

    def forget(self, volume: str, location_ids: Sequence[int]) -> int:
        """Drop every row of the locations: their own results, merged groups they belong to, and their traces.

        For locations just written to the annotation database, whose cached answers describe what they were before.
        Returns rows removed.
        """
        ids = sorted({int(location_id) for location_id in location_ids})
        if not ids:
            return 0
        path = self._path(volume)
        marks = ",".join("?" * len(ids))
        with self._connect(path) as connection:
            keys = {str(location_id) for location_id in ids}
            keys.update(
                str(row[0])
                for row in connection.execute(
                    f"SELECT location_key FROM segmentation_member WHERE location_id IN ({marks})", ids
                )
            )
            removed = 0
            for key in keys:
                removed += connection.execute("DELETE FROM segmentation WHERE location_key=?", (key,)).rowcount
            if removed:
                self._drop_orphan_members(connection)
        return removed

    @staticmethod
    def _drop_orphan_members(connection: sqlite3.Connection) -> None:
        connection.execute(
            "DELETE FROM segmentation_member WHERE location_key NOT IN (SELECT location_key FROM segmentation)"
        )

    # -- jobs --------------------------------------------------------------------------------------------------------

    def create_job(self, volume: str, title: str, username: str, description: str, kind: str) -> JobRow:
        path = self._path(volume)
        created = now_ticks()
        with self._connect(path) as connection:
            cursor = connection.execute(
                "INSERT INTO job (title, username, description, kind, created_ticks) VALUES (?, ?, ?, ?, ?)",
                (title, username, description, kind, created),
            )
            job_id = int(cursor.lastrowid)
        return JobRow(job_id, title, username, description, kind, created, 0)

    def delete_job(self, volume: str, job_id: int) -> bool:
        """Remove a job and its links; its rows become evictable. False when there was no such job."""
        path = self._path(volume)
        with self._connect(path) as connection:
            return connection.execute("DELETE FROM job WHERE job_id=?", (int(job_id),)).rowcount > 0

    def list_jobs(self, volume: str) -> List[JobRow]:
        path = self._path(volume)
        with self._connect(path) as connection:
            rows = connection.execute(
                "SELECT j.job_id, j.title, j.username, j.description, j.kind, j.created_ticks, COUNT(l.location_key) "
                "FROM job j LEFT JOIN job_link l ON l.job_id = j.job_id GROUP BY j.job_id ORDER BY j.job_id"
            ).fetchall()
        return [JobRow(int(r[0]), r[1], r[2], r[3], r[4], int(r[5]), int(r[6])) for r in rows]

    # -- eviction ----------------------------------------------------------------------------------------------------

    def _maybe_evict(self, volume: str, path: Path) -> None:
        if not self._max_bytes and not self._max_age_ticks:
            return
        now = time.monotonic()
        with self._lock:
            last = self._last_evict.get(volume)
            if last is not None and now - last < _EVICT_INTERVAL_SECONDS:
                return
            self._last_evict[volume] = now
        try:
            self.evict(volume)
        except sqlite3.Error:
            logger.exception("Result cache eviction failed for %s (%s)", volume, path)

    def evict(self, volume: str) -> int:
        """Apply the age and size limits to one volume. Manual traces and rows linked to a job are kept, and traces do
        not count toward the size. Returns rows removed."""
        path = self._path(volume)
        unprotected = "manual_trace=0 AND location_key NOT IN (SELECT location_key FROM job_link)"
        removed = 0
        with self._connect(path) as connection:
            if self._max_age_ticks:
                removed += connection.execute(
                    f"DELETE FROM segmentation WHERE last_hit_ticks < ? AND {unprotected}",
                    (now_ticks() - self._max_age_ticks,),
                ).rowcount
            if self._max_bytes:
                total = int(
                    connection.execute(
                        "SELECT COALESCE(SUM(LENGTH(response)), 0) FROM segmentation WHERE manual_trace=0"
                    ).fetchone()[0]
                )
                if total > self._max_bytes:
                    rows = connection.execute(
                        f"SELECT rowid, LENGTH(response) FROM segmentation WHERE {unprotected} "
                        "ORDER BY last_hit_ticks ASC"
                    ).fetchall()
                    doomed: List[Tuple[int]] = []
                    for rowid, size in rows:
                        if total <= self._max_bytes:
                            break
                        doomed.append((int(rowid),))
                        total -= int(size)
                    connection.executemany("DELETE FROM segmentation WHERE rowid=?", doomed)
                    removed += len(doomed)
            if removed:
                self._drop_orphan_members(connection)
        if removed:
            logger.info("Result cache %s: evicted %d row(s)", volume, removed)
        return removed


def open_result_store() -> Optional[ResultStore]:
    """The store under ``SEGMENTATION_RESULTS_DIR``, or None when it is unset (no result cache)."""
    root = os.environ.get("SEGMENTATION_RESULTS_DIR", "").strip()
    if not root:
        logger.info("Result cache disabled (SEGMENTATION_RESULTS_DIR unset)")
        return None

    def number(name: str) -> float:
        raw = os.environ.get(name, "").strip()
        if not raw:
            return 0.0
        try:
            return max(0.0, float(raw))
        except ValueError:
            logger.warning("Invalid %s=%r; no limit", name, raw)
            return 0.0

    max_bytes = int(number("SEGMENTATION_RESULTS_MAX_BYTES"))
    max_age_days = number("SEGMENTATION_RESULTS_MAX_AGE_DAYS")
    try:
        store = ResultStore(Path(root), max_bytes=max_bytes, max_age_days=max_age_days)
    except OSError:
        logger.exception("Result cache disabled: %s is not usable", root)
        return None
    logger.info(
        "Result cache at %s (max %s bytes per volume, max age %s days; 0 is no limit)", root, max_bytes, max_age_days
    )
    return store
