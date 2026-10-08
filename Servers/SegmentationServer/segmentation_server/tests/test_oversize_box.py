"""A box that covers whole cores: those cores are object, and the cells holding only part of it start from its overlap."""

from __future__ import annotations

from cell_world import World, ellipse, iou
from segmentation_server.cell_grid import (
    CELL_SIZE,
    CORE_SIZE,
    MAX_BOX_CELLS,
    Cell,
    core_origin,
    cores_under_box,
    mosaic_to_window,
)
from segmentation_server.mask_utils import NoMatchingMask
from segmentation_server.tile_growth import GrowthWalk, grow_segmentation

# A disc of radius 1300 centred at (2048, 2048). Its inscribed square is about 1839 px on a side, so it
# covers 3x3 whole cores and leaves a ring of 16 cores covered in part.
CENTER = 2048
RADIUS = 1300
BOX = (1129, 1129, 2967, 2967)


class _RecordingWorld(World):
    def __init__(self, truth) -> None:
        super().__init__(truth)
        self.given = []

    def predict(self, row, col, points, labels, box=None):
        self.given.append((Cell(row, col), list(points), list(labels), box))
        return super().predict(row, col, points, labels, box=box)


def _disc_world() -> _RecordingWorld:
    return _RecordingWorld(ellipse(CENTER, CENTER, RADIUS, RADIUS, 0.0))


def _clicks():
    return [(CENTER, CENTER)]


def _split():
    full, partial = cores_under_box(BOX)
    return set(full), partial


def test_the_split_finds_the_whole_cores_and_the_ring_around_them() -> None:
    full, partial = _split()

    assert len(full) == 9
    assert len(partial) == 16
    assert not (full & set(partial))
    for cell, (x0, y0, x1, y1) in partial.items():
        bx, by = core_origin(cell)
        assert bx <= x0 <= x1 < bx + CORE_SIZE and by <= y0 <= y1 < by + CORE_SIZE
        assert (x1 - x0 + 1) * (y1 - y0 + 1) < CORE_SIZE * CORE_SIZE


def test_a_box_exactly_one_core_is_one_whole_core() -> None:
    bx, by = core_origin(Cell(3, 3))

    full, partial = cores_under_box((bx, by, bx + CORE_SIZE - 1, by + CORE_SIZE - 1))

    assert full == [Cell(3, 3)] and partial == {}


def test_a_box_inside_one_core_covers_no_whole_core() -> None:
    bx, by = core_origin(Cell(3, 3))

    full, partial = cores_under_box((bx + 10, by + 10, bx + 100, by + 100))

    assert full == [] and list(partial) == [Cell(3, 3)]


def test_an_absurdly_large_box_is_not_split() -> None:
    side = (MAX_BOX_CELLS + 1) * CORE_SIZE

    assert cores_under_box((0, 0, side, side)) is None


def test_whole_cores_are_never_predicted_and_are_wholly_in_the_result() -> None:
    world = _disc_world()
    full, _partial = _split()

    result = grow_segmentation(_clicks(), [], world.predict, boxes=[BOX])

    predicted = {cell for cell, *_ in world.given}
    assert not (predicted & full), "a core the box covers must not cost a prediction"
    truth_frame = world.to_truth_frame(result)
    for cell in full:
        bx, by = core_origin(cell)
        assert truth_frame[by:by + CORE_SIZE, bx:bx + CORE_SIZE].all()


def test_each_partly_covered_cell_is_first_given_the_overlap_with_its_core_as_the_box() -> None:
    world = _disc_world()
    _full, partial = _split()

    grow_segmentation(_clicks(), [], world.predict, boxes=[BOX])

    first_box = {}
    for cell, _points, _labels, box in world.given:
        first_box.setdefault(cell, box)
    for cell, (x0, y0, x1, y1) in partial.items():
        left, top = mosaic_to_window(cell, x0, y1)
        right, bottom = mosaic_to_window(cell, x1, y0)
        assert first_box[cell] == (left, top, right, bottom), cell
        assert 0 < left and right < CELL_SIZE - 1 and 0 < top and bottom < CELL_SIZE - 1, (
            "the seed must sit clear of the window border so the answer can cover it"
        )


def test_the_seam_record_names_the_overlap_each_partly_covered_cell_started_from() -> None:
    world = _disc_world()
    _full, partial = _split()

    result = grow_segmentation(_clicks(), [], world.predict, boxes=[BOX])

    started = {seam.cell: seam for seam in result.seams if seam.kind == "user"}
    assert set(started) >= set(partial)
    for cell, overlap in partial.items():
        assert started[cell].box == overlap
        assert started[cell].outcome == "accepted"


def test_the_grown_result_matches_a_disc_far_larger_than_one_window() -> None:
    world = _disc_world()

    result = grow_segmentation(_clicks(), [], world.predict, boxes=[BOX])

    assert iou(world.to_truth_frame(result), world.truth_up) >= 0.95
    assert len(world.given) <= 16 * 4 + 40, "no whole core was predicted, so the walk stays small"


def test_the_start_cell_being_taken_as_object_means_a_rejected_neighbor_is_not_a_failure() -> None:
    def refuse(row, col, points, labels, box=None):
        raise NoMatchingMask("nothing fits")

    full, _partial = _split()

    result = grow_segmentation(_clicks(), [], refuse, boxes=[BOX])

    covered = {cell for cell in result.cells if result.cells[cell].core.all()}
    assert covered == full
    assert all(seam.outcome == "rejected" for seam in result.seams)


def test_a_small_box_still_prompts_the_starting_cell_as_before() -> None:
    world = _disc_world()

    result = grow_segmentation(_clicks(), [], world.predict, boxes=[(2000, 2000, 2096, 2096)])

    assert result.seams[0].kind == "user"
    assert result.seams[0].box == (2000, 2000, 2096, 2096), "a box that fits the window is used whole"


def test_a_core_taken_as_object_is_not_counted_against_the_cell_budget() -> None:
    world = _disc_world()
    walk = GrowthWalk(_clicks(), [], world.predict, boxes=[BOX], max_cells=16)

    walk.advance()

    assert len(walk._assumed) == 9
    assert len(walk._states) <= 16
    assert not (walk._assumed & set(walk._states))


def test_the_walk_runs_with_only_the_tiles_the_partly_covered_cells_need() -> None:
    from segmentation_server.cell_grid import tiles_for_cell

    world = _disc_world()
    full, partial = _split()
    world.available = {(t.row, t.col) for cell in partial for t in tiles_for_cell(cell)}

    result = grow_segmentation(_clicks(), [], world.predict, boxes=[BOX])

    covered = {cell for cell in result.cells if result.cells[cell].core.all()}
    assert full <= covered
    assert not any(cell in full for cell, *_ in world.given)
