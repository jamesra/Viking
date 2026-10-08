"""Demo HTTPS site stays down unless enabled, and its handlers call the servicer."""

from __future__ import annotations

import asyncio
import json
import threading
import urllib.error
import urllib.request
from contextlib import contextmanager
from io import BytesIO
from typing import Iterator

import grpc
import numpy as np
from PIL import Image

from segmentation_grpc import DeleteImageResponse, SegmentationResponse, SegmentResult, UploadImageResponse
from segmentation_server.demo_site import (
    DemoHttpsServer,
    demo_bind_address,
    demo_enabled,
    handle_demo_request,
    start_demo_site,
)


class FakeServicer:
    """Records in-process upload, segment, and delete calls."""

    last_upload: object
    last_segment: object
    deleted: list[int]

    def __init__(self) -> None:
        self.last_upload = None
        self.last_segment = None
        self.deleted = []

    async def UploadImage(self, request: object, context: object) -> UploadImageResponse:
        self.last_upload = request
        return UploadImageResponse(image_id=42)

    async def SegmentImage(self, request: object, context: object) -> SegmentationResponse:
        self.last_segment = request
        return SegmentationResponse(
            segments=[SegmentResult(index=1, score=0.75, mask=b"mask-png", x=3, y=4)]
        )

    async def DeleteImage(self, request: object, context: object) -> DeleteImageResponse:
        self.deleted.append(request.image_id)
        return DeleteImageResponse(success=True)


class AbortServicer:
    """Aborts upload the way SegmentationServicer does for a bad image."""

    async def UploadImage(self, request: object, context: object) -> None:
        await context.abort(grpc.StatusCode.INVALID_ARGUMENT, "width/height do not match")


def _png() -> bytes:
    image = Image.new("RGB", (2, 2), (10, 20, 30))
    buffer = BytesIO()
    image.save(buffer, format="PNG")
    return buffer.getvalue()


@contextmanager
def _running(servicer: object, **server_options: object) -> Iterator[tuple[int, object]]:
    """Serve the demo handler on plain HTTP so tests can call it without a certificate."""
    loop = asyncio.new_event_loop()
    thread = threading.Thread(target=loop.run_forever, name="demo-test-loop", daemon=True)
    thread.start()
    httpd = DemoHttpsServer(("127.0.0.1", 0), servicer, loop, ssl_context=None, **server_options)
    server_thread = threading.Thread(target=httpd.serve_forever, name="demo-test-http", daemon=True)
    server_thread.start()
    try:
        yield int(httpd.server_address[1]), servicer
    finally:
        httpd.shutdown()
        httpd.server_close()
        loop.call_soon_threadsafe(loop.stop)
        thread.join(timeout=5)


def _request(port: int, path: str, method: str = "GET", body: bytes | None = None, content_type: str | None = None, extra_headers: dict[str, str] | None = None, client_header: bool = True) -> tuple[int, dict[str, str], bytes]:
    headers = {}
    if client_header:
        headers["X-Demo-Client"] = "1"
    if content_type is not None:
        headers["Content-Type"] = content_type
    headers.update(extra_headers or {})
    request = urllib.request.Request(
        f"http://127.0.0.1:{port}{path}",
        data=body,
        method=method,
        headers=headers,
    )
    try:
        with urllib.request.urlopen(request) as response:
            return response.status, dict(response.headers), response.read()
    except urllib.error.HTTPError as exc:
        return exc.code, dict(exc.headers), exc.read()


def test_demo_enabled_defaults_off(monkeypatch) -> None:
    monkeypatch.delenv("SEGMENTATION_DEMO_SITE", raising=False)
    assert demo_enabled(None) is False


def test_demo_enabled_cli_overrides_env(monkeypatch) -> None:
    monkeypatch.setenv("SEGMENTATION_DEMO_SITE", "0")
    assert demo_enabled(True) is True
    monkeypatch.setenv("SEGMENTATION_DEMO_SITE", "1")
    assert demo_enabled(False) is False
    assert demo_enabled(None) is True


def test_start_demo_site_disabled_does_not_bind(monkeypatch) -> None:
    def boom(*args: object, **kwargs: object) -> None:
        raise AssertionError("demo server was constructed")

    monkeypatch.setattr("segmentation_server.demo_site.DemoHttpsServer", boom)
    loop = asyncio.new_event_loop()
    try:
        assert start_demo_site(enabled=False, port=8443, servicer=object(), loop=loop) is None
    finally:
        loop.close()


def test_start_demo_site_without_certificates_does_not_bind(monkeypatch) -> None:
    monkeypatch.delenv("SSL_CERT_PATH", raising=False)
    monkeypatch.delenv("SSL_KEY_PATH", raising=False)

    def boom(*args: object, **kwargs: object) -> None:
        raise AssertionError("demo server was constructed")

    monkeypatch.setattr("segmentation_server.demo_site.DemoHttpsServer", boom)
    loop = asyncio.new_event_loop()
    try:
        assert start_demo_site(enabled=True, port=8443, servicer=object(), loop=loop) is None
    finally:
        loop.close()


