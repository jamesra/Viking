"""Example client for the segmentation gRPC service."""

from .client_example import colorize_labels, segment_image, show_labeled_image

__all__ = [
    "segment_image",
    "show_labeled_image",
    "colorize_labels",
]
