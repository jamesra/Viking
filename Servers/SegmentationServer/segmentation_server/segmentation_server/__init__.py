"""
segmentation_server Package

This package provides a server for the segmentation service.
It includes the core segmentation model and the gRPC server implementation.
"""

from segmentation_server.server import serve

__all__: list[str] = [
    'serve',
    'SegmentationModel',
]


def __getattr__(name: str):
    if name == 'SegmentationModel':
        from segmentation_server.segmentation_service import SegmentationModel
        return SegmentationModel
    raise AttributeError(f"module {__name__!r} has no attribute {name!r}")
