using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSegmentationServiceResourceTypeAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ResourceTypes",
                columns: new[] { "Id", "Description" },
                values: new object[] { "SegmentationService", null });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "PermissionId", "ResourceTypeId", "Description" },
                values: new object[,]
                {
                    { "Access Manager", "SegmentationService", "Manage access to segmentation service resources" },
                    { "User", "SegmentationService", "Access segmentation service resources" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "Access Manager", "SegmentationService" });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "User", "SegmentationService" });

            migrationBuilder.DeleteData(
                table: "ResourceTypes",
                keyColumn: "Id",
                keyValue: "SegmentationService");
        }
    }
}
