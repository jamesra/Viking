"""The in-memory embedding cache and the cheap canvas fork used when a result is finished. The model-level cache flow is in test_eager_compile."""

from __future__ import annotations

import numpy as np

from segmentation_server.cell_grid import CORE_SIZE, LOGIT_WINDOW, Canvas, Cell
from segmentation_server.embedding_store import DEFAULT_RAM_ENTRIES, EmbeddingRamCache, open_embedding_ram_cache


def test_the_cache_drops_the_least_recently_used_entry_first() -> None:
    cache = EmbeddingRamCache(2)
    cache.put("a", "eager", {"n": 1})
    cache.put("b", "eager", {"n": 2})
    assert cache.get("a", "eager") == {"n": 1}  # a is now the most recently used
    cache.put("c", "eager", {"n": 3})

    assert cache.get("b", "eager") is None
    assert cache.get("a", "eager") == {"n": 1}
    assert cache.get("c", "eager") == {"n": 3}


def test_an_entry_is_keyed_by_encoder_generation_and_can_be_discarded() -> None:
    cache = EmbeddingRamCache(4)
    cache.put("a", "eager", {"n": 1})

    assert cache.get("a", "compiled") is None
    cache.discard("a", "eager")
    assert cache.get("a", "eager") is None


def test_the_size_comes_from_the_environment_and_zero_turns_it_off(monkeypatch) -> None:
    monkeypatch.delenv("SEGMENTATION_EMBEDDING_RAM_ENTRIES", raising=False)
    assert open_embedding_ram_cache().max_entries == DEFAULT_RAM_ENTRIES

    monkeypatch.setenv("SEGMENTATION_EMBEDDING_RAM_ENTRIES", "7")
    assert open_embedding_ram_cache().max_entries == 7

    monkeypatch.setenv("SEGMENTATION_EMBEDDING_RAM_ENTRIES", "0")
    assert open_embedding_ram_cache() is None

    monkeypatch.setenv("SEGMENTATION_EMBEDDING_RAM_ENTRIES", "lots")
    assert open_embedding_ram_cache().max_entries == DEFAULT_RAM_ENTRIES


def _canvas_with_one_prediction() -> Canvas:
    canvas = Canvas()
    canvas.blend_window(Cell(2, 2), np.full((LOGIT_WINDOW, LOGIT_WINDOW), 5.0, dtype=np.float32), 1.0)
    return canvas


def test_pinning_on_a_fork_leaves_the_original_canvas_alone() -> None:
    canvas = _canvas_with_one_prediction()
    before = {cell: canvas.decided_core(cell).copy() for cell in canvas.decided_cells()}

    fork = canvas.fork_for_pins()
    pinned = Cell(40, 40)
    fork.assume_core(pinned)
    fork.deny_box((canvas.decided_cells()[0].col * 512 + 128, canvas.decided_cells()[0].row * 512 + 128,
                   canvas.decided_cells()[0].col * 512 + 200, canvas.decided_cells()[0].row * 512 + 200))

    assert pinned not in canvas.decided_cells(), "the original never saw the pinned core"
    assert pinned in fork.decided_cells()
    for cell, core in before.items():
        assert np.array_equal(canvas.decided_core(cell), core), "the original's pixels are unchanged"


def test_a_fork_reads_the_same_fused_logits_without_copying_them() -> None:
    canvas = _canvas_with_one_prediction()
    fork = canvas.fork_for_pins()

    assert fork.has_logits()
    assert fork._sum is canvas._sum and fork._windows is canvas._windows
    cell = canvas.decided_cells()[0]
    assert np.array_equal(fork.fused_block(cell), canvas.fused_block(cell))
    assert fork.decided_core(cell).shape == (CORE_SIZE, CORE_SIZE)
