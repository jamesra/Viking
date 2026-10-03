"""Container health check: ask the running server for its status over TLS.

``python -m segmentation_server.healthcheck`` exits 0 when ``GetServerStatus`` answers and 1
otherwise, so it can be a Docker ``HEALTHCHECK``. The server only listens with TLS and its
certificate names the public host, not ``localhost``, so the check dials loopback and overrides
the name it verifies. Trust comes first from the system roots (which hold the Let's Encrypt root),
then, for a self-signed development certificate, from the server's own certificate file.
"""

from __future__ import annotations

import os
import re
import sys
from pathlib import Path
from typing import List, Optional, Sequence

import grpc

from segmentation_grpc import SegmentationServiceStub, ServerStatusRequest

DEFAULT_PORT = 443
TIMEOUT_SECONDS = 5.0
_LIVE_PATH = re.compile(r"[\\/]live[\\/]([^\\/]+)[\\/][^\\/]+$")


def tls_port() -> int:
    """The TLS port the server listens on (SEGMENTATION_TLS_PORT, default 443)."""
    raw = os.environ.get("SEGMENTATION_TLS_PORT", "").strip()
    try:
        return int(raw) if raw else DEFAULT_PORT
    except ValueError:
        return DEFAULT_PORT


def target_name(cert_path: Optional[str]) -> str:
    """The host name the certificate was issued for.

    ``SEGMENTATION_HEALTHCHECK_HOST`` wins. Otherwise a Let's Encrypt path
    (``.../live/<domain>/fullchain.pem``) names the domain. Otherwise ``localhost``, which is
    what ``segmentation_server.dev_cert`` issues for.
    """
    configured = os.environ.get("SEGMENTATION_HEALTHCHECK_HOST", "").strip()
    if configured:
        return configured
    if cert_path:
        match = _LIVE_PATH.search(cert_path)
        if match:
            return match.group(1)
    return "localhost"


def check(port: int, cert_path: Optional[str], timeout: float = TIMEOUT_SECONDS) -> Optional[str]:
    """None when the server answers; otherwise a short reason."""
    name = target_name(cert_path)
    roots: List[Optional[bytes]] = [None]
    if cert_path and Path(cert_path).is_file():
        roots.append(Path(cert_path).read_bytes())
    reasons: List[str] = []
    for root_certificates in roots:
        credentials = grpc.ssl_channel_credentials(root_certificates=root_certificates)
        options = [("grpc.ssl_target_name_override", name)]
        try:
            with grpc.secure_channel(f"localhost:{port}", credentials, options=options) as channel:
                SegmentationServiceStub(channel).GetServerStatus(ServerStatusRequest(), timeout=timeout)
            return None
        except grpc.RpcError as error:
            reasons.append(f"{error.code().name}: {error.details()}")
    return "; ".join(reasons)


def main(argv: Optional[Sequence[str]] = None) -> int:
    reason = check(tls_port(), os.environ.get("SSL_CERT_PATH"))
    if reason is None:
        return 0
    print(f"segmentation server is not healthy: {reason}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
