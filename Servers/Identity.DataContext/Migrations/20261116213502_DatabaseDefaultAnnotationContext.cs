using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <summary>
    /// An annotation database names its default context (<c>AnnotationServer.DefaultAnnotationContextId</c>). The value
    /// lives in the Resource column <c>DefaultAnnotationContextId</c> the connectome already uses, under the same foreign
    /// key, so there is no schema change. A database linked to exactly one context gets that context as its default; one
    /// with several (RC1) is set by an administrator.
    /// </summary>
    public partial class DatabaseDefaultAnnotationContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE s SET s.DefaultAnnotationContextId = links.OnlyContextId
FROM Resource s
JOIN (SELECT AnnotationServerId, MIN(AnnotationContextId) AS OnlyContextId
      FROM AnnotationContextServers
      GROUP BY AnnotationServerId
      HAVING COUNT(*) = 1) links ON links.AnnotationServerId = s.Id
WHERE s.ResourceTypeId = 'AnnotationServer' AND s.DefaultAnnotationContextId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Resource SET DefaultAnnotationContextId = NULL WHERE ResourceTypeId = 'AnnotationServer';");
        }
    }
}
