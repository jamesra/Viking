#!/usr/bin/env bash
# Compatibility shim. Older tooling (for example the segmentation rebuild-and-start.ps1 under
# D:\Docker\Builds) runs `bash /opt/identity-certbot/reload-identity.sh` inside the renewer to
# promote the lineage and restart services. The single viking-certs renewer does that in
# renew-loop.sh: publish /certs/${TLS_CERT_NAME}/ and restart containers labelled
# viking.tls-reload=true.
exec bash /opt/identity-certbot/renew-loop.sh --publish-only
