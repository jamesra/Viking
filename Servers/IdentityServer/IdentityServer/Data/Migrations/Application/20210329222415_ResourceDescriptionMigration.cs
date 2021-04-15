using Microsoft.EntityFrameworkCore.Migrations;

namespace IdentityServer.Data.Migrations.Application
{
    public partial class ResourceDescriptionMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8ee869bf-5966-4b86-8205-c8045424d1bd");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ResourceTypes",
                type: "nvarchar(max)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "4eec049b-6919-4cbd-97d5-264e98e69eaa", "26021c3c-6177-4646-8abe-f88c1bcca726", "ApplicationRole", "Administrator", "Administrator" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4eec049b-6919-4cbd-97d5-264e98e69eaa");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ResourceTypes");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "8ee869bf-5966-4b86-8205-c8045424d1bd", "949e1da8-ac91-4b43-bd22-301a7456654c", "ApplicationRole", "Administrator", "Administrator" });
        }
    }
}
