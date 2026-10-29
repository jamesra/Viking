############
Segmentation
############

``viking-segmentation`` runs ``Servers/SegmentationServer/docker-compose.yml`` on the GPU compute
host. Every container is the same image with a different model and listens only with TLS.

=======================  ======================  ===============================================
Service                  Host port (TLS)         Hostname
=======================  ======================  ===============================================
segmentation-server      40443, demo 40444       ``VIKING_HOSTNAME_SEGMENTATION``,
                                                 ``..._SEGMENTATIONDEMO``
segmentation-<prefix>    per row (see below)     ``segmentation-<prefix>.<VIKING_DOMAIN>``
=======================  ======================  ===============================================

``segmentation-server`` is the default model (``SAM2_CHECKPOINT`` in the run env). Every other model
is a row of ``<root>/Run/viking-segmentation/models.json``; ``Initialize-DockerHost.ps1`` copies an
empty table there (``deploy/examples/example.viking-segmentation.models.json``). Nothing about the
other models is in the compose file, the proxy or ``host.env``: a row is the whole definition.

.. code-block:: powershell

   stack.ps1 viking-segmentation model v3 sam2_em_v3_epoch_9.pt   # add or change a server, start it
   stack.ps1 viking-segmentation model v5 v5/serve_epoch4.pt      # weights in a subfolder
   stack.ps1 viking-segmentation model v3                         # recreate it with the stored weights
   stack.ps1 viking-segmentation model                            # every row
   stack.ps1 viking-segmentation model -Remove v3                 # delete the row and its container

``model`` runs ``Servers/SegmentationServer/rebuild-and-start.ps1`` (``-h`` lists everything). It
checks the weights file, updates ``models.json``, writes ``compose.generated.yaml`` next to it
(merged by ``stack.ps1`` after ``docker-compose.yml``; each generated service extends the
``segmentation-model`` template there) and starts the selected servers. Image build or pull is
separate (``build``, ``pull``). A row has:

==========================  ===========================================================================
Field                       Meaning
==========================  ===========================================================================
``prefix``                  DNS label; the service is ``segmentation-<prefix>``
``model``                   weights file in the models folder, or a path below it
``port``                    host port when published (default: next free from 40445)
``hostname``                full host name; default ``segmentation-<prefix>.<VIKING_DOMAIN>``, one label,
                            so the shared ``*.<VIKING_DOMAIN>`` certificate covers it
``network``                 ``backend``, ``published`` or ``macvlan`` (below)
``mac``, ``ip``,            macvlan only: the DHCP-reserved MAC and IP, and the name sent with the request
``dhcpHostname``
``serviceName``             Identity SegmentationService resource (local tilesets, below)
``cacheMaxMemoryBytes``     cap on the GPU cache of encoded tiles (server default 16 GiB); set it for a
                            server that shares the GPU with training
==========================  ===========================================================================

How the proxy reaches a model server
------------------------------------

- ``backend`` (the default when ``VIKING_BACKEND_NETWORK`` is set in ``host.env``): ``viking-proxy`` runs
  on this host and shares that Docker network, so it reaches ``segmentation-<prefix>:443`` by name and
  nothing is published. Add ``stacks/viking-proxy/compose.backend.yaml`` (connectomes-deploy) to the
  proxy's ``STACK_COMPOSE_OVERLAYS``; ``stack.ps1`` creates the network when it is missing. Use this
  whenever the proxy and the server are on the same machine.
- ``published``: the server publishes host port ``port`` (and its demo page on ``port`` + 100,
  loopback unless ``SEGMENTATION_MODEL_DEMO_HOST_BIND`` says otherwise) and the proxy on another host
  reaches ``SEGMENTATION_UPSTREAM_HOST:<port>`` (default ``VIKING_COMPUTE_HOST``). Open the ports from
  the web host only.
- ``macvlan``: the server has its own MAC and IP on the LAN, like the default server in
  ``docs/segmentation-host.md`` (connectomes-deploy). The row needs ``mac`` and ``ip``, and the run env
  needs ``SEGMENTATION_MACVLAN_PARENT``, ``SEGMENTATION_LAN_SUBNET`` and ``SEGMENTATION_LAN_GATEWAY``. A
  ``segmentation-net-<prefix>`` container holds the network and the DHCP lease. The proxy reaches
  ``<ip>:443``.

