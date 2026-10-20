############
Segmentation
############

``viking-segmentation`` runs ``Servers/SegmentationServer/docker-compose.yml`` on the GPU compute
host. Every container is the same image with a different model and listens only with TLS.

=======================  =============  ======================  ===============================
Service                  Profile        Host port (TLS)         Hostname
=======================  =============  ======================  ===============================
segmentation-server      (always)       40443, demo 40444       ``VIKING_HOSTNAME_SEGMENTATION``,
                                                                ``..._SEGMENTATIONDEMO``
segmentation-v3          seg-v3         40445                   ``VIKING_HOSTNAME_SEG_V3``
segmentation-v4          seg-v4         40446                   ``VIKING_HOSTNAME_SEG_V4``
segmentation-sem-v1      seg-sem-v1     40447                   ``VIKING_HOSTNAME_SEG_SEM_V1``
=======================  =============  ======================  ===============================

Profile ``models`` turns on all three extra models; ``stack.ps1`` uses it by default
(``Profiles`` in ``deploy/stacks.psd1``). Default hostnames are ``segmentation-v3.<domain>`` etc.,
single labels so the wildcard certificate covers them. The proxy reaches the compute host as
``VIKING_COMPUTE_HOST``; open 40443-40447 (and 40444 if the demo page is used) from the web host only.

This stack replaces the ``docker-compose.models.yml`` that ``rebuild-and-start.ps1`` generated from
``models.json`` and the ``segmentation-server-v4`` / ``segmentation-certbot-renewer`` services of
the old root compose file.

Models
======

Weights and their ``<stem>.model.json`` manifests go in ``<root>/Data/viking-segmentation/models``
(``SEGMENTATION_MODEL_HOST``), mounted read-only at ``/models``. Pick the file per container with
``SAM2_CHECKPOINT`` (default model), ``SEG_V3_CHECKPOINT``, ``SEG_V4_CHECKPOINT`` and
``SEG_SEM_V1_CHECKPOINT``. Weights load only at process start: after replacing a file,
``stack.ps1 restart viking-segmentation``. Manifest format and model naming:
``Servers/SegmentationServer/README.md``.

Caches
======

Each container has its own torch inductor and tile-embedding cache under
``<root>/Data/viking-segmentation/cache/segmentation-<name>-{inductor,embeddings}``
(``SEGMENTATION_CACHE_DIR``), because a server deletes other checkpoints' folders in its cache. The
one-shot ``segmentation-init`` creates the folders and gives them to the image's non-root user. On
the Windows dev box, set ``SEGMENTATION_CACHE_DIR=D:/Docker/cache`` to keep the old model caches.

TLS
===

The containers read ``/certs/<TLS_CERT_NAME>/`` and wait ``SEGMENTATION_TLS_WAIT_SECONDS`` (120) for
the files, then exit; ``restart: unless-stopped`` retries. The compute host gets the certificate with
``stack.ps1 certs push -From web`` (run on the dev box), which copies ``Certs/<TLS_CERT_NAME>/`` and runs
``certs reload`` there. Alternatively run ``viking-certs`` with ``TLS_MODE=manual`` on compute,
which restarts these containers (label ``viking.tls-reload``) when a new copy arrives. See :doc:`tls`.

Demo pages
==========

The default model's HTTPS demo page (container 8443, host 40444) is off unless
``SEGMENTATION_DEMO_SITE=1``; to reach it from the proxy also set ``SEGMENTATION_DEMO_BIND=0.0.0.0``,
``SEGMENTATION_DEMO_HOST_BIND=0.0.0.0`` and a ``SEGMENTATION_DEMO_TOKEN``. The extra models serve a
plain-HTTP demo page on host 40545-40547, bound to loopback (``SEGMENTATION_MODEL_DEMO_HOST_BIND``).
