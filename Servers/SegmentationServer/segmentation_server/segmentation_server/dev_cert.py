"""Generate a self-signed certificate for running the server locally or in tests.

The server only listens with TLS, so a developer without a Let's Encrypt certificate needs
one. This writes a short-lived self-signed certificate and key; point ``SSL_CERT_PATH`` and
``SSL_KEY_PATH`` at them, and give clients the certificate as their trusted root
(``Example/client_example.py --ca-cert``).

    python -m segmentation_server.dev_cert --out ./dev-cert

Needs the ``cryptography`` package, which is a test/development dependency and not part of
the server image.
"""

from __future__ import annotations

import argparse
import datetime
import ipaddress
import os
from pathlib import Path
from typing import Sequence, Tuple


def generate_self_signed(
    cert_path: Path,
    key_path: Path,
    hostnames: Sequence[str] = ("localhost",),
    valid_days: int = 30,
) -> Tuple[Path, Path]:
    """Write a self-signed certificate and an unencrypted private key as PEM files.

    ``hostnames`` become DNS subject alternative names; ``127.0.0.1`` and ``::1`` are always
    added so a client dialing the loopback address verifies. The certificate is its own root.

    Returns:
        ``(cert_path, key_path)``.
    """
    try:
        from cryptography import x509
        from cryptography.hazmat.primitives import hashes, serialization
        from cryptography.hazmat.primitives.asymmetric import ec
        from cryptography.x509.oid import NameOID
    except ImportError as error:
        raise RuntimeError(
            "Generating a development certificate needs the 'cryptography' package "
            "(pip install cryptography)."
        ) from error

    key = ec.generate_private_key(ec.SECP256R1())
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, hostnames[0])])
    alt_names = [x509.DNSName(host) for host in hostnames]
    alt_names.extend(
        [x509.IPAddress(ipaddress.ip_address("127.0.0.1")), x509.IPAddress(ipaddress.ip_address("::1"))]
    )
    now = datetime.datetime.now(datetime.timezone.utc)
    certificate = (
        x509.CertificateBuilder()
        .subject_name(name)
        .issuer_name(name)
        .public_key(key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(now - datetime.timedelta(minutes=5))
        .not_valid_after(now + datetime.timedelta(days=valid_days))
        .add_extension(x509.SubjectAlternativeName(alt_names), critical=False)
        .add_extension(x509.BasicConstraints(ca=True, path_length=None), critical=True)
        .sign(key, hashes.SHA256())
    )

    cert_path.parent.mkdir(parents=True, exist_ok=True)
    key_path.parent.mkdir(parents=True, exist_ok=True)
    cert_path.write_bytes(certificate.public_bytes(serialization.Encoding.PEM))
    key_bytes = key.private_bytes(
        serialization.Encoding.PEM,
        serialization.PrivateFormat.PKCS8,
        serialization.NoEncryption(),
    )
    # Created owner-only so the unencrypted key is never readable by other users, not even briefly.
    # os.open honours the mode on POSIX; on Windows only the read-only bit applies and access is
    # governed by the directory ACL.
    flags = os.O_WRONLY | os.O_CREAT | os.O_TRUNC | getattr(os, "O_BINARY", 0)
    descriptor = os.open(key_path, flags, 0o600)
    with os.fdopen(descriptor, "wb") as key_file:
        key_file.write(key_bytes)
    return cert_path, key_path


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry: write ``cert.pem`` and ``key.pem`` into ``--out``."""
    parser = argparse.ArgumentParser(description="Generate a self-signed development certificate.")
    parser.add_argument("--out", type=Path, default=Path("dev-cert"), help="Output directory")
    parser.add_argument("--host", action="append", dest="hosts", help="DNS name (repeatable)")
    parser.add_argument("--days", type=int, default=30, help="Validity in days (default: 30)")
    args = parser.parse_args(argv)

    cert_path, key_path = generate_self_signed(
        args.out / "cert.pem",
        args.out / "key.pem",
        hostnames=tuple(args.hosts or ("localhost",)),
        valid_days=args.days,
    )
    print(f"SSL_CERT_PATH={cert_path.resolve()}")
    print(f"SSL_KEY_PATH={key_path.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
