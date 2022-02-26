using Microsoft.EntityFrameworkCore.Migrations;

namespace Viking.Identity.Data.Migrations.Application
{
    public partial class AddOrgUnitResourceType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5c97bae4-55c0-427d-aff3-823dc7a81e7d");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "8fc96f07-55a6-4392-bb31-a692d6327673", "e7ff244b-a4cb-420e-8382-5a4320e4eceb", "ApplicationRole", "Administrator", "Administrator" });

            migrationBuilder.InsertData(
                table: "ResourceTypes",
                columns: new[] { "Id", "Description" },
                values: new object[] { "OrganizationalUnit", null });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8fc96f07-55a6-4392-bb31-a692d6327673");

            migrationBuilder.DeleteData(
                table: "ResourceTypes",
                keyColumn: "Id",
                keyValue: "OrganizationalUnit");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "5c97bae4-55c0-427d-aff3-823dc7a81e7d", "f078e06d-caf5-4eba-bfe5-7f8df4c6a9b9", "ApplicationRole", "Administrator", "Administrator" });
        }
    }
}
