using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationalUnitAdministratorPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "PermissionId", "ResourceTypeId", "Description" },
                values: new object[] { "Administrator", "OrganizationalUnit", "Edit all aspects of an organizational unit and CRUD operations on all child resources" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumns: new[] { "PermissionId", "ResourceTypeId" },
                keyValues: new object[] { "Administrator", "OrganizationalUnit" });
        }
    }
}
