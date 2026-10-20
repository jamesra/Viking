###
TLS
###

One certificate serves every Viking service. The ``viking-certs`` stack
(``Servers/certbot/docker-compose.yml``, script ``Servers/certbot/renew-loop.sh``) keeps one
certificate lineage named ``TLS_CERT_NAME`` and publishes it to the host's ``DOCKER_CERTS_DIR``::

   <root>/Certs/<TLS_CERT_NAME>/fullchain.pem   leaf + intermediates (what services load)
                                privkey.pem
                                cert.pem         leaf only
                                chain.pem        intermediates only
                                cert.pfx         for IIS (empty password unless TLS_PFX_PASSWORD)
                                .renewed-at      UTC time of the last change

Every TLS service mounts ``${DOCKER_CERTS_DIR}:/certs:ro`` and reads
``/certs/${TLS_CERT_NAME}/fullchain.pem`` and ``privkey.pem`` at start. Services that must restart
after a renewal carry the label ``viking.tls-reload: "true"``; the renewer restarts every running
container with that label (through the Docker socket) whenever the published files change.

The proxy never terminates TLS (port 443 is SNI passthrough), so it needs no certificate.

Modes
=====

``TLS_MODE`` in ``<root>/Run/viking-certs/viking-certs.run.env``:

``letsencrypt``
   Let's Encrypt with HTTP-01. certbot writes challenge files to the webroot
   ``<root>/Data/viking-certs/webroot``; ``viking-proxy`` serves it on port 80 for every hostname.
   Needs ``TLS_EMAIL``, DNS for every name pointing at the web host, and port 80 open.
   ``TLS_STAGING=1`` uses the staging CA (untrusted, no rate limits) while testing.

``acme-eab``
   Any ACME CA that needs External Account Binding, such as InCommon/Sectigo through Pitt IT.
   Set ``TLS_ACME_SERVER``, ``TLS_EAB_KID`` and ``TLS_EAB_HMAC_KEY`` (or put the key in
   ``Run/viking-certs/secrets/eab_hmac_key``); see ``deploy/examples/example.tls-eab.env``. The same
   webroot answers any HTTP-01 challenge the CA still asks for.

``manual``
   No ACME. ``stack.ps1 certs manual`` (or a copy from another host) writes ``fullchain.pem`` and
   ``privkey.pem`` into ``<root>/Certs/<TLS_CERT_NAME>/``. The renewer checks every
   ``TLS_MANUAL_POLL_SECONDS`` (300), derives ``cert.pem``/``chain.pem``/``cert.pfx``, writes
   ``.renewed-at`` and restarts the labelled containers when the files change. This is the mode for
   the compute host, which receives the web host's certificate.

Names on the certificate: ``TLS_HOSTNAMES`` (comma list), or every ``VIKING_HOSTNAME_*`` when empty.
With HTTP-01 each name must resolve to the web host, or the whole order fails; trim the list until
DNS exists for every name.

Commands
========

``stack.ps1 up viking-certs``
   Starts the loop: renewal check every ``TLS_RENEW_INTERVAL_HOURS`` (12), retry after 10 minutes on
   failure.

``stack.ps1 certs letsencrypt`` / ``stack.ps1 certs acme-eab``
   One issuance or renewal (``renew-loop.sh --once``); exits nonzero on failure.

By hand::

   docker compose -p viking-certs ... run --rm viking-certs --once
   docker compose -p viking-certs logs -f viking-certs
   openssl x509 -in <root>/Certs/<name>/fullchain.pem -noout -subject -ext subjectAltName -enddate

Switching modes
===============

#. Change ``TLS_MODE`` (and the mode's keys) in the run env.
#. ``stack.ps1 certs <mode>`` to issue immediately, or ``stack.ps1 restart viking-certs``.
#. The lineage keeps its name, so services need no change. Going from staging to production is
   detected (issuer contains "staging") and forces one real issuance.

Moving to ``manual`` keeps the files already in ``Certs/``; the renewer only watches them from then on.

Renewal
=======

certbot renews when fewer than 30 days remain; ``--keep-until-expiring`` makes every check
otherwise a no-op. Adding a name to ``TLS_HOSTNAMES`` (or a new ``VIKING_HOSTNAME_*``) reissues the
certificate at the next check. After a change:

* web host: the renewer restarts identity, annotation and corrections itself;
* compute host: copy ``Certs/<name>/`` across; the manual-mode renewer restarts segmentation;
* IIS host: copy ``cert.pfx`` and run ``deploy/Install-IisCertificate.ps1`` (next section).

PFX for IIS
===========

``cert.pfx`` uses 3DES/SHA1 PKCS#12 so older Windows Server builds can import it. On the IIS host::

   .\deploy\Install-IisCertificate.ps1 -PfxPath C:\certs\viking\cert.pfx -HostHeader websvc.connectomes.pitt.edu

It imports into ``LocalMachine\My``, points the https binding for that host name (or the catch-all
``*:443:`` binding; ``-CreateBinding`` adds an SNI binding) at the new certificate and prints its
serial number. The WCF AnnotationService ``web.config`` names its service certificate by serial
number (``serviceCertificate findValue``); update it when that service uses the certificate.
``deploy/Deploy-VikingIis.ps1 -Action Certificate`` does the same from the viking-iis run env.

Deprecated settings
===================

The old per-service renewers (``identity-certbot-renewer``, ``segmentation-certbot-renewer``,
``section-correction-certbot-renewer``) and their ``*-letsencrypt`` volumes are gone. Their
variables are still read when the new name is empty, with a warning in the renewer log:
``LETSENCRYPT_EMAIL`` → ``TLS_EMAIL``; ``LETSENCRYPT_PRIMARY_DOMAIN`` + ``LETSENCRYPT_ADDITIONAL_DOMAINS``
→ ``TLS_HOSTNAMES``; ``CERTBOT_AUTH_METHOD`` → ``TLS_MODE`` (``webroot`` = ``letsencrypt``);
``CERTBOT_STAGING``, ``CERTBOT_RENEW_INTERVAL_HOURS``, ``CERTBOT_KEY_TYPE``, ``CERTBOT_CERT_NAME``;
``ACME_SERVER``, ``ACME_EAB_KID``, ``ACME_EAB_HMAC_KEY``; ``CERTBOT_RESTART_CONTAINER`` (use the
label); and in manual mode ``SSL_CERT_PATH``/``SSL_KEY_PATH`` (copied into ``Certs/<name>/``).

This page replaces ``Documentation/IdentityServer-cert-renewal.md``.
