using Microsoft.EntityFrameworkCore.Migrations;

namespace Viking.Identity.Data.Migrations.Application
{
    public partial class UnknownChange : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4eec049b-6919-4cbd-97d5-264e98e69eaa");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "904f0342-6732-469d-b5e3-642272aa9391", "01963897-1dec-4736-81f9-8b90872cbcaf", "ApplicationRole", "Administrator", "Administrator" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "904f0342-6732-469d-b5e3-642272aa9391");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "4eec049b-6919-4cbd-97d5-264e98e69eaa", "26021c3c-6177-4646-8abe-f88c1bcca726", "ApplicationRole", "Administrator", "Administrator" });
        }
    }
}
