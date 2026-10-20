# Identity TLS auto-renewal (moved)

The per-service `identity-certbot-renewer` is gone. Identity, the gRPC services and segmentation now
share one certificate from the `viking-certs` stack (`Servers/certbot`), published to
`<root>/Certs/<TLS_CERT_NAME>/` and reloaded through the `viking.tls-reload` label.

See `Documentation/source/deployment/tls.rst` (modes, switching, renewal, PFX for IIS) and
`Documentation/source/deployment/identity.rst`.
