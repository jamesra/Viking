using Microsoft.EntityFrameworkCore.Migrations;

namespace Viking.Identity.Data.Migrations.Application
{
    public partial class AddVolumeResourceType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "904f0342-6732-469d-b5e3-642272aa9391");

            migrationBuilder.AddColumn<string>(
                name: "Endpoint",
                table: "Resource",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "5c97bae4-55c0-427d-aff3-823dc7a81e7d", "f078e06d-caf5-4eba-bfe5-7f8df4c6a9b9", "ApplicationRole", "Administrator", "Administrator" });

            migrationBuilder.InsertData(
                table: "ResourceTypes",
                columns: new[] { "Id", "Description" },
                values: new object[] { "Resource", null });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5c97bae4-55c0-427d-aff3-823dc7a81e7d");

            migrationBuilder.DeleteData(
                table: "ResourceTypes",
                keyColumn: "Id",
                keyValue: "Resource");

            migrationBuilder.DropColumn(
                name: "Endpoint",
                table: "Resource");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "904f0342-6732-469d-b5e3-642272aa9391", "01963897-1dec-4736-81f9-8b90872cbcaf", "ApplicationRole", "Administrator", "Administrator" });
        }
    }
}
