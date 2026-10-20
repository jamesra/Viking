########
Identity
########

``viking-identity`` runs ``Servers/IdentityServer/docker-compose-all.yml``: one container with the
Duende IdentityServer issuer (5001), the permissions WebApi (6001) and the management site (4001)
under supervisor. Publicly all three share ``https://<identity host>/`` on 443 through
``viking-proxy`` (see `Public URLs`_), plus a one-shot ``identity-init`` that creates the state folders and hands them to
the app user (uid 1654).

Files
=====

========================================================  ======================================================
Path                                                      Content
========================================================  ======================================================
``Run/viking-identity/viking-identity.run.env``           SQL, client secrets, ports (``example.viking-identity.run.env``).
                                                          Also the container environment.
``Run/viking-identity/secrets/secrets.json``              SMTP (``Email`` section; ``example.secrets.json``). Loaded
                                                          last by the management site, so it overrides everything.
``Run/viking-identity/secrets/Duende_License.key``        Duende license.
``Data/viking-identity/DataProtectionKeys``               ASP.NET data-protection key ring. Back it up; losing it
                                                          signs everyone out.
``Logs/viking-identity/identity-*``                       Per-process logs.
``Certs/<TLS_CERT_NAME>/``                                Shared certificate (``SSL__CertificatePath``,
                                                          ``SSL__KeyPath``).
========================================================  ======================================================

Public URLs
===========

Everything is served on port 443 at one origin. ``viking-proxy`` terminates TLS for the identity
name (``templates/identity.conf.template``) and routes by path:

.. list-table::
   :header-rows: 1

   * - Public URL
     - App (container port)
     - Path base
   * - ``https://<identity host>/`` (``/connect``, ``/.well-known``, ``/Account/Logout``, ...)
     - IdentityServerStandalone, the issuer (5001)
     - none
   * - ``https://<identity host>/manage/``
     - management site (4001)
     - ``/manage``
   * - ``https://<identity host>/webapi/``
     - permissions WebApi (6001)
     - ``/webapi``

One origin keeps the shared login cookie working: the management site issues it and the issuer must
see it. The management site and WebApi strip their prefix with ``UsePathBase``
(``IDENTITY_MANAGEMENT_PATH_BASE`` / ``IDENTITY_WEBAPI_PATH_BASE``), and still answer on their own
ports without it.

The appsettings files still name ``identity.codepharm.net`` with ports. The compose file overrides
them from ``VIKING_HOSTNAME_IDENTITY``: the issuer (``IssuerUri``) and the JWT/introspection
authorities become ``https://<identity host>/``, ``ManagementPublicUrl`` ``…/manage/`` (so the login
page is ``…/manage/Account/Login``) and ``WebApiOptions__BaseUrl`` ``…/webapi/``. To go back to
direct-port URLs set ``IDENTITY_AUTHORITY``, ``IDENTITY_MANAGEMENT_PUBLIC_URL`` and
``IDENTITY_WEBAPI_PUBLIC_URL`` and set both path bases empty.

Consumers that need the new URLs:

* ``viking-annotation`` / ``viking-corrections``: ``IdentityServer__Endpoint=https://<identity host>/``
  and ``IdentityServer__ApiEndpoint=https://<identity host>/webapi/``.
* SAM2 gallery: OIDC authority ``https://<identity host>/``; its callback URIs are
  ``VikingIdentityServerOptions__GalleryTemRedirectUris__0`` / ``…Sem…`` in the identity run env.
* VikingXML ``IdentityApi`` / ``Authentication``: ``https://<identity host>/``. Viking desktop clients
  derive the WebApi as ``/webapi/`` on the same host when the issuer is on port 443
  (``Viking.Common.IdentityEndpoints``); **clients built before this change** look for ``:6001``
  instead and must be updated.
* The sbfsem-tools bounce URL is now ``https://<identity host>/manage/SbfsemOpen/Redirect``.

Moving an existing instance
===========================

#. Copy the old ``Servers/IdentityServer/DataProtectionKeys/*.xml`` (or the old host's folder) into
   ``Data/viking-identity/DataProtectionKeys`` before the first start.
#. Copy the values from the old ``D:\Docker\Builds\IdentityServer\.env.All`` into the run env and
   ``secrets.json`` from ``D:\Docker\mounted-configs\IdentityServer``.
#. Clients registered with redirect URIs on the old hostname need their URIs updated in the management site.

Changes from the old layout
===========================

* No ``identity-certbot-renewer`` and no ``identity-letsencrypt`` volume: the shared ``viking-certs``
  renewer restarts this container (label ``viking.tls-reload``).
* The ACME HTTP-01 webroot mount (``/app/identity-server/wwwroot/.well-known/acme-challenge``) and
  the ``Acme:ChallengePath`` setting it never matched are removed; the proxy answers HTTP-01.
* The ``.env.All`` files are no longer mounted as ``/app/.env``; the run env reaches every process
  through ``env_file``.
* The tracked private keys in ``Servers/IdentityServer/certs`` and the ``./certs:/seed-certs`` mount
  are gone, as are ``scripts/acquire-certificate.sh``, ``scripts/start-services.sh`` (the image
  builds its own), ``setup-secrets.ps1`` and ``setup-ssl.ps1``.
* The remote-debug port 4024 is published on loopback unless ``REMOTE_DEBUG_HOST_BIND`` says otherwise.

Checks::

   curl -s https://identity.<domain>/.well-known/openid-configuration | head
   docker compose -p viking-identity logs -f identity-all-services
