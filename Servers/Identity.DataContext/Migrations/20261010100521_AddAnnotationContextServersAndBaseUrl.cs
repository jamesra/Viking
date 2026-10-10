using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnotationContextServersAndBaseUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Resource_AnnotationEndpoint",
                table: "Resource");

            migrationBuilder.RenameColumn(
                name: "AnnotationEndpoint",
                table: "Resource",
                newName: "BaseUrl");

            // Reduce each server's stored endpoint to its service root: drop a trailing slash, then peel
            // trailing Annotation / OData / Export / *.svc segments (e.g. .../RC1/Annotation/Service.svc -> .../RC1).
            migrationBuilder.Sql(@"
UPDATE Resource
SET BaseUrl = LEFT(BaseUrl, LEN(BaseUrl) - 1)
WHERE ResourceTypeId = N'AnnotationServer'
  AND BaseUrl IS NOT NULL
  AND RIGHT(BaseUrl, 1) = '/';

WHILE 1 = 1
BEGIN
    UPDATE Resource
    SET BaseUrl = LEFT(BaseUrl, LEN(BaseUrl) - CHARINDEX('/', REVERSE(BaseUrl)))
    WHERE ResourceTypeId = N'AnnotationServer'
      AND BaseUrl IS NOT NULL
      AND CHARINDEX('/', REVERSE(BaseUrl)) > 0
      AND (
            LOWER(RIGHT(BaseUrl, 11)) = '/annotation'
         OR LOWER(RIGHT(BaseUrl, 6)) = '/odata'
         OR LOWER(RIGHT(BaseUrl, 7)) = '/export'
         OR LOWER(RIGHT(BaseUrl, 4)) = '.svc'
      );
    IF @@ROWCOUNT = 0 BREAK;
END
");

            migrationBuilder.CreateTable(
                name: "AnnotationContextServers",
                columns: table => new
                {
                    AnnotationContextId = table.Column<long>(type: "bigint", nullable: false),
                    AnnotationServerId = table.Column<long>(type: "bigint", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnotationContextServers", x => new { x.AnnotationContextId, x.AnnotationServerId });
                    table.ForeignKey(
                        name: "FK_AnnotationContextServers_Resource_AnnotationContextId",
                        column: x => x.AnnotationContextId,
                        principalTable: "Resource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnnotationContextServers_Resource_AnnotationServerId",
                        column: x => x.AnnotationServerId,
                        principalTable: "Resource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Every context that already named a default server gets that server as its default link.
            migrationBuilder.Sql(@"
INSERT INTO AnnotationContextServers (AnnotationContextId, AnnotationServerId, IsDefault)
SELECT c.Id, c.AnnotationServerId, 1
FROM Resource c
WHERE c.ResourceTypeId = N'AnnotationContext'
  AND c.AnnotationServerId IS NOT NULL;
");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_BaseUrl",
                table: "Resource",
                column: "BaseUrl",
                unique: true,
                filter: "[BaseUrl] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnnotationContextServers_AnnotationContextId_Default",
                table: "AnnotationContextServers",
                column: "AnnotationContextId",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AnnotationContextServers_AnnotationServerId",
                table: "AnnotationContextServers",
                column: "AnnotationServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnotationContextServers");

            migrationBuilder.DropIndex(
                name: "IX_Resource_BaseUrl",
                table: "Resource");

            migrationBuilder.RenameColumn(
                name: "BaseUrl",
                table: "Resource",
                newName: "AnnotationEndpoint");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_AnnotationEndpoint",
                table: "Resource",
                column: "AnnotationEndpoint",
                unique: true,
                filter: "[AnnotationEndpoint] IS NOT NULL");
        }
    }
}
