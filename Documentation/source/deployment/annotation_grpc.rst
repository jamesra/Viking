##########################
Annotation service (gRPC)
##########################

``viking-annotation`` runs ``Servers/GrpcAnnotationService/docker-compose.yml`` on the web host:
``grpc-annotation-service``, the annotation API Viking uses at ``VIKING_HOSTNAME_ANNOTATION``, and,
with profile ``local-sql``, a throwaway ``annotation-sql`` container.

Ports
=====

=================  ===========  ============================================================
Container          Host         Use
=================  ===========  ============================================================
80 (h2c)           5010         Tests and modern .NET clients (``GRPC_ANNOTATION_HTTP_PORT``)
443 (TLS)          5011         Viking; the proxy's ``VIKING_UPSTREAM_ANNOTATION``
                                (``GRPC_ANNOTATION_HTTPS_PORT``)
1433 (local-sql)   1433         ``ANNOTATION_SQL_HOST_PORT``
=================  ===========  ============================================================

Viking is .NET 4.8 and uses ``WinHttpHandler``, which cannot do unencrypted HTTP/2: its VikingXML
``Endpoint`` must be the HTTPS name (``https://annotation.<domain>/``).

TLS
===

``Program.cs`` loads ``SSL_CERT_PATH``/``SSL_KEY_PATH`` (``/certs/<TLS_CERT_NAME>/fullchain.pem`` and
``privkey.pem``) for port 443 through ``DockerTlsCertificate.TryLoad``. When the files are absent it
falls back to the image's self-signed localhost certificate, so a dev box without certificates still
starts. The container log says which one is in use. ``viking-certs`` restarts it after a renewal.

Configuration
=============

The run env (``deploy/examples/example.viking-annotation.run.env``) is also the container
environment; ASP.NET Core reads ``Section__Key`` as ``Section:Key``:

* ``ConnectionStrings__AnnotationConnection``: SQL auth (the Linux container has no Windows identity).
  Production points at the external annotation SQL Server (:doc:`sql`); ``local-sql`` uses
  ``Server=annotation-sql,1433``.
* ``IdentityServer__Endpoint`` / ``ClientId`` / ``ClientSecret``: a confidential client registered in
  Identity with introspection rights for ``Viking.Annotation``. A token from one issuer is a 401
  against another.
* ``VIKING_CORRECTIONS_DIR`` (``host.env``, shared with ``viking-corrections``): host folder whose
  ``corrections`` subfolder ``viking-corrections`` publishes (default
  ``<root>/Data/viking-corrections``). ``GRPC_ANNOTATION_CORRECTIONS_PATH`` is the older per-stack name.

One deployment per annotation database
======================================

``Servers/GrpcAnnotationService/scripts/Deploy-AnnotationGrpc.ps1`` deploys compose project
``viking-annotation-<database>`` with its own env file and port pair (``GRPC_ANNOTATION_ENV_FILE``,
``GRPC_ANNOTATION_HTTP_PORT``, ``GRPC_ANNOTATION_HTTPS_PORT``). Each extra database needs its own
hostname and proxy upstream if it is reached through the proxy.

1. Preview. ``-WhatIf`` prints the project, ports and env file and starts nothing::

     .\Servers\GrpcAnnotationService\scripts\Deploy-AnnotationGrpc.ps1 -Database RC3 -SqlServer "192.168.0.110,1433" -WhatIf

2. First run (same command without ``-WhatIf``). It creates the env file from
   ``Servers/GrpcAnnotationService/config-template/build/docker-env-template.txt`` with the database
   name (and ``-SqlServer``) filled in, then stops and lists the keys that still hold a
   ``REPLACE_WITH_`` placeholder. The file is ``D:\Docker\Builds\GrpcAnnotationService\<database>.env``
   unless ``-EnvFile`` names another; it must be outside the repo because it holds credentials.
   Fill in those keys and the ``IdentityServer__*`` values (see Configuration above).
3. Run the command again. It refuses to go on while a placeholder is left; otherwise it checks the
   configuration with ``docker compose config --quiet``, runs
   ``docker compose up -d --build grpc-annotation-service``, waits up to 120 s for the HTTPS port to
   answer and prints the endpoint (``https://localhost:<port + 1>/``). Re-running is safe.
4. ``-Status`` lists the deployed projects; ``-Database <database> -Remove`` runs ``down`` (volumes are kept).
5. Production needs ``-Environment Production -Confirm``; without ``-Confirm`` the script refuses
   (and PowerShell then asks before each step). ``-Environment`` defaults to ``Test``.

``-Port`` is the h2c port and HTTPS is ``-Port`` + 1, as in the Ports table. Omitted, a running
project keeps the pair it publishes, and a new one gets the first free pair from 5010 up (5020 is
skipped). A ``-Port`` that is already in use is refused. ``-SqlServer`` (a connection-string server such
as ``host,1433``) is used only when the env file is created; to change it later, edit the env file.
The script never changes the database schema or connects to the database.

Local test stack
================

``Servers/GrpcAnnotationService/scripts/Start-AnnotationTestStack.ps1 -ApplySchema -Build`` starts
``annotation-sql`` and the gRPC service (profile ``local-sql``), waits for SQL and applies the minimal
schema (``Servers/AnnotationDatabase/config-template/README.md``). The DevTest Identity container
(``identity-devtest``, port 5020) has been removed: its Dockerfile no longer existed.

The old root ``docker-compose.annotation-db.yml`` overlay is replaced by the ``local-sql`` profile; the
root ``docker-compose.yml`` now only includes the per-stack files.
