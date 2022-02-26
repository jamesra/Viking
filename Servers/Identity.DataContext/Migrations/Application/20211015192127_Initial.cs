using Microsoft.EntityFrameworkCore.Migrations;

namespace Viking.Identity.Data.Migrations.Application
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8fc96f07-55a6-4392-bb31-a692d6327673");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "cdf2b676-7edc-4d96-9ebb-8d1968734482", "b1443916-eb30-40a7-8ef0-668f4e42adf3", "ApplicationRole", "Administrator", "Administrator" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "cdf2b676-7edc-4d96-9ebb-8d1968734482");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "8fc96f07-55a6-4392-bb31-a692d6327673", "e7ff244b-a4cb-420e-8382-5a4320e4eceb", "ApplicationRole", "Administrator", "Administrator" });
        }
    }
}
