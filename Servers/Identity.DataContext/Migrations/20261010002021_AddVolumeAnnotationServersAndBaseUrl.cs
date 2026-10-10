using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVolumeAnnotationServersAndBaseUrl : Migration
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

            // Strip known service suffixes so BaseUrl is the volume service root.
            migrationBuilder.Sql(@"
UPDATE UPDATE Resource
SET BaseUrl = LEFT(BaseUrl, LEN(BaseUrl) - CHARINDEX('/', REVERSE(BaseUrl)))
WHERE ResourceTypeId = N'AnnotationServer'
  AND BaseUrl IS NOT NULL
  AND (
        BaseUrl LIKE '%/Annotation/%'
     OR BaseUrl LIKE '%/Annotation'
     OR BaseUrl LIKE '%/OData/%'
     OR BaseUrl LIKE '%/OData'
     OR BaseUrl LIKE '%/Export/%'
     OR BaseUrl LIKE '%/Export'
  );

UPDATE WHILE 1 = 1
BEGIN
    UPDATE Resource
    SET BaseUrl = LEFT(BaseUrl, LEN(BaseUrl) - CHARINDEX('/', REVERSE(BaseUrl)))
    WHERE ResourceTypeId = N'AnnotationServer'
      AND BaseUrl IS NOT NULL
      AND (
            LOWER(RIGHT(BaseUrl, 10)) = '/annotation'
         OR LOWER(RIGHT(BaseUrl, 6)) = '/odata'
         OR LOWER(RIGHT(BaseUrl, 7)) = '/export'
         OR BaseUrl LIKE '%/Service.svc'
      );
    IF @@ROWCOUNT = 0 BREAK;
END

UPDATE UPDATE Resource
SET BaseUrl = LEFT(BaseUrl, LEN(BaseUrl) - 1)
WHERE ResourceTypeId = N'AnnotationServer'
  AND BaseUrl IS NOT NULL
  AND RIGHT(BaseUrl, 1) = '/';
");

            migrationBuilder.CreateTable(
                name: "VolumeAnnotationServers",
                columns: table => new
                {
                    VolumeId = table.Column<long>(type: "bigint", nullable: false),
                    AnnotationServerId = table.Column<long>(type: "bigint", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolumeAnnotationServers", x => new { x.VolumeId, x.AnnotationServerId });
                    table.ForeignKey(
                        name: "FK_VolumeAnnotationServers_Resource_AnnotationServerId",
                        column: x => x.AnnotationServerId,
                        principalTable: "Resource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolumeAnnotationServers_Resource_VolumeId",
                        column: x => x.VolumeId,
                        principalTable: "Resource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
INSERT INTO VolumeAnnotationServers (VolumeId, AnnotationServerId, IsDefault)
SELECT v.Id, v.AnnotationServerId, 1
FROM Resource v
WHERE v.ResourceTypeId IN (N'Volume', N'AnnotationContext')
  AND v.AnnotationServerId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM VolumeAnnotationServers l
      WHERE l.VolumeId = v.Id AND l.AnnotationServerId = v.AnnotationServerId);
");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_BaseUrl",
                table: "Resource",
                column: "BaseUrl",
                unique: true,
                filter: "[BaseUrl] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeAnnotationServers_AnnotationServerId",
                table: "VolumeAnnotationServers",
                column: "AnnotationServerId");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeAnnotationServers_VolumeId_Default",
                table: "VolumeAnnotationServers",
                column: "VolumeId",
                unique: true,
                filter: "[IsDefault] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VolumeAnnotationServers");

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
