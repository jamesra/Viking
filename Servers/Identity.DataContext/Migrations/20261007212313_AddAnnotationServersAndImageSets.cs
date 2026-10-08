using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnotationServersAndImageSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnnotationDatabaseName",
                table: "Resource",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnotationEndpoint",
                table: "Resource",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AnnotationServerId",
                table: "Resource",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthenticationUrl",
                table: "Resource",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogSyncMessage",
                table: "Resource",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CatalogSyncedUtc",
                table: "Resource",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportUrl",
                table: "Resource",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImageSetId",
                table: "Resource",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationName",
                table: "Resource",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImageSets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    VersionLabel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PixelSpace = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImageSetMirrors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageSetId = table.Column<long>(type: "bigint", nullable: false),
                    VikingXmlUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    RegionLabel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    LastCheckUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastStatus = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageSetMirrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageSetMirrors_ImageSets_ImageSetId",
                        column: x => x.ImageSetId,
                        principalTable: "ImageSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ResourceTypes",
                columns: new[] { "Id", "Description" },
                values: new object[] { "AnnotationServer", null });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "PermissionId", "ResourceTypeId", "Description" },
                values: new object[,]
                {
                    { "Annotate", "AnnotationServer", "Create and edit annotations; implies Read" },
                    { "Read", "AnnotationServer", "View annotations and the images of every linked volume" },
                    { "Review", "AnnotationServer", "Review and correct annotations; implies Read" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Resource_AnnotationEndpoint",
                table: "Resource",
                column: "AnnotationEndpoint",
                unique: true,
                filter: "[AnnotationEndpoint] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_AnnotationServerId",
                table: "Resource",
                column: "AnnotationServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_ImageSetId",
                table: "Resource",
                column: "ImageSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageSetMirrors_ImageSetId",
                table: "ImageSetMirrors",
                column: "ImageSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageSets_ContentHash",
                table: "ImageSets",
                column: "ContentHash");

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_ImageSets_ImageSetId",
                table: "Resource",
                column: "ImageSetId",
                principalTable: "ImageSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_Resource_AnnotationServerId",
                table: "Resource",
                column: "AnnotationServerId",
                principalTable: "Resource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resource_ImageSets_ImageSetId",
                table: "Resource");

            migrationBuilder.DropForeignKey(
                name: "FK_Resource_Resource_AnnotationServerId",
                table: "Resource");

            migrationBuilder.DropTable(
                name: "ImageSetMirrors");

            migrationBuilder.DropTable(
                name: "ImageSets");

            migrationBuilder.DropIndex(
                name: "IX_Resource_AnnotationEndpoint",
                table: "Resource");

            migrationBuilder.DropIndex(
                name: "IX_Resource_AnnotationServerId",
                table: "Resource");

            migrationBuilder.DropIndex(
                name: "IX_Resource_ImageSetId",
                table: "Resource");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "Annotate", "AnnotationServer" });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "Read", "AnnotationServer" });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "Review", "AnnotationServer" });

            migrationBuilder.DeleteData(
                table: "ResourceTypes",
                keyColumn: "Id",
                keyValue: "AnnotationServer");

            migrationBuilder.DropColumn(
                name: "AnnotationDatabaseName",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "AnnotationEndpoint",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "AnnotationServerId",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "AuthenticationUrl",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "CatalogSyncMessage",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "CatalogSyncedUtc",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "ExportUrl",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "ImageSetId",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "RegistrationName",
                table: "Resource");
        }
    }
}
