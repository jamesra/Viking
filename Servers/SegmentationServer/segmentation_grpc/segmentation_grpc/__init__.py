"""gRPC types for the segmentation service.

Stubs are committed next to this package. Regenerate with
`python -m segmentation_grpc` when segmentation.proto changes.
"""

from .generate_grpc import generate_grpc_code
from .segmentation_pb2 import (
    DeleteImageRequest,
    DeleteImageResponse,
    MultiSegmentationRequest,
    Point,
    Polygon,
    SegmentationRequest,
    SegmentationResponse,
    SegmentResult,
    ServerStatusRequest,
    ServerStatusResponse,
    UploadImageRequest,
    UploadImageResponse,
)
from .segmentation_pb2_grpc import (
    SegmentationServiceServicer,
    SegmentationServiceStub,
    add_SegmentationServiceServicer_to_server,
)

__all__: list[str] = [
    'SegmentationRequest',
    'SegmentationResponse',
    'MultiSegmentationRequest',
    'UploadImageRequest',
    'UploadImageResponse',
    'DeleteImageRequest',
    'DeleteImageResponse',
    'ServerStatusRequest',
    'ServerStatusResponse',
    'Point',
    'Polygon',
    'SegmentResult',
    'SegmentationServiceStub',
    'SegmentationServiceServicer',
    'add_SegmentationServiceServicer_to_server',
    'generate_grpc_code',
]