def test_known_missing_certificates_do_not_bind(monkeypatch) -> None:
    def boom_lookup() -> None:
        raise AssertionError("certificate lookup ran again")

    def boom_server(*args: object, **kwargs: object) -> None:
        raise AssertionError("demo server was constructed")

    monkeypatch.setattr("segmentation_server.demo_site.resolve_tls_pem_paths", boom_lookup)
    monkeypatch.setattr("segmentation_server.demo_site.DemoHttpsServer", boom_server)
    loop = asyncio.new_event_loop()
    try:
        assert start_demo_site(
            enabled=True,
            port=8443,
            servicer=object(),
            loop=loop,
            pem_paths=None,
        ) is None
    finally:
        loop.close()


def test_get_page_and_post_image_segment_delete() -> None:
    servicer = FakeServicer()
    with _running(servicer) as (port, _servicer):
        status, _headers, page = _request(port, "/")
        assert status == 200
        assert b"foreground" in page

        status, headers, png = _request(port, "/api/images", "POST", _png(), "image/png")
        assert status == 201
        assert headers["X-Image-Id"] == "42"
        assert headers["X-Image-Width"] == "2"
        assert png.startswith(b"\x89PNG")
        assert servicer.last_upload.width == 2
        assert servicer.last_upload.height == 2

        body = json.dumps({"points": [{"x": 1, "y": 1, "label": 1}, {"x": 0, "y": 0, "label": 0}]}).encode()
        status, _headers, payload = _request(port, "/api/images/42/segment", "POST", body, "application/json")
        assert status == 200
        parsed = json.loads(payload)
        assert parsed["segments"][0]["x"] == 3
        assert parsed["segments"][0]["score"] == 0.75
        assert parsed["segments"][0]["mask_png_base64"] == "bWFzay1wbmc="
        assert servicer.last_segment.image_id == 42
        assert servicer.last_segment.omit_labeled_image is True
        assert list(servicer.last_segment.labels) == [1, 0]

        status, _headers, payload = _request(port, "/api/images/42", "DELETE")
        assert status == 200
        assert json.loads(payload)["success"] is True
        assert servicer.deleted == [42]


def test_sixteen_bit_upload_is_autocontrasted_to_8bit() -> None:
    """A 16-bit upload is 8-bit RGB, with the brighter square still brighter.

    ImageJ Auto plus CLAHE must not leave the narrow 1000-1800 window crushed
    into the bottom of 8-bit, which is what a full-range convert("RGB") does.
    """
    raw = np.full((32, 32), 1000, dtype=np.uint16)
    raw[8:24, 8:24] = 1800
    source = BytesIO()
    Image.fromarray(raw).save(source, format="TIFF")
    servicer = FakeServicer()
    with _running(servicer) as (port, _servicer):
        status, _headers, png = _request(port, "/api/images", "POST", source.getvalue(), "image/tiff")
    assert status == 201
    assert png == servicer.last_upload.image_data
    shown = np.array(Image.open(BytesIO(png)))
    assert shown.dtype == np.uint8
    assert shown.shape == (32, 32, 3)
    background = int(shown[0, 0, 0])
    center = int(shown[16, 16, 0])
    assert center > background
    assert center > 128
    assert background < 80


def test_upload_abort_is_http_400() -> None:
    with _running(AbortServicer()) as (port, _servicer):
        status, _headers, payload = _request(port, "/api/images", "POST", _png(), "image/png")
        assert status == 400
        assert "width/height" in json.loads(payload)["error"]


def test_segment_requires_points() -> None:
    servicer = FakeServicer()
    status, _headers, payload = asyncio.run(
        _segment_status(servicer)
    )
    assert status == 400
    assert servicer.last_segment is None
    assert "point" in json.loads(payload)["error"]


async def _segment_status(servicer: FakeServicer) -> tuple[int, dict[str, str], bytes]:
    return await handle_demo_request(
        "POST",
        "/api/images/7/segment",
        b'{"points": []}',
        servicer,
    )

def test_demo_binds_loopback_unless_told_otherwise(monkeypatch) -> None:
    monkeypatch.delenv("SEGMENTATION_DEMO_BIND", raising=False)
    assert demo_bind_address() == "127.0.0.1"
    monkeypatch.setenv("SEGMENTATION_DEMO_BIND", "0.0.0.0")
    assert demo_bind_address() == "0.0.0.0"
    assert demo_bind_address("10.0.0.5") == "10.0.0.5"


def test_a_changing_request_without_the_client_header_is_refused() -> None:
    servicer = FakeServicer()
    with _running(servicer) as (port, _servicer):
        status, _h, payload = _request(port, "/api/images", "POST", _png(), "image/png", client_header=False)
        assert status == 403
        assert "X-Demo-Client" in json.loads(payload)["error"]
        status, _h, _p = _request(port, "/api/images/42", "DELETE", client_header=False)
        assert status == 403
        assert servicer.last_upload is None and servicer.deleted == []
        status, _h, _p = _request(port, "/", client_header=False)
        assert status == 200


