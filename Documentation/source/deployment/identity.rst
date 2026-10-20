########
Identity
########

``viking-identity`` runs ``Servers/IdentityServer/docker-compose-all.yml``: one container with the
Duende IdentityServer issuer (5001), the permissions WebApi (6001) and the management site (4001)
under supervisor, plus a one-shot ``identity-init`` that creates the state folders and hands them to
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

The appsettings files still name ``identity.codepharm.net``. The compose file overrides them from
``VIKING_HOSTNAME_IDENTITY``: ``VikingIdentityServerOptions__Authority`` and the JWT/introspection
authorities default to ``https://<identity host>:5001/``, ``ManagementPublicUrl`` to ``:4001`` and
``WebApiOptions__BaseUrl`` to ``:6001``. Set ``IDENTITY_AUTHORITY``,
``IDENTITY_MANAGEMENT_PUBLIC_URL`` or ``IDENTITY_WEBAPI_PUBLIC_URL`` to differ. Port 443 on the
identity name reaches 5001 through the proxy; 4001 and 6001 are reached directly.

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
