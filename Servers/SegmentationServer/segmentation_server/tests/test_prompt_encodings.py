"""The mask prompt a model was trained on: the start window is encoded per the model's manifest."""

from __future__ import annotations

from dataclasses import replace

import numpy as np
import pytest

from cell_world import World, ellipse
from segmentation_server import trainer_prompt_encoding as enc
from segmentation_server.cell_grid import CELL_SIZE, LOGIT_SCALE, LOGIT_WINDOW, Cell, MaskShape
from segmentation_server.prompt_encodings import EdgeConfidenceEncoder
from segmentation_server.tile_growth import grow_segmentation

CELL = Cell(row=4, col=4)  # window origin (2048, 2048)


def disc(cx: int, cy: int, radius: int) -> MaskShape:
    """A disc seed as a client sends it: its bounding box and a bitmap in Y-up order."""
    ys, xs = np.mgrid[-radius:radius + 1, -radius:radius + 1]
    return MaskShape((cx - radius, cy - radius, cx + radius, cy + radius), xs**2 + ys**2 <= radius**2)


def window_value(prompt: np.ndarray, x: int, y: int) -> float:
    """The prompt sample at mosaic ``(x, y)`` of ``CELL``'s window (Y-up mosaic, Y-down window)."""
    col = (x - 2048) // LOGIT_SCALE
    row = (2048 + CELL_SIZE - 1 - y) // LOGIT_SCALE
    return float(prompt[row, col])


def test_window_full_is_the_pixels_window_samples_reduces() -> None:
    shape = disc(2560, 2560, 150)

    full = shape.window_full(CELL)
    samples = shape.window_samples(CELL)

    assert full.shape == (CELL_SIZE, CELL_SIZE)
    reduced = full.reshape(LOGIT_WINDOW, LOGIT_SCALE, LOGIT_WINDOW, LOGIT_SCALE).any(axis=(1, 3))
    assert np.array_equal(reduced, samples)
    assert full.sum() == int(shape.bitmap_up.sum())


def test_window_full_is_none_when_the_mask_misses_the_window() -> None:
    assert disc(100, 100, 20).window_full(CELL) is None
    assert MaskShape((100, 100, 200, 200)).window_full(CELL) is None


def test_a_filled_rectangle_fills_its_window_pixels() -> None:
    full = MaskShape((2100, 2100, 2199, 2149)).window_full(CELL)

    assert full.sum() == 100 * 50


def test_a_seed_is_strong_inside_and_weak_at_its_edge() -> None:
    prompt = EdgeConfidenceEncoder({}).start_window(CELL, disc(2560, 2560, 150), [])

    assert prompt.shape == (256, 256) and prompt.dtype == np.float32
    assert window_value(prompt, 2560, 2560) == pytest.approx(enc.SEED_LOGIT, abs=0.2)
    assert 0 < window_value(prompt, 2560 + 148, 2560) < 0.5 * enc.SEED_LOGIT  # near the edge: little confidence
    assert window_value(prompt, 2560 + 300, 2560) == 0.0  # unknown outside


def test_a_mask_to_correct_is_encoded_weaker_than_a_seed() -> None:
    seed = disc(2560, 2560, 150)

    as_seed = EdgeConfidenceEncoder({}).start_window(CELL, seed, [])
    as_correction = EdgeConfidenceEncoder({}).start_window(CELL, replace(seed, role="correction"), [])

    assert window_value(as_correction, 2560, 2560) == pytest.approx(enc.CORRECTION_LOGIT, abs=0.1)
    assert window_value(as_seed, 2560, 2560) > 3 * window_value(as_correction, 2560, 2560)


def test_a_neighbour_is_negative_and_where_it_contradicts_the_seed_the_pixel_is_unknown() -> None:
    seed = disc(2560, 2560, 150)
    neighbour = MaskShape((2600, 2400, 2800, 2700))  # overlaps the seed's right side

    prompt = EdgeConfidenceEncoder({}).start_window(CELL, seed, [neighbour])

    assert window_value(prompt, 2740, 2550) < -5.0  # inside the neighbour only
    assert window_value(prompt, 2640, 2560) == 0.0  # in both: contradictory, so unknown
    assert window_value(prompt, 2480, 2560) > 5.0  # the seed's far side is untouched


def test_the_manifests_constants_are_used_not_the_defaults() -> None:
    record = {"SEED_LOGIT": 6.0, "EDGE_TAU_PX": 4.0}

    prompt = EdgeConfidenceEncoder(record).start_window(CELL, disc(2560, 2560, 150), [])

    assert window_value(prompt, 2560, 2560) == pytest.approx(6.0, abs=0.1)


def test_a_window_the_mask_misses_gets_no_mask_prompt() -> None:
    assert EdgeConfidenceEncoder({}).start_window(CELL, disc(100, 100, 20), []) is None


def test_the_encoder_matches_the_trainers_function_on_the_same_masks() -> None:
    seed = disc(2560, 2560, 150)
    neighbour = MaskShape((2600, 2400, 2800, 2700))

    ours = EdgeConfidenceEncoder({}).start_window(CELL, seed, [neighbour])
    theirs = enc.encode_mask_input(
        (CELL_SIZE, CELL_SIZE), seed=seed.window_full(CELL), negatives=[neighbour.window_full(CELL)]
    )

    assert np.array_equal(ours, theirs)


# ---- through the growth walk ------------------------------------------------------------------------------------

def client_square(cx: int, cy: int, radius: int):
    half = int(radius / np.sqrt(2))
    reach = int(round(0.95 * radius))
    return (cx - half, cy - half, cx + half, cy + half), [(cx + reach, cy), (cx, cy + reach), (cx - reach, cy), (cx, cy - reach)]


def test_the_walk_gives_the_start_window_the_encoders_prompt_and_no_flat_values() -> None:
    world = World(ellipse(2304, 2304, 300, 300, 0.0))
    square, points = client_square(2304, 2304, 300)
    carve = (2560, 2200, 2660, 2400)  # beside the seed: where a negative overlaps a seed the pixel is unknown

    grow_segmentation(
        points, [], world.predict, masks=[MaskShape(square)], background_masks=[carve],
        prompt_encoder=EdgeConfidenceEncoder({}),
    )

    prompt = world.mask_prompts[0]
    assert prompt.shape == (256, 256)
    assert prompt.max() == pytest.approx(enc.SEED_LOGIT, abs=0.5)  # not the legacy flat +8
    assert prompt.min() < -5.0  # the carve is a negative


def test_without_an_encoder_the_walk_still_builds_the_legacy_flat_prompt() -> None:
    world = World(ellipse(2304, 2304, 300, 300, 0.0))
    square, points = client_square(2304, 2304, 300)
    carve = (2400, 2200, 2500, 2400)

    grow_segmentation(points, [], world.predict, masks=[MaskShape(square)], background_masks=[carve])

    prompt = world.mask_prompts[0]
    assert prompt.max() == 8.0 and prompt.min() == -8.0


def test_a_correction_role_reaches_the_encoder_through_the_walk() -> None:
    world = World(ellipse(2304, 2304, 300, 300, 0.0))
    square, points = client_square(2304, 2304, 300)

    grow_segmentation(
        points, [], world.predict, masks=[replace(MaskShape(square), role="correction")],
        prompt_encoder=EdgeConfidenceEncoder({}),
    )

    assert world.mask_prompts[0].max() == pytest.approx(enc.CORRECTION_LOGIT, abs=0.2)
