"""prompt_provider: loading the served model's provider, and the Viking provider's native build against its golden
request (run where ``SEGMENTATION_PROMPT_LIBRARY`` names a build; the server image checks the same at build time)."""

from __future__ import annotations

import hashlib
import os
import re
from pathlib import Path

import pytest

from segmentation_grpc import prompt_input_pb2 as prompt_pb
from segmentation_server import prompt_provider
from segmentation_server.prompt_provider import PromptRejected, VikingPromptsProvider, open_prompt_provider

TESTS = Path(__file__).resolve().parents[2] / "prompt_providers" / "Viking.SegmentationPrompts.Tests"


def test_an_unknown_provider_turns_segment_location_off() -> None:
    assert open_prompt_provider("no-such-provider") is None


def test_a_provider_that_does_not_load_turns_segment_location_off(monkeypatch, tmp_path) -> None:
    monkeypatch.setenv("SEGMENTATION_PROMPT_LIBRARY", str(tmp_path / "missing.so"))

    assert open_prompt_provider() is None


def test_the_default_provider_is_vikings() -> None:
    assert prompt_provider.PROVIDERS[prompt_provider.DEFAULT_PROVIDER] is VikingPromptsProvider


@pytest.mark.skipif(
    not os.environ.get("SEGMENTATION_PROMPT_LIBRARY") or not Path(os.environ["SEGMENTATION_PROMPT_LIBRARY"]).exists(),
    reason="no native build of Viking.SegmentationPrompts (SEGMENTATION_PROMPT_LIBRARY)",
)
def test_the_native_build_gives_the_dotnet_tests_golden_request() -> None:
    provider = VikingPromptsProvider()
    golden = prompt_pb.PromptInput.FromString(bytes.fromhex((TESTS / "Fixtures" / "golden_prompt_input.hex").read_text().strip()))
    expected = re.search(r'GoldenDigest = "([0-9A-F]{64})"', (TESTS / "PromptBuilderTests.cs").read_text()).group(1)

    start = provider.build_start(golden)

    assert hashlib.sha256(start.SerializeToString()).hexdigest().upper() == expected
    assert provider.reach(golden).HasField("reach")
    with pytest.raises(PromptRejected):
        provider.build_start(prompt_pb.PromptInput())
