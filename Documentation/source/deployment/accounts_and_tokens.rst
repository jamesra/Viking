###################
Accounts and tokens
###################

Viking rows of the shared accounts table; the full table (Docker, GitHub, Nornir hosts) is in the
`Nornir deployment guide`_. Values go in the files named, never in the repository.

============================  ========================================  ================================================
Item                          Where it goes                             Where to get it
============================  ========================================  ================================================
Duende IdentityServer         ``Run/viking-identity/secrets/``          Your account at duendesoftware.com (license
license                       ``Duende_License.key``                    for the production identity host).
OIDC client secrets           ``IDENTITY_SERVER_*SECRET``,              Generate locally (``openssl rand -base64 32``),
(Viking, api, mvc,            ``SBFSEM_TOOLS_CLIENT_SECRET``,           one per client. Share the sbfsem-tools secret
ro.viking, sbfsem-tools,      ``CORRECTION_SERVICE_CLIENT_SECRET``,     with its operators; enter the correction,
correction, segmentation,     ``SEGMENTATION_SERVICE_CLIENT_SECRET``,   segmentation and gallery ones in their stacks
galleries)                    ``GALLERY_*_CLIENT_SECRET``               too.
                              (viking-identity run env)
gRPC introspection client     ``IdentityServer__ClientId`` /            Register in the Identity management site;
                              ``ClientSecret`` (viking-annotation)      generate the secret locally.
SQL logins                    viking-identity, viking-annotation,       The SQL Server administrator (:doc:`sql`).
                              viking-corrections run envs; IIS
SMTP relay                    ``Run/viking-identity/secrets/``          Pitt IT (relay host, port, any credentials and
                              ``secrets.json`` (``Email``)              an allowed sender address).
ACME EAB (InCommon)           ``TLS_ACME_SERVER``, ``TLS_EAB_KID``,     Pitt IT, InCommon certificate service (ACME
                              ``TLS_EAB_HMAC_KEY`` (viking-certs)       enrollment for the connectomes.pitt.edu names).
Let's Encrypt account         ``TLS_EMAIL`` (viking-certs)              Created automatically; use a group mailbox.
DNS records                   one per ``VIKING_HOSTNAME_*``             Pitt IT / the zone owner.
Segmentation demo token       ``SEGMENTATION_DEMO_TOKEN``               Generate locally (only with the demo page on).
PFX password                  ``TLS_PFX_PASSWORD`` (viking-certs)       Optional; generate locally and pass it to
                                                                        ``Install-IisCertificate.ps1``.
============================  ========================================  ================================================

Templates for every file: ``deploy/examples/``.

.. _Nornir deployment guide: https://nornir.github.io/deployment/
