###
SQL
###

All production databases are on an external SQL Server; no Viking stack runs SQL in production.

======================  ===============================================  =====================================
Consumer                Setting                                          Run env
======================  ===============================================  =====================================
Identity                ``SQL_SERVER_HOST``, ``SQL_SERVER_PORT``,         ``viking-identity``
                        ``SQL_SERVER_USER``, ``SQL_SERVER_PASSWORD``,
                        ``SQL_SERVER_IDENTITY_DB``, ``_CONFIG_DB``,
                        ``_GRANTS_DB`` (compose builds the three
                        connection strings)
gRPC annotation         ``ConnectionStrings__AnnotationConnection``      ``viking-annotation``
Section correction      ``AnnotationConnections__<Database>``,            ``viking-corrections``
                        ``AnnotationConnectionTemplate`` (optional;
                        OData fallback otherwise)
IIS services            *VikingDatabaseConnection*,                      IIS application settings
                        *VikingApplicationServices*                      (:doc:`iis_legacy_services`)
======================  ===============================================  =====================================

Containers are Linux, so they use SQL logins, not Windows authentication; add
``TrustServerCertificate=True`` unless the SQL Server presents a trusted certificate. The SQL host
must be reachable from the web host (TCP 1433 by default). Identity waits up to
``SQL_WAIT_TIMEOUT_SECONDS`` (900) for SQL at start, for a database VM that boots later.

Local SQL (``local-sql``)
=========================

``stack.ps1 up viking-annotation`` with profile ``local-sql`` (or
``Start-AnnotationTestStack.ps1``) adds ``annotation-sql`` (SQL Server 2022 Developer, container
``viking-annotation-sql``, host port 1433, sa password ``MSSQL_SA_PASSWORD``) in a named Docker
volume. Point ``ConnectionStrings__AnnotationConnection`` at ``Server=annotation-sql,1433``. It is for
integration tests and development only. Schema: ``Servers/AnnotationDatabase/config-template/README.md``.

Backups
=======

There are **no automated backups yet**, for the external databases or the ``local-sql`` volume.
Until there are, take manual ``BACKUP DATABASE`` copies before migrations and upgrades, and keep
``<root>/Data/viking-identity/DataProtectionKeys`` with them.
