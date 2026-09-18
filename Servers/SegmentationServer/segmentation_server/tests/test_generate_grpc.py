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
    assert "rpc UploadImage" in text


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
