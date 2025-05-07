"""
SegmentationServer Package

This package provides a server for the segmentation service.
It includes the core segmentation model and the gRPC server implementation.
"""

# Import the server functions for easy access
from SegmentationServer.server import serve
from SegmentationServer.segmentation_service import SegmentationModel

__all__ = [
    'serve',
    'SegmentationModel'
]