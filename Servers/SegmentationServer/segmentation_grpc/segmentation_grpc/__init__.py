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
    SegmentImageSetRequest,
    SegmentResult,
    SegmentTilesRequest,
    ServerStatusRequest,
    ServerStatusResponse,
    TileCoord,
    UploadImageRequest,
    UploadImageResponse,
    UploadTileRequest,
    UploadTileResponse,
)
from .segmentation_pb2_grpc import (
    SegmentationServiceServicer,
    SegmentationServiceStub,
    add_SegmentationServiceServicer_to_server,
)

__all__: list[str] = [
    'SegmentationRequest',
    'SegmentationResponse',
    'SegmentImageSetRequest',
    'MultiSegmentationRequest',
    'UploadImageRequest',
    'UploadImageResponse',
    'UploadTileRequest',
    'UploadTileResponse',
    'TileCoord',
    'SegmentTilesRequest',
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
