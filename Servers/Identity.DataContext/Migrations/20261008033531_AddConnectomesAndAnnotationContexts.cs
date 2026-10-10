using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectomesAndAnnotationContexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollaboratorInvites_Resource_VolumeId",
                table: "CollaboratorInvites");

            // Resource type Volume → AnnotationContext: keep existing rows (TPH discriminator + grants).
            migrationBuilder.InsertData(
                table: "ResourceTypes",
                columns: new[] { "Id", "Description" },
                values: new object[] { "AnnotationContext", null });

            migrationBuilder.Sql(
                "UPDATE [Permissions] SET [ResourceTypeId] = N'AnnotationContext' WHERE [ResourceTypeId] = N'Volume';");
            migrationBuilder.Sql(
                "UPDATE [Resource] SET [ResourceTypeId] = N'AnnotationContext' WHERE [ResourceTypeId] = N'Volume';");

            migrationBuilder.DeleteData(
                table: "ResourceTypes",
                keyColumn: "Id",
                keyValue: "Volume");

            migrationBuilder.RenameColumn(
                name: "VolumeId",
                table: "CollaboratorInvites",
                newName: "AnnotationContextId");

            migrationBuilder.RenameIndex(
                name: "IX_CollaboratorInvites_VolumeId",
                table: "CollaboratorInvites",
                newName: "IX_CollaboratorInvites_AnnotationContextId");

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

            migrationBuilder.AddColumn<long>(
                name: "ConnectomeId",
                table: "Resource",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DefaultAnnotationContextId",
                table: "Resource",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportUrl",
                table: "Resource",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationName",
                table: "Resource",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VolumeId",
                table: "Resource",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Volumes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    VersionLabel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PixelSpace = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConnectomeId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Volumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Volumes_Resource_ConnectomeId",
                        column: x => x.ConnectomeId,
                        principalTable: "Resource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolumeMirrors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VolumeId = table.Column<long>(type: "bigint", nullable: false),
                    VikingXmlUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    RegionLabel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    LastCheckUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastStatus = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolumeMirrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolumeMirrors_Volumes_VolumeId",
                        column: x => x.VolumeId,
                        principalTable: "Volumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ResourceTypes",
                columns: new[] { "Id", "Description" },
                values: new object[,]
                {
                    { "AnnotationServer", null },
                    { "Connectome", null }
                });

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
                name: "IX_Resource_ConnectomeId",
                table: "Resource",
                column: "ConnectomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_DefaultAnnotationContextId",
                table: "Resource",
                column: "DefaultAnnotationContextId");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_VolumeId",
                table: "Resource",
                column: "VolumeId");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeMirrors_VolumeId",
                table: "VolumeMirrors",
                column: "VolumeId");

            migrationBuilder.CreateIndex(
                name: "IX_Volumes_ConnectomeId",
                table: "Volumes",
                column: "ConnectomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Volumes_ContentHash",
                table: "Volumes",
                column: "ContentHash");

            migrationBuilder.AddForeignKey(
                name: "FK_CollaboratorInvites_Resource_AnnotationContextId",
                table: "CollaboratorInvites",
                column: "AnnotationContextId",
                principalTable: "Resource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_Resource_AnnotationServerId",
                table: "Resource",
                column: "AnnotationServerId",
                principalTable: "Resource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_Resource_ConnectomeId",
                table: "Resource",
                column: "ConnectomeId",
                principalTable: "Resource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_Resource_DefaultAnnotationContextId",
                table: "Resource",
                column: "DefaultAnnotationContextId",
                principalTable: "Resource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_Volumes_VolumeId",
                table: "Resource",
                column: "VolumeId",
                principalTable: "Volumes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollaboratorInvites_Resource_AnnotationContextId",
                table: "CollaboratorInvites");

            migrationBuilder.DropForeignKey(
                name: "FK_Resource_Resource_AnnotationServerId",
                table: "Resource");

            migrationBuilder.DropForeignKey(
                name: "FK_Resource_Resource_ConnectomeId",
                table: "Resource");

            migrationBuilder.DropForeignKey(
                name: "FK_Resource_Resource_DefaultAnnotationContextId",
                table: "Resource");

            migrationBuilder.DropForeignKey(
                name: "FK_Resource_Volumes_VolumeId",
                table: "Resource");

            migrationBuilder.DropTable(
                name: "VolumeMirrors");

            migrationBuilder.DropTable(
                name: "Volumes");

            migrationBuilder.DropIndex(
                name: "IX_Resource_AnnotationEndpoint",
                table: "Resource");

            migrationBuilder.DropIndex(
                name: "IX_Resource_AnnotationServerId",
                table: "Resource");

            migrationBuilder.DropIndex(
                name: "IX_Resource_ConnectomeId",
                table: "Resource");

            migrationBuilder.DropIndex(
                name: "IX_Resource_DefaultAnnotationContextId",
                table: "Resource");

            migrationBuilder.DropIndex(
                name: "IX_Resource_VolumeId",
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
                keyValue: "Connectome");

            migrationBuilder.DeleteData(
                table: "ResourceTypes",
                keyColumn: "Id",
                keyValue: "AnnotationServer");

            migrationBuilder.InsertData(
                table: "ResourceTypes",
                columns: new[] { "Id", "Description" },
                values: new object[] { "Volume", null });

            migrationBuilder.Sql(
                "UPDATE [Permissions] SET [ResourceTypeId] = N'Volume' WHERE [ResourceTypeId] = N'AnnotationContext';");
            migrationBuilder.Sql(
                "UPDATE [Resource] SET [ResourceTypeId] = N'Volume' WHERE [ResourceTypeId] = N'AnnotationContext';");

            migrationBuilder.DeleteData(
                table: "ResourceTypes",
                keyColumn: "Id",
                keyValue: "AnnotationContext");

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
                name: "ConnectomeId",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "DefaultAnnotationContextId",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "ExportUrl",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "RegistrationName",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "VolumeId",
                table: "Resource");

            migrationBuilder.RenameColumn(
                name: "AnnotationContextId",
                table: "CollaboratorInvites",
                newName: "VolumeId");

            migrationBuilder.RenameIndex(
                name: "IX_CollaboratorInvites_AnnotationContextId",
                table: "CollaboratorInvites",
                newName: "IX_CollaboratorInvites_VolumeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CollaboratorInvites_Resource_VolumeId",
                table: "CollaboratorInvites",
                column: "VolumeId",
                principalTable: "Resource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
