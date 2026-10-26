"""Demo HTTPS site stays down unless enabled, and its handlers call the servicer."""

from __future__ import annotations

import asyncio
import base64
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

from segmentation_grpc import (
    GridCell,
    IntPoint,
    IntSize,
    SegmentationResponse,
    SegmentResult,
    SegmentTilesStreamResponse,
    TilesNeeded,
    UploadTileResponse,
)
from segmentation_server.demo_site import (
    DemoHttpsServer,
    demo_bind_address,
    demo_enabled,
    demo_plaintext,
    demo_port_for,
    handle_demo_request,
    start_demo_site,
)


class FakeServicer:
    """Records the tile uploads and the stream the demo drives, and answers like the real servicer.

    The stream asks for the start tiles and one tile off the picture, then returns one island: a 2 x 2-sample object
    whose top-left pixel is the first foreground click.
    """

    def __init__(self) -> None:
        self.uploads: list = []
        self.starts: list = []
        self.answers: list = []

    async def UploadTile(self, request: object, context: object) -> UploadTileResponse:
        self.uploads.append(request)
        return UploadTileResponse()

    async def SegmentTilesStream(self, requests, context):
        first = await requests.__anext__()
        start = first.start
        self.starts.append(start)
        yield SegmentTilesStreamResponse(needed=TilesNeeded(tiles=[*start.tiles, GridCell(row=9, col=9)]))
        answer = await requests.__anext__()
        self.answers.append(answer.answer)
        click = start.foreground_feature.points[0]
        logits = np.full((4, 4), -5.0, dtype="<f2")
        logits[1:3, 1:3] = 5.0
        segment = SegmentResult(
            island=1,
            score=0.75,
            logits=logits.tobytes(),
            logits_size=IntSize(width=4, height=4),
            # Sample (1, 1)'s top-left pixel is the clicked pixel.
            logits_origin=IntPoint(x=int(click.x) - 4, y=int(click.y) + 4),
            logits_scale=4.0,
        )
        yield SegmentTilesStreamResponse(result=SegmentationResponse(segments=[segment], request_id=0))


def _png(size: int = 2) -> bytes:
    image = Image.new("RGB", (size, size), (10, 20, 30))
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

def test_plaintext_flag_and_default_port(monkeypatch) -> None:
    monkeypatch.delenv("SEGMENTATION_DEMO_PLAINTEXT", raising=False)
    assert demo_plaintext(None) is False
    monkeypatch.setenv("SEGMENTATION_DEMO_PLAINTEXT", "1")
    assert demo_plaintext(None) is True
    assert demo_plaintext(False) is False
    assert demo_port_for(None, plaintext=True) == 80
    assert demo_port_for(None, plaintext=False) == 8443
    assert demo_port_for(9000, plaintext=True) == 9000


def test_plaintext_site_serves_http_without_certificates(monkeypatch) -> None:
    monkeypatch.delenv("SSL_CERT_PATH", raising=False)
    monkeypatch.delenv("SSL_KEY_PATH", raising=False)
    loop = asyncio.new_event_loop()
    loop_thread = threading.Thread(target=loop.run_forever, name="demo-test-loop", daemon=True)
    loop_thread.start()
    site = start_demo_site(
        enabled=True,
        port=0,
        servicer=FakeServicer(),
        loop=loop,
        bind="127.0.0.1",
        plaintext=True,
    )
    try:
        assert site is not None
        status, _headers, page = _request(site.port, "/")
        assert status == 200
        assert b"foreground" in page
    finally:
        if site is not None:
            site.close()
        loop.call_soon_threadsafe(loop.stop)
        loop_thread.join(timeout=5)
        loop.close()


