##################
Section correction
##################

``viking-corrections`` runs ``Servers/GrpcSectionCorrectionService/docker-compose.yml`` on the web
host. The service builds residual section-correction maps for every annotation volume assigned to
it in Identity and serves them over gRPC at ``VIKING_HOSTNAME_CORRECTIONS``.

=============  ======  ============================================================================
Container      Host    Use
=============  ======  ============================================================================
80 (h2c)       41080   In-container ``correction-menu`` and local clients
443 (TLS)      41443   ``VIKING_UPSTREAM_CORRECTIONS``. Bound only once the certificate files exist;
                       until then the service serves cleartext only and logs it.
=============  ======  ============================================================================

Data (defaults under ``<root>/Data/viking-corrections``): ``corrections/`` is the published output,
also mounted read-only by ``viking-annotation`` (keep both stacks on one host, or point
``VIKING_CORRECTIONS_DIR`` in ``host.env`` at shared storage); ``cache/`` is the stos cache. Override with
``VIKING_CORRECTIONS_DIR`` (host-wide, also read by ``viking-annotation``) and ``VIKING_CORRECTIONS_CACHE_DIR``
(older names: ``GRPC_ANNOTATION_CORRECTIONS_PATH``, and ``SECTION_CORRECTION_CONFIG_PATH`` as the parent of ``cache/``).

Identity setup
==============

#. In the viking-identity run env set ``CORRECTION_SERVICE_CLIENT_SECRET`` (generate it) and restart
   Identity; this registers the ``correction`` client.
#. Put the same value in ``IdentityServer__ClientSecret`` of the corrections run env.
#. In the management site create a Correction Service resource, name it in
   ``CorrectionService__Name`` and assign annotation contexts on each context's Edit page.

SQL: ``AnnotationConnections__<Database>`` (or ``AnnotationConnectionTemplate``) gives direct SQL per
annotation database; without it the service reads through the OData endpoint Identity reports
(slower).

Rebuilds
========

``Rebuild__RunOnStartup=true`` checks every assigned volume at start.

After startup the service checks for annotation changes every ``Rebuild__CheckInterval``
(``d.hh:mm:ss``, default ``24:00:00``; ``00:00:00`` turns the check off). A check reads one
fingerprint per section (row count, newest edit, ID sums, stos file time). The joint registration
fit couples every section, so if any section changed the whole group is rebuilt; otherwise it is
left alone. A change waits for a later check until its newest edit is ``Rebuild__QuietMinutes`` old
(default 60), so an editing session is not rebuilt halfway through. A new builder version, a new
``MinLocations`` or a set from the old leave-one-out method is rebuilt regardless.

``Rebuild__Cron`` is a 5-field cron in UTC (default ``0 2 * * 0``, Sunday 02:00) that forces a
rebuild of everything, as a safety net for changes the fingerprints cannot see.

``volume.npz`` is not written at publish. ``GET /corrections/{group}/volume.npz`` builds it from
the section files on first request and caches it until the next publish.

Console menu (status and manual refresh)::

   docker compose -p viking-corrections exec section-correction-service correction-menu

*Status* shows the rebuild state and each volume's build time; *Refresh volume* rebuilds one volume
(or all) with a progress bar. ``RebuildVolume`` is refused for any caller not on loopback, and only
one rebuild runs at a time.

TLS comes from ``viking-certs`` (label ``viking.tls-reload``); the old
``section-correction-certbot-renewer`` and its Cloudflare DNS credentials are gone.
