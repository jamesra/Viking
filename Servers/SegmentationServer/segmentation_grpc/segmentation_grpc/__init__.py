"""gRPC types for the segmentation service.

Stubs are committed next to this package. Regenerate with
`python -m segmentation_grpc` when segmentation.proto or Common/geometry.proto changes.

The shared geometry types (package ``viking.geometry.v1``) are re-exported here, so a caller
imports ``TileGrid``, ``GridCell`` or ``Shape`` from this package like the segmentation types.
"""

from .generate_grpc import generate_grpc_code
from .geometry_pb2 import (
    Circle,
    CoordinateSpace,
    CurveFit,
    CurveMethod,
    Ellipse,
    Frame,
    GridCell,
    IntPoint,
    IntSize,
    Line,
    Polygon,
    Rectangle,
    Ring,
    Shape,
    TileGrid,
    Vector2,
)
from .segmentation_pb2 import (
    Feature,
    HeldTilesRequest,
    HeldTilesResponse,
    MaskKind,
    MaskTile,
    MaskTiles,
    ModelInfo,
    SegmentationResponse,
    SegmentResult,
    SegmentTilesRequest,
    SegmentTilesStreamRequest,
    SegmentTilesStreamResponse,
    ServerStatusRequest,
    ServerStatusResponse,
    TilesAnswer,
    TilesNeeded,
    UploadTileRequest,
    UploadTileResponse,
)
from .segmentation_pb2_grpc import (
    SegmentationServiceServicer,
    SegmentationServiceStub,
    add_SegmentationServiceServicer_to_server,
)

__all__: list[str] = [
    # viking.geometry.v1
    'Circle',
    'CoordinateSpace',
    'CurveFit',
    'CurveMethod',
    'Ellipse',
    'Frame',
    'GridCell',
    'IntPoint',
    'IntSize',
    'Line',
    'Polygon',
    'Rectangle',
    'Ring',
    'Shape',
    'TileGrid',
    'Vector2',
    # segmentation
    'UploadTileRequest',
    'UploadTileResponse',
    'HeldTilesRequest',
    'HeldTilesResponse',
    'SegmentTilesRequest',
    'SegmentTilesStreamRequest',
    'SegmentTilesStreamResponse',
    'TilesAnswer',
    'TilesNeeded',
    'Feature',
    'MaskKind',
    'MaskTile',
    'MaskTiles',
    'SegmentationResponse',
    'SegmentResult',
    'ServerStatusRequest',
    'ServerStatusResponse',
    'ModelInfo',
    'SegmentationServiceStub',
    'SegmentationServiceServicer',
    'add_SegmentationServiceServicer_to_server',
    'generate_grpc_code',
]
