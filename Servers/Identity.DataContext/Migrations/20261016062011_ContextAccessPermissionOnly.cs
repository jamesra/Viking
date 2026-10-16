using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Viking.Identity.Data.Migrations
{
    /// <summary>
    /// Annotation contexts keep a single permission, Access. Read / Annotate / Review live only on annotation servers.
    /// Existing context grants are converted: the principal gets Access on the context and the same Read / Annotate /
    /// Review on every annotation server the context uses (its default server and its AnnotationContextServers links),
    /// then the old context grants and permission definitions are removed. Contexts without a server keep Access only.
    /// Every step is idempotent. Back up IdentityViking before applying.
    /// </summary>
    /// <inheritdoc />
    public partial class ContextAccessPermissionOnly : Migration
    {
        const string DataPermissions = "('Read','Annotate','Review')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "PermissionId", "ResourceTypeId", "Description" },
                values: new object[] { "Access", "AnnotationContext", "See this context in Viking and open it; data rights come from its annotation server" });

            // 1. Access on the context for everyone who held any data right on it.
            migrationBuilder.Sql($@"
INSERT INTO dbo.GrantedUserPermissions (PermissionId, ResourceId, UserId, GranteeType)
SELECT DISTINCT 'Access', g.ResourceId, g.UserId, 'User'
FROM dbo.GrantedUserPermissions g
JOIN dbo.Resource c ON c.Id = g.ResourceId AND c.ResourceTypeId = 'AnnotationContext'
WHERE g.PermissionId IN {DataPermissions}
  AND NOT EXISTS (SELECT 1 FROM dbo.GrantedUserPermissions x
                  WHERE x.PermissionId = 'Access' AND x.ResourceId = g.ResourceId AND x.UserId = g.UserId);");

            migrationBuilder.Sql($@"
INSERT INTO dbo.GrantedGroupPermissions (PermissionId, ResourceId, GroupId, GranteeType)
SELECT DISTINCT 'Access', g.ResourceId, g.GroupId, 'Group'
FROM dbo.GrantedGroupPermissions g
JOIN dbo.Resource c ON c.Id = g.ResourceId AND c.ResourceTypeId = 'AnnotationContext'
WHERE g.PermissionId IN {DataPermissions}
  AND NOT EXISTS (SELECT 1 FROM dbo.GrantedGroupPermissions x
                  WHERE x.PermissionId = 'Access' AND x.ResourceId = g.ResourceId AND x.GroupId = g.GroupId);");

            // 2. The same data rights on each annotation server the context uses.
            const string contextServers = @"
(SELECT l.AnnotationContextId AS ContextId, l.AnnotationServerId AS ServerId
   FROM dbo.AnnotationContextServers l
 UNION
 SELECT r.Id, r.AnnotationServerId
   FROM dbo.Resource r
  WHERE r.ResourceTypeId = 'AnnotationContext' AND r.AnnotationServerId IS NOT NULL)";

            migrationBuilder.Sql($@"
INSERT INTO dbo.GrantedUserPermissions (PermissionId, ResourceId, UserId, GranteeType)
SELECT DISTINCT g.PermissionId, cs.ServerId, g.UserId, 'User'
FROM dbo.GrantedUserPermissions g
JOIN {contextServers} cs ON cs.ContextId = g.ResourceId
WHERE g.PermissionId IN {DataPermissions}
  AND NOT EXISTS (SELECT 1 FROM dbo.GrantedUserPermissions x
                  WHERE x.PermissionId = g.PermissionId AND x.ResourceId = cs.ServerId AND x.UserId = g.UserId);");

            migrationBuilder.Sql($@"
INSERT INTO dbo.GrantedGroupPermissions (PermissionId, ResourceId, GroupId, GranteeType)
SELECT DISTINCT g.PermissionId, cs.ServerId, g.GroupId, 'Group'
FROM dbo.GrantedGroupPermissions g
JOIN {contextServers} cs ON cs.ContextId = g.ResourceId
WHERE g.PermissionId IN {DataPermissions}
  AND NOT EXISTS (SELECT 1 FROM dbo.GrantedGroupPermissions x
                  WHERE x.PermissionId = g.PermissionId AND x.ResourceId = cs.ServerId AND x.GroupId = g.GroupId);");

            // 3. Remove the data rights from the contexts.
            migrationBuilder.Sql($@"
DELETE g FROM dbo.GrantedUserPermissions g
JOIN dbo.Resource c ON c.Id = g.ResourceId AND c.ResourceTypeId = 'AnnotationContext'
WHERE g.PermissionId IN {DataPermissions};");

            migrationBuilder.Sql($@"
DELETE g FROM dbo.GrantedGroupPermissions g
JOIN dbo.Resource c ON c.Id = g.ResourceId AND c.ResourceTypeId = 'AnnotationContext'
WHERE g.PermissionId IN {DataPermissions};");

            // 4. Retire the permission definitions.
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "Annotate", "AnnotationContext" });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "Read", "AnnotationContext" });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "Review", "AnnotationContext" });
        }

        /// <summary>
        /// Restores the permission definitions only. The data rights now held by annotation servers stay there; the
        /// old per-context Read / Annotate / Review grants are not recreated.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "Access", "AnnotationContext" });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "PermissionId", "ResourceTypeId", "Description" },
                values: new object[,]
                {
                    { "Annotate", "AnnotationContext", null },
                    { "Read", "AnnotationContext", null },
                    { "Review", "AnnotationContext", null }
                });
        }
    }
}