The script writes the proxy's lines (``<host name> <upstream>;``) to
``<root>/Data/viking-segmentation/proxy-models.map`` and the host names to ``cert-names.names``. On
the proxy host, ``segmentation.map`` goes in ``Data/viking-proxy/models.d/`` (``viking-proxy``
includes it in its SNI map) and ``segmentation.names`` in ``Data/viking-certs/names.d/``
(``viking-certs`` adds the names to the certificate on its next cycle). When those stacks run on the
same host, set ``SEGMENTATION_PROXY_MAP_DIR`` and ``SEGMENTATION_CERT_NAMES_DIR`` in the run env and the
script writes them there. Restart ``viking-proxy`` after the map changes. Creating the DNS record for a
new name is up to you.

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
(``SEGMENTATION_MODEL_HOST``), mounted read-only at ``/models``. The default model's file is
``SAM2_CHECKPOINT``; every other model's file is the ``model`` of its row. Weights load only at
process start: after replacing a file, ``stack.ps1 restart viking-segmentation`` (or ``model
<prefix>`` for one server). Manifest format and model naming: ``Servers/SegmentationServer/README.md``.

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
- ``SEGMENTATION_SERVICE_NAME`` for the default server, and ``serviceName`` in a row of
  ``models.json`` for the others: the resource name of each container's SegmentationService.

The server reads plain greyscale cells in mosaic space from the context's VikingXML tilesets over
HTTP and keeps them in its tile store; anything else (pyramid-only sections, blended or coloured
channels, volume-space views) is still uploaded by the client. Without these settings every cell
comes from the client, as before.

TLS
===

Every container reads ``fullchain.pem`` and ``privkey.pem`` from one folder, mounted at ``/tls``, and waits
``SEGMENTATION_TLS_WAIT_SECONDS`` (120) for the files, then exits; ``restart: unless-stopped`` retries. Two
choices for the folder:

- **The certificate manager (default):** ``Certs/<TLS_CERT_NAME>/``, filled by ``viking-certs`` in any of its
  modes. The compute host gets the certificate with ``stack.ps1 certs push -From web`` (run on the dev
  box), which copies ``Certs/<TLS_CERT_NAME>/`` and runs ``certs reload`` there. Alternatively run
  ``viking-certs`` with ``TLS_MODE=manual`` on compute, which restarts these containers (label
  ``viking.tls-reload``) when a new copy arrives. See :doc:`tls`. For a certificate that lists its names
  (``letsencrypt``, ``acme-eab``), the model servers' names reach ``viking-certs`` through its
  ``names.d`` folder (above), so they are on the next request.
- **Your own files:** ``SEGMENTATION_CERT_DIR=<host folder>`` in the run env. Name the folder's two
  files ``fullchain.pem`` and ``privkey.pem``. Nothing restarts the servers when you renew: run
  ``stack.ps1 viking-segmentation restart``.

The certificate must cover every host name. ``model`` reports a name the certificate lacks (it needs
``openssl`` on the host); the server just waits for valid files.

Demo pages
==========

The default model's HTTPS demo page (container 8443, host 40444) is off unless
``SEGMENTATION_DEMO_SITE=1``; to reach it from the proxy also set ``SEGMENTATION_DEMO_BIND=0.0.0.0``,
``SEGMENTATION_DEMO_HOST_BIND=0.0.0.0`` and a ``SEGMENTATION_DEMO_TOKEN``. The model servers of
``models.json`` serve a plain-HTTP demo page on port 80 of the container; published rows map it to
host port ``port`` + 100, bound to loopback (``SEGMENTATION_MODEL_DEMO_HOST_BIND``).

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
   SEGMENTATION_MACVLAN_PARENT=eno1
   SEGMENTATION_LAN_SUBNET=10.20.30.0/24
   SEGMENTATION_LAN_GATEWAY=10.20.30.1
   SEGMENTATION_MAC_ADDRESS=02:5e:10:00:00:01
   SEGMENTATION_LAN_IP=10.20.30.40

Docker gives the container that MAC and the reserved IP. A small ``segmentation-net`` container holds the network (``segmentation-server`` shares
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

Model servers from ``models.json`` can have their own MAC and IP the same way: ``"network": "macvlan"``
with ``mac`` and ``ip`` in the row (the three ``SEGMENTATION_MACVLAN_*`` / ``SEGMENTATION_LAN_*`` network
settings above are shared). Each gets its own ``segmentation-net-<prefix>`` container and DHCP reservation.

To move ``segmentation.<domain>`` to this server, set ``VIKING_UPSTREAM_SEGMENTATION=<IP>:443`` in the web
host's ``viking-proxy`` run env and restart the proxy (or point DNS at the IP to bypass the proxy), then
stop the old host's default server.
