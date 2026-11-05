"""Prompt providers: what turns the annotations of a SegmentLocation call into the SegmentTilesRequest the walk runs.

A provider owns the prompt rules (which shapes, which clicks, which neighbours are background), so several can exist
side by side and a model is fed by the one it was trained for. The first is Viking.SegmentationPrompts
(``prompt_providers/`` next to this package), a C# library loaded as a NativeAOT shared library through ctypes. Its
calls take and return serialized protobuf messages (``prompt_input.proto``); the bytes never leave this module.

The provider for the served model is named by the manifest's ``client_contract.prompt_provider`` (default
:data:`DEFAULT_PROVIDER`); :func:`open_prompt_provider` loads it, or returns None when it cannot be loaded, and then
SegmentLocation answers UNIMPLEMENTED while SegmentTilesStream works as before.
"""

from __future__ import annotations

import ctypes
import logging
import os
from typing import Callable, Dict, Optional, Protocol, Sequence

from segmentation_grpc import prompt_input_pb2 as prompt_pb
from segmentation_grpc import segmentation_pb2 as pb

logger = logging.getLogger(__name__)

DEFAULT_PROVIDER = "viking-prompts"
DEFAULT_VIKING_LIBRARY = "/home/user/native/Viking.SegmentationPrompts.Native.so"

_OK, _INVALID_ARGUMENT, _TOO_LARGE = 0, 1, 2
_BYTES = ctypes.POINTER(ctypes.c_uint8)


class PromptRejected(ValueError):
    """The provider refused the input: no target, an unsupported annotation type, mixed circles and areas, bad
    geometry."""


class PromptTooLarge(ValueError):
    """The targets cover more tiles or shapes than one request may carry."""


class PromptProvider(Protocol):
    """Builds requests from a :class:`prompt_pb.PromptInput`. Calls may come from several threads at once."""

    # Names the provider and its rules in logs; part of nothing on the wire.
    name: str

    def build_start(self, prompt_input: "prompt_pb.PromptInput") -> "pb.SegmentTilesRequest":
        """The request to grow from."""
        ...

    def reach(self, prompt_input: "prompt_pb.PromptInput") -> "prompt_pb.PromptReach":
        """The rectangle within which every annotation of the section must be passed as a candidate."""
        ...

    def background_for(
        self, prompt_input: "prompt_pb.PromptInput", needed: "pb.TilesNeeded", sent_ids: Sequence[int]
    ) -> "prompt_pb.PromptBackground":
        """The background shapes for ``needed.background_cells`` not among ``sent_ids``, with their ids."""
        ...


class VikingPromptsProvider:
    """Viking.SegmentationPrompts through its native build. Its rules are in ``prompt_providers/README.md``."""

    name: str = DEFAULT_PROVIDER
    path: str
    _library: ctypes.CDLL

    def __init__(self, path: Optional[str] = None) -> None:
        self.path = path or os.environ.get("SEGMENTATION_PROMPT_LIBRARY") or DEFAULT_VIKING_LIBRARY
        library = ctypes.CDLL(self.path)
        out = [ctypes.POINTER(_BYTES), ctypes.POINTER(ctypes.c_ssize_t)]
        library.sp_build_start.argtypes = [ctypes.c_char_p, ctypes.c_ssize_t, *out]
        library.sp_reach.argtypes = [ctypes.c_char_p, ctypes.c_ssize_t, *out]
        library.sp_background_for.argtypes = [
            ctypes.c_char_p, ctypes.c_ssize_t, ctypes.c_char_p, ctypes.c_ssize_t,
            ctypes.POINTER(ctypes.c_int64), ctypes.c_ssize_t, *out,
        ]
        for name in ("sp_build_start", "sp_reach", "sp_background_for"):
            getattr(library, name).restype = ctypes.c_int
        library.sp_free.argtypes = [_BYTES]
        library.sp_free.restype = None
        self._library = library

    def _call(self, name: str, *args: object) -> bytes:
        buffer, length = _BYTES(), ctypes.c_ssize_t()
        status = getattr(self._library, name)(*args, ctypes.byref(buffer), ctypes.byref(length))
        try:
            payload = ctypes.string_at(buffer, length.value)
        finally:
            self._library.sp_free(buffer)
        if status == _OK:
            return payload
        message = payload.decode("utf-8", errors="replace")
        if status == _INVALID_ARGUMENT:
            raise PromptRejected(message)
        if status == _TOO_LARGE:
            raise PromptTooLarge(message)
        raise RuntimeError(f"{name} failed: {message}")

    def build_start(self, prompt_input: "prompt_pb.PromptInput") -> "pb.SegmentTilesRequest":
        data = prompt_input.SerializeToString()
        return pb.SegmentTilesRequest.FromString(self._call("sp_build_start", data, len(data)))

    def reach(self, prompt_input: "prompt_pb.PromptInput") -> "prompt_pb.PromptReach":
        data = prompt_input.SerializeToString()
        return prompt_pb.PromptReach.FromString(self._call("sp_reach", data, len(data)))

    def background_for(
        self, prompt_input: "prompt_pb.PromptInput", needed: "pb.TilesNeeded", sent_ids: Sequence[int]
    ) -> "prompt_pb.PromptBackground":
        data = prompt_input.SerializeToString()
        asked = needed.SerializeToString()
        sent = (ctypes.c_int64 * max(1, len(sent_ids)))(*sent_ids)
        return prompt_pb.PromptBackground.FromString(
            self._call("sp_background_for", data, len(data), asked, len(asked), sent, len(sent_ids))
        )


PROVIDERS: Dict[str, Callable[[], PromptProvider]] = {DEFAULT_PROVIDER: VikingPromptsProvider}


def open_prompt_provider(name: Optional[str] = None) -> Optional[PromptProvider]:
    """The provider ``name`` (default :data:`DEFAULT_PROVIDER`), or None when it is unknown or does not load."""
    wanted = name or DEFAULT_PROVIDER
    factory = PROVIDERS.get(wanted)
    if factory is None:
        logger.error("Prompt provider %r is unknown (known: %s); SegmentLocation is off", wanted, ", ".join(PROVIDERS))
        return None
    try:
        provider = factory()
    except OSError as e:
        logger.warning("Prompt provider %s did not load (%s); SegmentLocation is off", wanted, e)
        return None
    logger.info("Prompt provider %s loaded", wanted)
    return provider