def test_a_token_is_required_on_changing_requests_when_one_is_set() -> None:
    servicer = FakeServicer()
    with _running(servicer, token="s3cret") as (port, _servicer):
        assert _request(port, "/api/images", "POST", _png(), "image/png")[0] == 401
        wrong = _request(port, "/api/images", "POST", _png(), "image/png", extra_headers={"X-Demo-Token": "nope"})
        assert wrong[0] == 401
        assert servicer.last_upload is None
        right = _request(port, "/api/images", "POST", _png(), "image/png", extra_headers={"X-Demo-Token": "s3cret"})
        assert right[0] == 201
        assert _request(port, "/")[0] == 200


def _raw(port: int, request: bytes) -> bytes:
    import socket

    with socket.create_connection(("127.0.0.1", port), timeout=5) as sock:
        sock.sendall(request)
        chunks = []
        try:
            while True:
                data = sock.recv(65536)
                if not data:
                    break
                chunks.append(data)
        except socket.timeout:
            pass
    return b"".join(chunks)


def test_a_post_without_content_length_or_with_chunked_encoding_is_refused() -> None:
    with _running(FakeServicer()) as (port, _servicer):
        head = b"POST /api/images HTTP/1.1\r\nHost: x\r\nX-Demo-Client: 1\r\nConnection: close\r\n"
        assert _raw(port, head + b"\r\n").startswith(b"HTTP/1.1 411")
        chunked = head + b"Transfer-Encoding: chunked\r\n\r\n0\r\n\r\n"
        assert _raw(port, chunked).startswith(b"HTTP/1.1 411")


def test_an_invalid_or_oversized_content_length_is_refused_before_reading() -> None:
    with _running(FakeServicer()) as (port, _servicer):
        head = b"POST /api/images/42/segment HTTP/1.1\r\nHost: x\r\nX-Demo-Client: 1\r\nConnection: close\r\n"
        assert _raw(port, head + b"Content-Length: abc\r\n\r\n").startswith(b"HTTP/1.1 400")
        assert _raw(port, head + b"Content-Length: -5\r\n\r\n").startswith(b"HTTP/1.1 400")
        assert _raw(port, head + b"Content-Length: 99999999\r\n\r\n").startswith(b"HTTP/1.1 413")


def test_a_full_pool_answers_503_at_once_instead_of_queueing(monkeypatch) -> None:
    release = threading.Event()
    started = threading.Event()

    class SlowServicer(FakeServicer):
        async def DeleteImage(self, request: object, context: object) -> DeleteImageResponse:
            started.set()
            while not release.is_set():
                await asyncio.sleep(0.01)
            return DeleteImageResponse(success=True)

    results: list[int] = []
    with _running(SlowServicer(), max_workers=1, max_pending=0) as (port, _servicer):
        first = threading.Thread(target=lambda: results.append(_request(port, "/api/images/1", "DELETE")[0]))
        first.start()
        assert started.wait(5)
        status, _h, _p = _request(port, "/api/images/2", "DELETE")
        release.set()
        first.join(10)
    assert status == 503
    assert results == [200]


def test_internal_failures_are_not_echoed_to_the_browser() -> None:
    class Failing:
        async def DeleteImage(self, request: object, context: object) -> None:
            await context.abort(grpc.StatusCode.INTERNAL, "CUDA error at /home/user/secret/path.py:12")

    with _running(Failing()) as (port, _servicer):
        status, _h, payload = _request(port, "/api/images/9", "DELETE")
    assert status == 500
    assert "secret" not in json.loads(payload)["error"]


def test_a_not_found_image_gets_a_fixed_message() -> None:
    class Missing:
        async def DeleteImage(self, request: object, context: object) -> None:
            await context.abort(grpc.StatusCode.NOT_FOUND, "Image ID=9 not found")

    with _running(Missing()) as (port, _servicer):
        status, _h, payload = _request(port, "/api/images/9", "DELETE")
    assert status == 404
    assert "upload it again" in json.loads(payload)["error"]


def test_an_image_declaring_too_many_pixels_is_refused_before_it_is_decoded(monkeypatch) -> None:
    monkeypatch.setenv("SEGMENTATION_MAX_IMAGE_PIXELS", "100")
    servicer = FakeServicer()
    with _running(servicer) as (port, _servicer):
        big = Image.new("RGB", (20, 20), (1, 2, 3))
        buffer = BytesIO()
        big.save(buffer, format="PNG")
        status, _h, payload = _request(port, "/api/images", "POST", buffer.getvalue(), "image/png")
    assert status == 400
    assert "limit" in json.loads(payload)["error"]
    assert servicer.last_upload is None
