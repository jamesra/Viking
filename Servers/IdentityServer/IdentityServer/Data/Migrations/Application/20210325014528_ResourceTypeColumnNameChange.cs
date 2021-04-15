using Microsoft.EntityFrameworkCore.Migrations;

namespace IdentityServer.Data.Migrations.Application
{
    public partial class ResourceTypeColumnNameChange : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resource_ResourceTypes_TypeId",
                table: "Resource");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "747fdd8a-2f9e-48ee-ac5e-cec25e832765");

            migrationBuilder.RenameColumn(
                name: "TypeId",
                table: "Resource",
                newName: "ResourceTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Resource_TypeId",
                table: "Resource",
                newName: "IX_Resource_ResourceTypeId");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "8ee869bf-5966-4b86-8205-c8045424d1bd", "949e1da8-ac91-4b43-bd22-301a7456654c", "ApplicationRole", "Administrator", "Administrator" });

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_ResourceTypes_ResourceTypeId",
                table: "Resource",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resource_ResourceTypes_ResourceTypeId",
                table: "Resource");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8ee869bf-5966-4b86-8205-c8045424d1bd");

            migrationBuilder.RenameColumn(
                name: "ResourceTypeId",
                table: "Resource",
                newName: "TypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Resource_ResourceTypeId",
                table: "Resource",
                newName: "IX_Resource_TypeId");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "747fdd8a-2f9e-48ee-ac5e-cec25e832765", "1a8edd37-13a5-4bca-a4c4-1e5c487b3757", "ApplicationRole", "Administrator", "Administrator" });

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_ResourceTypes_TypeId",
                table: "Resource",
                column: "TypeId",
                principalTable: "ResourceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