def test_get_page_and_post_image_segment_delete() -> None:
    servicer = FakeServicer()
    with _running(servicer) as (port, _servicer):
        status, _headers, page = _request(port, "/")
        assert status == 200
        assert b"foreground" in page

        status, headers, png = _request(port, "/api/images", "POST", _png(32), "image/png")
        assert status == 201
        image_id = headers["X-Image-Id"]
        assert image_id.isdigit()
        assert headers["X-Image-Width"] == "32"
        assert png.startswith(b"\x89PNG")
        assert servicer.uploads == [], "nothing reaches the server until a segment asks for tiles"

        body = json.dumps({"points": [{"x": 10, "y": 12, "label": 1}, {"x": 0, "y": 0, "label": 0}]}).encode()
        status, _headers, payload = _request(port, f"/api/images/{image_id}/segment", "POST", body, "application/json")
        assert status == 200
        parsed = json.loads(payload)
        (segment,) = parsed["segments"]
        assert segment["island"] == 1 and segment["score"] == 0.75
        assert 8 <= segment["x"] <= 12 and 10 <= segment["y"] <= 14
        assert Image.open(BytesIO(base64.b64decode(segment["mask_png_base64"]))).size[0] >= 4
        (start,) = servicer.starts
        # Image pixel (10, 12) on a one-tile grid is grid pixel (10, 1024 - 1 - 12), Y up; the click is its centre.
        assert (start.foreground_feature.points[0].x, start.foreground_feature.points[0].y) == (10.5, 1011.5)
        assert len(start.background_feature.points) == 1
        assert [(cell.row, cell.col) for cell in start.tiles] == [(0, 0)]
        assert [(upload.cell.row, upload.cell.col) for upload in servicer.uploads] == [(0, 0)]
        assert np.array(Image.open(BytesIO(servicer.uploads[0].image_data))).shape[:2] == (1024, 1024)
        assert [(cell.row, cell.col) for cell in servicer.answers[0].unavailable] == [(9, 9)]

        status, _headers, payload = _request(port, f"/api/images/{image_id}", "DELETE")
        assert status == 200
        assert json.loads(payload)["success"] is True
        status, _headers, payload = _request(port, f"/api/images/{image_id}/segment", "POST", body, "application/json")
        assert status == 404


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
    shown = np.array(Image.open(BytesIO(png)))
    assert shown.dtype == np.uint8
    assert shown.shape == (32, 32, 3)
    background = int(shown[0, 0, 0])
    center = int(shown[16, 16, 0])
    assert center > background
    assert center > 128
    assert background < 80


def test_an_unreadable_upload_is_http_400() -> None:
    with _running(FakeServicer()) as (port, _servicer):
        status, _headers, payload = _request(port, "/api/images", "POST", b"not an image", "image/png")
        assert status == 400
        assert "Could not read image" in json.loads(payload)["error"]


def test_segment_requires_points() -> None:
    servicer = FakeServicer()
    status, _headers, payload = asyncio.run(
        _segment_status(servicer)
    )
    assert status == 400
    assert servicer.starts == []
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
        assert servicer.uploads == [] and servicer.starts == []
        status, _h, _p = _request(port, "/", client_header=False)
        assert status == 200


def test_a_token_is_required_on_changing_requests_when_one_is_set() -> None:
    servicer = FakeServicer()
    with _running(servicer, token="s3cret") as (port, _servicer):
        assert _request(port, "/api/images", "POST", _png(), "image/png")[0] == 401
        wrong = _request(port, "/api/images", "POST", _png(), "image/png", extra_headers={"X-Demo-Token": "nope"})
        assert wrong[0] == 401
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


def _upload_picture(port: int) -> str:
    status, headers, _png_bytes = _request(port, "/api/images", "POST", _png(32), "image/png")
    assert status == 201
    return headers["X-Image-Id"]


_SEGMENT_BODY = json.dumps({"points": [{"x": 3, "y": 3, "label": 1}]}).encode()


def test_a_full_pool_answers_503_at_once_instead_of_queueing(monkeypatch) -> None:
    release = threading.Event()
    started = threading.Event()

    class SlowServicer(FakeServicer):
        async def SegmentTilesStream(self, requests, context):
            started.set()
            while not release.is_set():
                await asyncio.sleep(0.01)
            async for update in FakeServicer.SegmentTilesStream(self, requests, context):
                yield update

    results: list[int] = []
    with _running(SlowServicer(), max_workers=1, max_pending=0) as (port, _servicer):
        image_id = _upload_picture(port)
        path = f"/api/images/{image_id}/segment"
        first = threading.Thread(
            target=lambda: results.append(_request(port, path, "POST", _SEGMENT_BODY, "application/json")[0])
        )
        first.start()
        assert started.wait(5)
        status, _h, _p = _request(port, path, "POST", _SEGMENT_BODY, "application/json")
        release.set()
        first.join(10)
    assert status == 503
    assert results == [200]


def test_internal_failures_are_not_echoed_to_the_browser() -> None:
    class Failing(FakeServicer):
        async def SegmentTilesStream(self, requests, context):
            await context.abort(grpc.StatusCode.INTERNAL, "CUDA error at /home/user/secret/path.py:12")
            yield SegmentTilesStreamResponse()

    with _running(Failing()) as (port, _servicer):
        image_id = _upload_picture(port)
        status, _h, payload = _request(port, f"/api/images/{image_id}/segment", "POST", _SEGMENT_BODY, "application/json")
    assert status == 500
    assert "secret" not in json.loads(payload)["error"]


def test_a_not_found_image_gets_a_fixed_message() -> None:
    with _running(FakeServicer()) as (port, _servicer):
        status, _h, payload = _request(port, "/api/images/999999/segment", "POST", _SEGMENT_BODY, "application/json")
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
    assert servicer.uploads == []
