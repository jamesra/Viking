using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrectionService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CorrectionServiceId",
                table: "Resource",
                type: "bigint",
                nullable: true);

            migrationBuilder.InsertData(
                table: "ResourceTypes",
                columns: new[] { "Id", "Description" },
                values: new object[] { "CorrectionService", null });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "PermissionId", "ResourceTypeId", "Description" },
                values: new object[,]
                {
                    { "Access Manager", "CorrectionService", "Manage access to correction service resources" },
                    { "User", "CorrectionService", "Use the correction service" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Resource_CorrectionServiceId",
                table: "Resource",
                column: "CorrectionServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_Resource_CorrectionServiceId",
                table: "Resource",
                column: "CorrectionServiceId",
                principalTable: "Resource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resource_Resource_CorrectionServiceId",
                table: "Resource");

            migrationBuilder.DropIndex(
                name: "IX_Resource_CorrectionServiceId",
                table: "Resource");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "Access Manager", "CorrectionService" });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "User", "CorrectionService" });

            migrationBuilder.DeleteData(
                table: "ResourceTypes",
                keyColumn: "Id",
                keyValue: "CorrectionService");

            migrationBuilder.DropColumn(
                name: "CorrectionServiceId",
                table: "Resource");
        }
    }
}
