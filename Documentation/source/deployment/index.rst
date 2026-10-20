#############################
Production bring-up checklist
#############################

Viking's servers run as *stacks*: one compose project each (plus one IIS deployment), listed in
``deploy/stacks.psd1`` and driven by nornir-docker's ``stack.ps1``. The host layout
(``<root>/Run``, ``Data``, ``Logs``, ``Certs``), the ``stack.ps1`` commands and remote access are
shared with Nornir and documented once in the `Nornir deployment guide`_. These pages cover only
what is specific to Viking.

Topology (``connectomes.pitt.edu``, one subdomain per service, all on port 443 through the proxy):

==========  =====================================  ===================================================
Host        OS                                     Viking stacks
==========  =====================================  ===================================================
web         Ubuntu (dedicated hardware)            viking-proxy, viking-certs, viking-identity,
                                                   viking-annotation, viking-corrections, (sam2-gallery)
compute     Ubuntu 26 with GPU                     viking-segmentation (and nornir)
iis         Windows Server, IIS                    viking-iis (WCF annotation, OData, DataExport, tiles)
dev box     Windows, Docker Desktop                any of the above, same files
==========  =====================================  ===================================================

Every hostname is a single label under ``VIKING_DOMAIN`` (``identity.``, ``websvc.``,
``annotation.``, ``corrections.``, ``segmentation.``, ``segmentationdemo.``,
``segmentation-v3.``, ``segmentation-v4.``, ``segmentation-sem-v1.``, ``gallery.``,
``gallery-tem.``, ``gallery-sem.``), so one ``*.connectomes.pitt.edu`` certificate can cover all of
them.

Before you start
================

#. DNS: an A/CNAME record for every ``VIKING_HOSTNAME_*`` pointing at the **web** host's public
   address. Ask Pitt IT if the zone is theirs.
#. Firewall on the web host: inbound 80 and 443 from anywhere; 4001 and 6001 (Identity
   management and WebApi) and 5001 (issuer, also reachable through 443) as needed. See
   :doc:`ports_and_remote_access`.
#. Accounts and tokens collected (Duende license, SQL logins, EAB key if using InCommon, SMTP):
   :doc:`accounts_and_tokens`.
#. Clone Viking on each Linux host and set ``VIKING_REPO_ROOT_HOST`` to it.

Web host
========

#. Merge ``deploy/examples/example.viking-host.env`` into ``<root>/Run/_host/host.env``
   (``VIKING_DOMAIN``, ``TLS_CERT_NAME``, ``VIKING_HOSTNAME_*``, ``VIKING_COMPUTE_HOST``,
   ``VIKING_IIS_HOST``).
#. For each stack below copy ``deploy/examples/example.<stack>.run.env`` to
   ``<root>/Run/<stack>/<stack>.run.env`` and fill in every ``# REQUIRED`` key.
#. ``stack.ps1 up viking-proxy`` and check ``http://<any hostname>/healthz`` returns ``ok``
   (:doc:`proxy`).
#. ``stack.ps1 up viking-certs``, then ``stack.ps1 certs letsencrypt`` (or ``acme-eab``). Start with
   ``TLS_STAGING=1`` until the order succeeds, then set it to ``0`` (:doc:`tls`).
   ``<root>/Certs/<TLS_CERT_NAME>/fullchain.pem`` must exist before the next steps.
#. Identity: put ``secrets.json`` and ``Duende_License.key`` in
   ``<root>/Run/viking-identity/secrets/``; copy the old ``DataProtectionKeys`` if you are moving an
   existing instance; ``stack.ps1 up viking-identity`` (:doc:`identity`).
#. ``stack.ps1 up viking-annotation`` (:doc:`annotation_grpc`) and
   ``stack.ps1 up viking-corrections`` (:doc:`corrections`).
#. Optional: the ``sam2-gallery`` stack (its compose file lives in the trainer repository, not here).

Compute host
============

#. Same ``host.env`` keys as the web host.
#. From the dev box, run ``stack.ps1 certs push -From web``. It copies ``<root>/Certs/<TLS_CERT_NAME>/``
   to every target with ``CertPush = $true`` (compute, iis) and reloads their TLS services. Re-run it
   after each renewal.
#. Put the weights and their ``<stem>.model.json`` in ``<root>/Data/viking-segmentation/models``
   (or ``SEGMENTATION_MODEL_HOST``), then ``stack.ps1 up viking-segmentation``
   (:doc:`segmentation`).

IIS host
========

#. ``stack.ps1 certs push -From web`` (above) copies ``cert.pfx`` here and runs ``certs install-iis``,
   which calls ``deploy/Install-IisCertificate.ps1``. By hand:
   ``deploy/Install-IisCertificate.ps1 -PfxPath <file> -HostHeader websvc.connectomes.pitt.edu``.
#. Deploy or update the services with ``deploy/Deploy-VikingIis.ps1`` (:doc:`iis_legacy_services`).

Verify
======

* ``https://identity.<domain>/.well-known/openid-configuration`` returns JSON with the expected issuer.
* ``https://annotation.<domain>/`` and ``https://corrections.<domain>/`` answer (gRPC services also
  answer a plain GET on ``/``).
* ``openssl s_client -connect segmentation.<domain>:443 -servername segmentation.<domain>`` shows
  the shared certificate.
* Viking opens a volume whose VikingXML points at the new endpoints.

SQL has no automated backups yet: see :doc:`sql`.

.. _Nornir deployment guide: https://nornir.github.io/deployment/
