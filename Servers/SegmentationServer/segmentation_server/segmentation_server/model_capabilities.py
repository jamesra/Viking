"""How a model wants its input submitted, as advertised to clients in GetServerStatus."""

from __future__ import annotations

from segmentation_grpc import ModelCapabilities, ResolutionMode, SubmissionMode

SAM2_CAPABILITIES = ModelCapabilities(
    submission_mode=SubmissionMode.SUBMISSION_MODE_FIXED_TILE_GRID,
    resolution_mode=ResolutionMode.RESOLUTION_MODE_SINGLE,
)
"""The SAM2 tile-growth path: fixed 1024 px cells at downsample 1, shared by every client.

Every model class must expose a ``capabilities`` attribute like this one. Neither enum may be
UNSPECIFIED: that value exists only because proto3 requires a zero, and clients reject it.
"""
