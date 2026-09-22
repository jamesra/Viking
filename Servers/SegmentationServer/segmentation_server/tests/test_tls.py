"""TLS credential loading stays optional until certificate files exist."""

from segmentation_server.server import load_server_credentials


def test_load_server_credentials_unset_paths_returns_none(monkeypatch) -> None:
    monkeypatch.delenv("SSL_CERT_PATH", raising=False)
    monkeypatch.delenv("SSL_KEY_PATH", raising=False)
    assert load_server_credentials() is None
    assert load_server_credentials(cert_path="", key_path="") is None


def test_load_server_credentials_missing_files_returns_none(tmp_path) -> None:
    missing = tmp_path / "missing.pem"
    assert load_server_credentials(cert_path=str(missing), key_path=str(missing)) is None
