"""Optional HTTPS page for point-prompt segmentation.

gRPC already owns container ports 80 and 443, so this listener uses its own port
and the same Let's Encrypt PEMs. Browsers cannot call the gRPC service, so the
page talks to HTTP handlers that invoke SegmentationServicer on the server loop.
The site stays down unless enabled, and it stays down when the certificate files
are not on disk yet.
"""

from __future__ import annotations

import asyncio
import base64
import json
import logging
import ssl
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from io import BytesIO
from pathlib import Path
from typing import Any, Mapping, Optional
from urllib.parse import urlsplit

import cv2
import grpc
import numpy as np
from PIL import Image, ImageOps

from segmentation_grpc import (
    DeleteImageRequest,
    Point,
    SegmentationRequest,
    UploadImageRequest,
)
from segmentation_server.compile_config import env_flag_enabled
from segmentation_server.mask_utils import prepare_image_for_sam2
from segmentation_server.server import resolve_tls_pem_paths

logger = logging.getLogger(__name__)

_UNSET = object()

DEMO_SITE_ENV = "SEGMENTATION_DEMO_SITE"
DEFAULT_DEMO_PORT = 8443
_MAX_IMAGE_BYTES = 64 * 1024 * 1024
_MAX_JSON_BYTES = 1024 * 1024
_MAX_POINTS = 256
# Modes wider than 8 bits. convert("RGB") maps the full 0–65535 range, so a
# narrow electron-microscopy window becomes nearly black.
_HIGH_DEPTH_MODES = frozenset({"I", "I;16", "I;16B", "I;16L", "I;16N", "F"})
# ImageJ Brightness/Contrast Auto (ContrastAdjuster.autoAdjust): 256 bins,
# ignore a bin that holds more than a tenth of the pixels, and stop at the
# first bin from each end with more than pixelCount/5000 samples.
_IMAGEJ_HISTOGRAM_BINS = 256
_IMAGEJ_BIN_LIMIT_DIVISOR = 10
_IMAGEJ_AUTO_THRESHOLD = 5000
# OpenCV's documented CLAHE defaults. Applied after the global window so a
# single black/white point does not flatten membranes.
_CLAHE_CLIP_LIMIT = 2.0
_CLAHE_TILE_GRID = 8
_STATIC_DIR = Path(__file__).resolve().parent / "demo_static"
_STATIC_FILES = {
    "/": "index.html",
    "/index.html": "index.html",
    "/static/app.js": "app.js",
    "/static/app.css": "app.css",
}
_CONTENT_TYPES = {
    ".html": "text/html; charset=utf-8",
    ".js": "text/javascript; charset=utf-8",
    ".css": "text/css; charset=utf-8",
}
_STATUS_TO_HTTP = {
    grpc.StatusCode.INVALID_ARGUMENT: 400,
    grpc.StatusCode.NOT_FOUND: 404,
    grpc.StatusCode.UNAVAILABLE: 503,
}

DemoResult = tuple[int, dict[str, str], bytes]


class DemoAbort(Exception):
    """Raised by the in-process context when a servicer method aborts."""

    code: grpc.StatusCode
    details: str

    def __init__(self, code: grpc.StatusCode, details: str) -> None:
        super().__init__(details)
        self.code = code
        self.details = details


class DemoContext:
    """Stand-in for grpc.aio.ServicerContext.abort used by the demo handlers."""

    async def abort(self, code: grpc.StatusCode, details: str = "") -> None:
        raise DemoAbort(code, details)


class DemoHttpsServer(ThreadingHTTPServer):
    """Threading HTTP server that carries the servicer and the asyncio loop."""

    allow_reuse_address: bool = True
    servicer: Any
    loop: asyncio.AbstractEventLoop

    def __init__(
        self,
        server_address: tuple[str, int],
        servicer: Any,
        loop: asyncio.AbstractEventLoop,
        ssl_context: Optional[ssl.SSLContext] = None,
    ) -> None:
        self.servicer = servicer
        self.loop = loop
        super().__init__(server_address, DemoRequestHandler)
        if ssl_context is not None:
            self.socket = ssl_context.wrap_socket(self.socket, server_side=True)


class DemoSite:
    """A running demo listener. close() stops the accept thread."""

    _httpd: DemoHttpsServer
    _thread: threading.Thread

    def __init__(self, httpd: DemoHttpsServer, thread: threading.Thread) -> None:
        self._httpd = httpd
        self._thread = thread

    @property
    def port(self) -> int:
        return int(self._httpd.server_address[1])

    def close(self) -> None:
        """Stop accepting requests and wait for the accept thread."""
        self._httpd.shutdown()
        self._httpd.server_close()
        self._thread.join(timeout=5)


