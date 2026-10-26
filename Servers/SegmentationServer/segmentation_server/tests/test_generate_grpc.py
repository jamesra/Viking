"""Proto discovery for stub generation."""

from __future__ import annotations

from segmentation_grpc.generate_grpc import _find_proto_file


def test_find_proto_file_locates_canonical_segmentation_proto() -> None:
    proto = _find_proto_file()
    assert proto is not None
    assert proto.name == "segmentation.proto"
    assert proto.exists()
    text = proto.read_text(encoding="utf-8")
    assert "rpc GetServerStatus" in text
    assert "in_flight_requests" in text
    assert "recent_latency_ms" in text
    assert "rpc SegmentTilesStream" in text
    assert 'import "Common/geometry.proto"' in text


def test_the_generated_modules_load_as_one_package() -> None:
    """segmentation_pb2 imports the geometry module from this package, not from a top-level ``Common``."""
    from segmentation_grpc import Shape, TileGrid, geometry_pb2, segmentation_pb2

    request = segmentation_pb2.SegmentTilesRequest(grid=TileGrid(volume="v"))
    assert request.grid.volume == "v"
    assert segmentation_pb2.DESCRIPTOR.dependencies[0].name == "Common/geometry.proto"
    assert Shape.DESCRIPTOR.file is geometry_pb2.DESCRIPTOR


def test_server_status_response_exposes_load_fields() -> None:
    from segmentation_grpc import ServerStatusResponse

    response = ServerStatusResponse(
        version="0.1.0",
        in_flight_requests=2,
        cached_images=1,
        recent_latency_ms=12.5,
        inference_workers=1,
    )
    assert response.in_flight_requests == 2
    assert response.cached_images == 1
    assert response.recent_latency_ms == 12.5
