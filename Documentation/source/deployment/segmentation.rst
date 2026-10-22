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

Prebuilt image
==============

By default the target builds the image (``viking-segmentation-server:latest``), which takes a long
time for CUDA and torch. To build once and pull everywhere else, push it to GHCR from the machine
that built it (nornir checkout; ``docker login ghcr.io`` with a PAT that has ``write:packages``):

.. code-block:: powershell

   stack.ps1 viking-segmentation build
   nornir-docker\docker-push.ps1 -Images segmentation

This pushes ``ghcr.io/jamesra/viking-segmentation-server:latest`` and ``:<Viking short SHA>``. The
image holds no weights other than Meta's public checkpoint, and no certificates or secrets. A new
GHCR package is private, so each pulling host needs ``docker login ghcr.io`` with ``read:packages``.

On the new host, use either of these:

- ``nornir-docker/docker-pull.ps1 -Images segmentation`` pulls ``:latest`` and tags it
  ``viking-segmentation-server:latest``. Then ``stack.ps1 viking-segmentation up`` starts it without
  building.
- Set ``VIKING_SEGMENTATION_IMAGE=ghcr.io/jamesra/viking-segmentation-server:latest`` (or a SHA tag;
  ``SEGMENTATION_IMAGE`` is the older name) in the run
  env. Then ``stack.ps1 viking-segmentation up -Target compute -Images ghcr``, run from the dev box,
  pulls it first.

Use ``up`` or ``restart`` to pick up a new image, not ``rebuild``, because ``rebuild`` builds on the host.

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

Local tilesets
==============

A container can read the tiles of some annotation contexts itself instead of asking the Viking
client to render and upload every cell. In the Identity management site, give the container's
SegmentationService resource **Local resources**: annotation contexts, or the groups and
organizational units that hold them (a group can hold resources, so group contexts by where their
storage is). Then set, in the run env:

- ``SEGMENTATION_SERVICE_CLIENT_SECRET``, equal to the identity run env's secret for the
  ``segmentation`` client. The issuer and Permissions API default to the host-wide
  ``VIKING_IDENTITY_URL`` / ``VIKING_IDENTITY_API_URL``; ``SEGMENTATION_IDENTITY_ENDPOINT`` /
  ``SEGMENTATION_IDENTITY_API_ENDPOINT`` override them;
- ``SEGMENTATION_SERVICE_NAME`` (and ``SEG_V3_SERVICE_NAME`` etc. for the extra models): the
  resource name of each container's SegmentationService.

The server reads plain greyscale cells in mosaic space from the context's VikingXML tilesets over
HTTP and keeps them in its tile store; anything else (pyramid-only sections, blended or coloured
channels, volume-space views) is still uploaded by the client. Without these settings every cell
comes from the client, as before.

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

Own MAC address and IP
======================

The default server (``segmentation-server``) can run on its own MAC address and IP on the LAN instead of
the host's ports, for example to take its address from a DHCP reservation. It uses a Docker ``macvlan``
network through the overlay ``stacks/viking-segmentation/compose.macvlan.yaml`` in connectomes-deploy,
so the host runs that repo's ``stack.ps1``.

Requirements:

- A Linux host with a wired NIC on that LAN and Docker Engine 25 or newer. Docker Desktop cannot do this
  (its VM hides the container from the LAN), and Wi-Fi interfaces do not support macvlan.
- The switch port must accept a second MAC address (port security).
- A MAC address for the container and a DHCP reservation for it. Pick a locally administered MAC (first
  byte ``02``, but not ``02:42``, which Docker uses), e.g. on the host:
  ``printf 02; od -An -N5 -tx1 /dev/urandom | tr ' ' ':'``.

Then set, in ``Run/viking-segmentation/viking-segmentation.run.env`` (template block "Own MAC address
and IP"):

.. code-block:: none

   STACK_COMPOSE_OVERLAYS=stacks/viking-segmentation/compose.macvlan.yaml
   STACK_PROFILES=none
   SEGMENTATION_MACVLAN_PARENT=eno1
   SEGMENTATION_LAN_SUBNET=10.20.30.0/24
   SEGMENTATION_LAN_GATEWAY=10.20.30.1
   SEGMENTATION_MAC_ADDRESS=02:5e:10:00:00:01
   SEGMENTATION_LAN_IP=10.20.30.40

``STACK_PROFILES=none`` starts only the default server. Docker gives the container that MAC and the
reserved IP. A small ``segmentation-net`` container holds the network (``segmentation-server`` shares
it) and runs a DHCP client, so the DHCP server and any DHCP snooping on the switch see a real lease for
the MAC. Its log shows each lease; if DHCP offers a different address it logs an error and turns
unhealthy, and the container keeps the reserved IP. While no DHCP server answers, it keeps serving.
``SEGMENTATION_DHCP=0`` skips DHCP.

What differs from the default setup:

- The server listens on ``<IP>:443`` itself; nothing is published on the host, and the demo page stays
  on the container's loopback.
- macvlan traffic bypasses the host firewall (ufw and the ``DOCKER-USER`` chain); filter on the network.
- The host cannot reach the container's IP. Test from another machine, e.g.
  ``openssl s_client -connect <IP>:443 -servername segmentation.<domain>``.
- ``segmentation-server`` uses ``segmentation-net``'s network. If ``segmentation-net`` is restarted on its
  own, restart the server too; ``stack.ps1 viking-segmentation restart`` does both.

To move ``segmentation.<domain>`` to this server, set ``VIKING_UPSTREAM_SEGMENTATION=<IP>:443`` in the web
host's ``viking-proxy`` run env and restart the proxy (or point DNS at the IP to bypass the proxy), then
stop the old host's default server.
