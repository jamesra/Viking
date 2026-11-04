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

Viking merges overlapping proposals by asking again for the merged set, and every step of that chain is cached, so a
client that comes back walks the same chain from cache hits.

One SQLite file per volume (WAL) under ``SEGMENTATION_RESULTS_DIR``, shared by the model containers of a host; the key
names the model, so they never read each other's rows. It holds three other things the cache needs:

- reviewed outlines: outlines a person approved or retraced in the gallery, sent with the next segmentation of that
  location until it is edited; never evicted;
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
PROMPT_SCHEME = "prompt-v2"  # v2: the outlines to correct (correction_feature), and one click per island
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
    created_ticks  INTEGER NOT NULL,
    last_hit_ticks INTEGER NOT NULL,
    response       BLOB    NOT NULL,
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
CREATE TABLE IF NOT EXISTS reviewed_outline (
    location_id         INTEGER PRIMARY KEY,
    last_modified_ticks INTEGER NOT NULL,
    outline             BLOB    NOT NULL,
    state               TEXT    NOT NULL,
    reviewed_ticks      INTEGER NOT NULL,
    reviewer            TEXT    NOT NULL
);
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
    """The object prompt as a hash: threshold, mask input, painted bitmaps, clicks and shapes, and the outlines to
    correct.

    Computed here from the decoded request, so two clients that format numbers differently agree. Coordinates are grid
    pixels at the grid's downsample, which is part of the key on its own.
    """
    has_field = getattr(request, "HasField", None)
    threshold = (
        f"{float(request.mask_threshold):.4f}" if callable(has_field) and has_field("mask_threshold") else "default"
    )
    text = (
        f"{PROMPT_SCHEME}|thr={threshold}|mask={int(bool(request.use_mask_input))}:{mask_digest}|"
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


@dataclass(frozen=True)
class CachedResult:
    response: bytes
    created_ticks: int


@dataclass(frozen=True)
class StoredOutline:
    location_id: int
    last_modified_ticks: int
    outline: bytes
    state: str
    reviewed_ticks: int
    reviewer: str


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
        with self._lock:
            self._ready[volume] = path
        return path

    @staticmethod
    def _connect(path: Path) -> sqlite3.Connection:
        # A connection per call: several model containers and threads share a file, and WAL lets readers through
        # while one of them writes.
        connection = sqlite3.connect(str(path), timeout=30.0)
        connection.execute("PRAGMA journal_mode=WAL")
        connection.execute("PRAGMA foreign_keys=ON")
        return connection

    def lookup(self, volume: str, key: CacheKey, job_id: int = 0) -> Optional[CachedResult]:
        """The cached answer for ``key``, marking it hit (and linking it to ``job_id``), or None."""
        path = self._path(volume)
        with self._connect(path) as connection:
            row = connection.execute(
                "SELECT response, created_ticks FROM segmentation WHERE location_key=? AND location_lm=? AND "
                "model_version=? AND server_version=? AND space=? AND transform=? AND channel=? AND downsample=? AND "
                "context_hash=? AND prompt_hash=?",
                key.columns(),
            ).fetchone()
            if row is None:
                return None
            connection.execute(
                "UPDATE segmentation SET last_hit_ticks=? WHERE location_key=? AND location_lm=? AND model_version=? "
                "AND server_version=? AND space=? AND transform=? AND channel=? AND downsample=? AND context_hash=? "
                "AND prompt_hash=?",
                (now_ticks(), *key.columns()),
            )
            self._link(connection, job_id, key.location_key)
        return CachedResult(response=bytes(row[0]), created_ticks=int(row[1]))

    def store(self, volume: str, key: CacheKey, response: bytes, job_id: int = 0) -> int:
        """Keep ``response`` under ``key``, replacing any row there. Returns the row's created time (ticks)."""
        path = self._path(volume)
        created = now_ticks()
        with self._connect(path) as connection:
            connection.execute(
                "INSERT OR REPLACE INTO segmentation (location_key, location_lm, model_version, server_version, space, "
                "transform, channel, downsample, context_hash, prompt_hash, created_ticks, last_hit_ticks, response) "
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                (*key.columns(), created, created, sqlite3.Binary(response)),
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

    # -- reviewed outlines -------------------------------------------------------------------------------------------

    def reviewed(self, volume: str, location_id: int, last_modified_ticks: int) -> Optional[StoredOutline]:
        """The reviewed outline of a location not edited since it was reviewed, or None."""
        path = self._path(volume)
        with self._connect(path) as connection:
            row = connection.execute(
                "SELECT last_modified_ticks, outline, state, reviewed_ticks, reviewer FROM reviewed_outline "
                "WHERE location_id=?",
                (location_id,),
            ).fetchone()
        if row is None or int(row[0]) != int(last_modified_ticks):
            return None
        return StoredOutline(location_id, int(row[0]), bytes(row[1]), str(row[2]), int(row[3]), str(row[4]))

    def put_reviewed(self, volume: str, outlines: Sequence[StoredOutline], remove: Sequence[int]) -> Tuple[int, int]:
        """Store and remove reviewed outlines. Returns (stored, removed)."""
        path = self._path(volume)
        with self._connect(path) as connection:
            connection.executemany(
                "INSERT OR REPLACE INTO reviewed_outline (location_id, last_modified_ticks, outline, state, "
                "reviewed_ticks, reviewer) VALUES (?, ?, ?, ?, ?, ?)",
                [
                    (o.location_id, o.last_modified_ticks, sqlite3.Binary(o.outline), o.state, o.reviewed_ticks,
                     o.reviewer)
                    for o in outlines
                ],
            )
            removed = 0
            for location_id in remove:
                removed += connection.execute(
                    "DELETE FROM reviewed_outline WHERE location_id=?", (int(location_id),)
                ).rowcount
        return len(outlines), removed

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
        """Apply the age and size limits to one volume. Rows linked to a job are kept. Returns rows removed."""
        path = self._path(volume)
        unprotected = "location_key NOT IN (SELECT location_key FROM job_link)"
        removed = 0
        with self._connect(path) as connection:
            if self._max_age_ticks:
                removed += connection.execute(
                    f"DELETE FROM segmentation WHERE last_hit_ticks < ? AND {unprotected}",
                    (now_ticks() - self._max_age_ticks,),
                ).rowcount
            if self._max_bytes:
                total = int(connection.execute("SELECT COALESCE(SUM(LENGTH(response)), 0) FROM segmentation").fetchone()[0])
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
                connection.execute(
                    "DELETE FROM segmentation_member WHERE location_key NOT IN (SELECT location_key FROM segmentation)"
                )
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