def demo_enabled(cli_value: Optional[bool]) -> bool:
    """CLI flag wins. When the flag is omitted, SEGMENTATION_DEMO_SITE defaults off."""
    if cli_value is not None:
        return cli_value
    return env_flag_enabled(DEMO_SITE_ENV, default=False)


def start_demo_site(
    *,
    enabled: bool,
    port: int,
    servicer: Any,
    loop: asyncio.AbstractEventLoop,
    pem_paths: Any = _UNSET,
) -> Optional[DemoSite]:
    """Bind the HTTPS demo site, or return None when it must stay down.

    Pass pem_paths from the gRPC listener's lookup. None means the certificate
    files are already known to be missing. Omit pem_paths to look them up here.
    """
    if not enabled:
        logger.info(
            "Demo HTTPS site is off (pass --demo-site or set %s=1)",
            DEMO_SITE_ENV,
        )
        return None
    if pem_paths is _UNSET:
        pem_paths = resolve_tls_pem_paths()
    if pem_paths is None:
        logger.info(
            "Demo HTTPS site is enabled but TLS certificates are not available; listener not started"
        )
        return None
    cert_path, key_path = pem_paths
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    try:
        context.load_cert_chain(certfile=cert_path, keyfile=key_path)
    except OSError as exc:
        logger.error("Demo HTTPS certificate could not be loaded: %s", exc)
        return None
    try:
        httpd = DemoHttpsServer(("0.0.0.0", port), servicer, loop, context)
    except OSError:
        logger.exception("Demo HTTPS site could not bind 0.0.0.0:%s", port)
        return None
    thread = threading.Thread(target=httpd.serve_forever, name="demo-https", daemon=True)
    thread.start()
    logger.info("Demo HTTPS site listening on 0.0.0.0:%s", port)
    return DemoSite(httpd, thread)


