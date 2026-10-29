#####
Proxy
#####

``viking-proxy`` (``Servers/proxy/``) is nginx on the web host and owns host ports 80 and 443.

Port 443
   TLS passthrough for every name except Identity.
   The Identity name goes to a local listener (``templates/identity.conf.template``) that
   terminates TLS with ``/certs/<TLS_CERT_NAME>/`` and routes by path: ``/manage/`` to
   ``VIKING_UPSTREAM_IDENTITY_MANAGEMENT``, ``/webapi/`` to ``VIKING_UPSTREAM_IDENTITY_WEBAPI``,
   everything else to ``VIKING_UPSTREAM_IDENTITY`` (see :doc:`identity`). The proxy therefore mounts
   ``DOCKER_CERTS_DIR`` read-only and carries ``viking.tls-reload`` so a renewal restarts it.
   For the other names, ``ssl_preread`` reads the SNI name and the ``map`` in
   ``templates/stream.conf.template`` sends ``VIKING_HOSTNAME_<SERVICE>`` to
   ``VIKING_UPSTREAM_<SERVICE>`` (``host:port`` of the service's own TLS port). Unknown names go to
   ``VIKING_UPSTREAM_DEFAULT`` (the IIS host). Upstreams are resolved per connection, so a host that is
   down does not stop the proxy.

Port 80
   ``/.well-known/acme-challenge/`` from the ``viking-certs`` webroot (HTTP-01), ``/healthz``, a
   ``robots.txt`` that blocks indexing on the gallery names, and a 301 to https for everything else.
   ``VIKING_HTTP_UPSTREAM_WEBSVC`` (``host:port``) instead proxies plain HTTP for the websvc name, for
   Viking clients that still use ``http://`` WCF or tile URLs (a redirect would turn their POSTs into GETs).

Configuration
=============

``nginx.conf`` includes ``conf.d/stream.conf`` inside ``stream {}`` and ``conf.d/http.conf`` plus
``conf.d/identity.conf`` inside ``http {}``. The nginx image renders both from ``templates/*.template`` with envsubst at container
start (only ``VIKING_*``, ``PROXY_*`` and ``TLS_CERT_NAME``), so a change of hostname or upstream needs only
``stack.ps1 restart viking-proxy``.

Defaults (override any of them in ``Run/viking-proxy/viking-proxy.run.env``):

=====================  ==========================================  ===============================
Service                Upstream default                            Host
=====================  ==========================================  ===============================
IDENTITY               ``host.docker.internal:5001``               web (issuer, ``/``)
IDENTITY_MANAGEMENT    ``host.docker.internal:4001``               web (``/manage/``)
IDENTITY_WEBAPI        ``host.docker.internal:6001``               web (``/webapi/``)
ANNOTATION             ``host.docker.internal:5011``               web
CORRECTIONS            ``host.docker.internal:41443``              web
GALLERY, TEM, SEM      ``host.docker.internal:8442``               web (gallery-router)
SEGMENTATION           ``${VIKING_COMPUTE_HOST}:40443``            compute
SEGMENTATIONDEMO       ``${VIKING_COMPUTE_HOST}:40444``            compute
SEGMENTATION models    lines in ``models.d/segmentation.map``      compute (written by ``stack.ps1 viking-segmentation model``)
WEBSVC, DEFAULT        ``${VIKING_IIS_HOST}:443``                  iis
=====================  ==========================================  ===============================

``host.docker.internal`` is mapped to the host gateway, so it works on Linux as well as Docker Desktop.

Checks::

   curl http://127.0.0.1/healthz
   docker compose -p viking-proxy exec viking-proxy nginx -T | less
   openssl s_client -connect <host>:443 -servername annotation.<domain> </dev/null | openssl x509 -noout -subject

The routing was ported from the Nginx Proxy Manager ``stream.conf`` and ``custom/http.conf`` of the
old single-host setup. Its ``rogue1`` route and the HTTP-terminated proxy hosts are not carried over.
