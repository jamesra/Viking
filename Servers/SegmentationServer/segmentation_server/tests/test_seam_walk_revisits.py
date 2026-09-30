"""How often a walk crosses the same tile border.

The fakes in ``seam_world`` answer from a ground-truth mask. A border may be
searched a second time for a stretch that only the neighbor holds (the return
of a C) or when a tile's contact doubles after its mask grew. It must not be
searched a third time, and the same prompts must not be asked twice. A wobbling
model, which returns a slightly larger or smaller mask on each call, is what
makes the contact grow between visits.
"""

from __future__ import annotations

from typing import List, Sequence, Tuple

from hypothesis import given, settings
from hypothesis import strategies as st

from seam_world import SIZE, SeamWorld, blob
from segmentation_server.growth_memory import GrowthMemory, grow_remembering, prompt_key
from segmentation_server.tile_growth import TileIndex, tile_of_point

Shape = Tuple[str, int, int, int, int]

MAX_CROSSINGS_PER_EDGE = 2


@st.composite
def _worlds(draw: st.DrawFn) -> Tuple[int, int, List[Shape]]:
    rows = draw(st.integers(min_value=2, max_value=4))
    cols = draw(st.integers(min_value=2, max_value=4))
    shapes = draw(
        st.lists(
            st.tuples(
                st.sampled_from(["rect", "ellipse"]),
                st.integers(min_value=0, max_value=cols * SIZE),
                st.integers(min_value=0, max_value=rows * SIZE),
                st.integers(min_value=6, max_value=cols * SIZE // 2),
                st.integers(min_value=6, max_value=rows * SIZE // 2),
            ),
            min_size=1,
            max_size=5,
        )
    )
    return rows, cols, shapes


def _build(rows: int, cols: int, shapes: Sequence[Shape], wobble: Tuple[int, ...]) -> SeamWorld | None:
    truth = blob(rows, cols, shapes)
    if not truth.any():
        return None
    return SeamWorld(truth=truth, rows=rows, cols=cols, wobble=wobble)


def _incremental_calls(world: SeamWorld, per_call: int) -> None:
    """The client uploads only the cells the server asked for, then calls again."""
    memory = GrowthMemory()
    click = world.click()
    session = prompt_key("v", 1, "c", "t", 1, False, [click], [])
    uploaded = [tile_of_point(click[0], click[1], SIZE)]
    for _ in range(40):
        outcome = grow_remembering(
            memory,
            session,
            uploaded,
            [click],
            [],
            world.predict,
            seam_predict=world.seam_predict,
            tile_size=SIZE,
            max_requested=8,
        )
        fresh = [
            cell
            for cell in outcome.result.requested
            if cell not in uploaded and 0 <= cell.row < world.rows and 0 <= cell.col < world.cols
        ]
        if not fresh:
            return
        uploaded.extend(fresh[:per_call])


def _assert_bounded(world: SeamWorld) -> None:
    for key, crossings in world.crossings_by_edge().items():
        assert crossings <= MAX_CROSSINGS_PER_EDGE, (key, crossings)
    assert world.repeated_prompts() == []


@given(_worlds(), st.sampled_from([(0,), (2,), (3,), (0, 2, 4)]))
@settings(max_examples=150, deadline=None, derandomize=True)
def test_a_walk_crosses_a_border_at_most_twice(
    case: Tuple[int, int, List[Shape]], wobble: Tuple[int, ...]
) -> None:
    rows, cols, shapes = case
    world = _build(rows, cols, shapes, wobble=wobble)
    if world is None:
        return
    world.walk()
    _assert_bounded(world)


@given(_worlds(), st.sampled_from([1, 3]), st.sampled_from([(0,), (0, 2, 4)]))
@settings(max_examples=100, deadline=None, derandomize=True)
def test_incremental_uploads_do_not_add_crossings(
    case: Tuple[int, int, List[Shape]], per_call: int, wobble: Tuple[int, ...]
) -> None:
    """Calls that add cells one at a time share one session, so one graph."""
    rows, cols, shapes = case
    world = _build(rows, cols, shapes, wobble=wobble)
    if world is None:
        return
    _incremental_calls(world, per_call)
    _assert_bounded(world)


@given(_worlds())
@settings(max_examples=100, deadline=None, derandomize=True)
def test_the_same_click_set_does_not_search_again(case: Tuple[int, int, List[Shape]]) -> None:
    rows, cols, shapes = case
    world = _build(rows, cols, shapes, wobble=(0,))
    if world is None:
        return
    memory = GrowthMemory()
    click = world.click()
    session = prompt_key("v", 1, "c", "t", 1, False, [click], [])
    cells: List[TileIndex] = world.cells()

    def call() -> None:
        grow_remembering(
            memory,
            session,
            cells,
            [click],
            [],
            world.predict,
            seam_predict=world.seam_predict,
            tile_size=SIZE,
        )

    call()
    first = len(world.seam_calls)
    call()
    assert len(world.seam_calls) == first