class DemoRequestHandler(BaseHTTPRequestHandler):
    """One HTTP request: static page, upload, segment, or delete."""

    server: DemoHttpsServer
    protocol_version: str = "HTTP/1.1"

    def do_GET(self) -> None:
        self._handle()

    def do_POST(self) -> None:
        self._handle()

    def do_DELETE(self) -> None:
        self._handle()

    def log_message(self, fmt: str, *args: Any) -> None:
        logger.info("%s - " + fmt, self.address_string(), *args)

    def _handle(self) -> None:
        length = _content_length(self.headers)
        limit = _MAX_IMAGE_BYTES if self.command == "POST" and urlsplit(self.path).path == "/api/images" else _MAX_JSON_BYTES
        if self.command != "GET" and length > limit:
            self._send(413, {"Content-Type": "application/json; charset=utf-8"}, _json_bytes({"error": "request body is too large"}))
            return
        body = self.rfile.read(length) if length else b""
        future = asyncio.run_coroutine_threadsafe(
            handle_demo_request(self.command, self.path, body, self.server.servicer),
            self.server.loop,
        )
        try:
            status, headers, payload = future.result(timeout=600)
        except Exception:
            logger.exception("Demo request failed")
            status, headers, payload = 500, {"Content-Type": "application/json; charset=utf-8"}, _json_bytes({"error": "demo request failed"})
        self._send(status, headers, payload)

    def _send(self, status: int, headers: Mapping[str, str], payload: bytes) -> None:
        self.send_response(status)
        for name, value in headers.items():
            self.send_header(name, value)
        self.send_header("Content-Length", str(len(payload)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(payload)


async def handle_demo_request(method: str, raw_path: str, body: bytes, servicer: Any) -> DemoResult:
    """Route one demo request to a static file or an in-process servicer call."""
    path = urlsplit(raw_path).path
    if method == "GET":
        return _static_response(path)
    if method == "POST" and path == "/api/images":
        return await _upload(body, servicer)
    if method == "POST" and path.startswith("/api/images/") and path.endswith("/segment"):
        image_id = _image_id_from_path(path[: -len("/segment")])
        if image_id is None:
            return _error(404, "unknown demo path")
        return await _segment(image_id, body, servicer)
    if method == "DELETE" and path.startswith("/api/images/"):
        image_id = _image_id_from_path(path)
        if image_id is None:
            return _error(404, "unknown demo path")
        return await _delete(image_id, servicer)
    return _error(404, "unknown demo path")


def _static_response(path: str) -> DemoResult:
    name = _STATIC_FILES.get(path)
    if name is None:
        return _error(404, "unknown demo path")
    file_path = _STATIC_DIR / name
    if not file_path.is_file():
        return _error(404, "demo page is not installed")
    content_type = _CONTENT_TYPES[file_path.suffix]
    return 200, {"Content-Type": content_type}, file_path.read_bytes()


async def _upload(body: bytes, servicer: Any) -> DemoResult:
    if not body:
        return _error(400, "image body is empty")
    try:
        png, width, height = _canonical_png(body)
    except (OSError, ValueError) as exc:
        return _error(400, f"Could not read image: {exc}")
    try:
        response = await servicer.UploadImage(
            UploadImageRequest(image_data=png, width=width, height=height),
            DemoContext(),
        )
    except DemoAbort as exc:
        return _abort_result(exc)
    return (
        201,
        {
            "Content-Type": "image/png",
            "X-Image-Id": str(response.image_id),
            "X-Image-Width": str(width),
            "X-Image-Height": str(height),
        },
        png,
    )


async def _segment(image_id: int, body: bytes, servicer: Any) -> DemoResult:
    try:
        payload = json.loads(body.decode("utf-8"))
        points = payload["points"]
    except (UnicodeDecodeError, json.JSONDecodeError, KeyError, TypeError):
        return _error(400, "segment body must be JSON with a points array")
    if not isinstance(points, list) or not points:
        return _error(400, "at least one point is required")
    if len(points) > _MAX_POINTS:
        return _error(400, f"at most {_MAX_POINTS} points")
    request = SegmentationRequest(
        image_id=image_id,
        multimask_output=False,
        omit_labeled_image=True,
    )
    for point in points:
        if not isinstance(point, dict):
            return _error(400, "each point needs x, y, and label")
        try:
            x = int(point["x"])
            y = int(point["y"])
            label = int(point["label"])
        except (KeyError, TypeError, ValueError):
            return _error(400, "each point needs integer x, y, and label")
        if label not in (0, 1) or x < 0 or y < 0:
            return _error(400, "label must be 1 (foreground) or 0 (background), and coordinates must be non-negative")
        request.coordinates.append(Point(x=x, y=y))
        request.labels.append(label)
    try:
        response = await servicer.SegmentImage(request, DemoContext())
    except DemoAbort as exc:
        return _abort_result(exc)
    segments = [
        {
            "index": segment.index,
            "score": segment.score,
            "x": segment.x,
            "y": segment.y,
            "mask_png_base64": base64.b64encode(segment.mask).decode("ascii"),
        }
        for segment in response.segments
    ]
    return _json(200, {"segments": segments})


async def _delete(image_id: int, servicer: Any) -> DemoResult:
    try:
        response = await servicer.DeleteImage(DeleteImageRequest(image_id=image_id), DemoContext())
    except DemoAbort as exc:
        return _abort_result(exc)
    return _json(200, {"success": bool(response.success)})


def _canonical_png(image_data: bytes) -> tuple[bytes, int, int]:
    """Return an 8-bit RGB PNG and the size UploadImage will check.

    A 16-bit image is leveled with ImageJ Auto and then CLAHE. The same PNG is
    stored and sent back to the browser, so clicks land on the picture the user sees.
    """
    image = Image.open(BytesIO(image_data))
    transposed = ImageOps.exif_transpose(image)
    if transposed is not None:
        image = transposed
    if _is_high_depth(image):
        image = _autocontrast_to_rgb(image)
    elif image.mode != "RGB":
        image = image.convert("RGB")
    buffer = BytesIO()
    image.save(buffer, format="PNG")
    png = buffer.getvalue()
    decoded = prepare_image_for_sam2(png)
    height, width = decoded.shape[:2]
    return png, int(width), int(height)


def _is_high_depth(image: Image.Image) -> bool:
    return image.mode in _HIGH_DEPTH_MODES or image.mode.startswith("I;")


def _autocontrast_to_rgb(image: Image.Image) -> Image.Image:
    """Level a high-bit-depth image to 8-bit RGB.

    ImageJ's Auto button sets one global black and white point, skipping a
    histogram bin that contains more than a tenth of the pixels so a flat
    background does not pin the range. CLAHE then restores local contrast
    inside that window. OpenCV's defaults are clip limit 2.0 and an 8×8 grid.
    """
    leveled = _level_uint8(np.asarray(image))
    if leveled.ndim == 2:
        display = Image.fromarray(_clahe(leveled), mode="L")
    else:
        channels = [_clahe(leveled[..., index]) for index in range(leveled.shape[-1])]
        display = Image.fromarray(np.stack(channels, axis=-1), mode="RGB")
    if display.mode != "RGB":
        return display.convert("RGB")
    return display


def _level_uint8(array: np.ndarray) -> np.ndarray:
    """Apply ImageJ Auto independently on each plane and return uint8."""
    values = np.asarray(array)
    if values.ndim == 3 and values.shape[-1] >= 3:
        channels = [_level_plane(values[..., index]) for index in range(3)]
        return np.stack(channels, axis=-1)
    if values.ndim == 3 and values.shape[-1] == 1:
        values = values[..., 0]
    return _level_plane(values)


def _level_plane(values: np.ndarray) -> np.ndarray:
    """Map one plane through the ImageJ Auto black and white points."""
    samples = np.asarray(values, dtype=np.float64)
    low, high = _imagej_display_limits(samples)
    if not np.isfinite(low) or not np.isfinite(high) or high <= low:
        return np.full(samples.shape, 128, dtype=np.uint8)
    scaled = (samples - low) * (255.0 / (high - low))
    scaled = np.nan_to_num(scaled, nan=0.0, posinf=255.0, neginf=0.0)
    return np.clip(scaled, 0, 255).astype(np.uint8)


def _imagej_display_limits(samples: np.ndarray) -> tuple[float, float]:
    """Return the black and white points ImageJ's Auto button would use.

    The histogram is 256 bins across the finite min/max, matching
    ``getRawStatistics`` for 16-bit and 32-bit images. A bin with more than
    ``pixelCount/10`` samples is ignored. The scan stops at the first remaining
    bin with more than ``pixelCount/5000`` samples. If that scan collapses,
    the limits fall back to the finite min and max.
    """
    finite = samples[np.isfinite(samples)]
    if finite.size == 0:
        return 0.0, 1.0
    hist_min = float(finite.min())
    hist_max = float(finite.max())
    if hist_max <= hist_min:
        return hist_min, hist_min
    counts, _edges = np.histogram(finite, bins=_IMAGEJ_HISTOGRAM_BINS, range=(hist_min, hist_max))
    pixel_count = int(finite.size)
    limit = pixel_count // _IMAGEJ_BIN_LIMIT_DIVISOR
    threshold = pixel_count // _IMAGEJ_AUTO_THRESHOLD
    low_bin = _scan_histogram_bin(counts, limit, threshold, reverse=False)
    high_bin = _scan_histogram_bin(counts, limit, threshold, reverse=True)
    if high_bin < low_bin:
        return hist_min, hist_max
    bin_size = (hist_max - hist_min) / float(_IMAGEJ_HISTOGRAM_BINS)
    low = hist_min + low_bin * bin_size
    high = hist_min + high_bin * bin_size
    if high <= low:
        return hist_min, hist_max
    return low, high


def _scan_histogram_bin(counts: np.ndarray, limit: int, threshold: int, *, reverse: bool) -> int:
    """Walk ImageJ's 256-bin histogram from one end until a bin qualifies."""
    if reverse:
        index = _IMAGEJ_HISTOGRAM_BINS
        while True:
            index -= 1
            count = int(counts[index])
            if count > limit:
                count = 0
            if count > threshold or index <= 0:
                return index
    index = -1
    while True:
        index += 1
        count = int(counts[index])
        if count > limit:
            count = 0
        if count > threshold or index >= _IMAGEJ_HISTOGRAM_BINS - 1:
            return index


def _clahe(plane: np.ndarray) -> np.ndarray:
    """Run CLAHE on one uint8 plane. OpenCV requires a contiguous buffer."""
    source = np.ascontiguousarray(plane, dtype=np.uint8)
    operator = cv2.createCLAHE(
        clipLimit=_CLAHE_CLIP_LIMIT,
        tileGridSize=(_CLAHE_TILE_GRID, _CLAHE_TILE_GRID),
    )
    return operator.apply(source)


def _image_id_from_path(path: str) -> Optional[int]:
    prefix = "/api/images/"
    if not path.startswith(prefix):
        return None
    token = path[len(prefix):]
    if not token.isdigit():
        return None
    return int(token)


def _abort_result(exc: DemoAbort) -> DemoResult:
    status = _STATUS_TO_HTTP.get(exc.code, 500)
    return _error(status, exc.details or "segmentation request failed")


def _error(status: int, message: str) -> DemoResult:
    return _json(status, {"error": message})


def _json(status: int, payload: dict[str, Any]) -> DemoResult:
    return status, {"Content-Type": "application/json; charset=utf-8"}, _json_bytes(payload)


def _json_bytes(payload: dict[str, Any]) -> bytes:
    return json.dumps(payload).encode("utf-8")


def _content_length(headers: Mapping[str, str]) -> int:
    raw = headers.get("Content-Length", "0")
    try:
        length = int(raw)
    except (TypeError, ValueError):
        return 0
    return max(0, length)
