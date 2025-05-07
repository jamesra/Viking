"""
SegmentationClient Package

This package provides a client for the segmentation service.
It includes a client example and utilities for testing the service.
"""

# Import the client functions for easy access
from SegmentationClient.client_example import segment_image, show_labeled_image, colorize_labels

__all__ = [
    'segment_image',
    'show_labeled_image',
    'colorize_labels'
]