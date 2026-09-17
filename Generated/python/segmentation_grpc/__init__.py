"""
segmentation_grpc Package

This package provides the gRPC interface for the segmentation service.
It includes the proto file definition and generated Python code.
"""

# Import the generate_grpc_code function
from segmentation_grpc.generate_grpc import generate_grpc_code

generate_grpc_code(False)

# Import the generated gRPC code for easy access
from segmentation_grpc.segmentation_pb2 import (
    SegmentationRequest,
    SegmentationResponse,
    Point,
    Polygon,
    SegmentResult
)
from segmentation_grpc.segmentation_pb2_grpc import (
    SegmentationServiceStub,
    SegmentationServiceServicer,
    add_SegmentationServiceServicer_to_server
)


__all__ = [
    'SegmentationRequest',
    'SegmentationResponse',
    'Point',
    'Polygon',
    'SegmentResult',
    'SegmentationServiceStub',
    'SegmentationServiceServicer',
    'add_SegmentationServiceServicer_to_server',
    'generate_grpc_code'
]