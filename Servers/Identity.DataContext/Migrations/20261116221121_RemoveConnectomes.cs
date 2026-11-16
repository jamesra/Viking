using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <summary>
    /// Connectomes are gone: a grouping record nothing read (no permissions, no clients). Annotation databases name their
    /// default context instead (DatabaseDefaultAnnotationContext). The connectome rows and every row pointing at them
    /// (grants, group membership, service assignments) are deleted; Down restores the schema, not the rows.
    /// </summary>
    public partial class RemoveConnectomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resource_Resource_ConnectomeId",
                table: "Resource");

            migrationBuilder.DropForeignKey(
                name: "FK_Volumes_Resource_ConnectomeId",
                table: "Volumes");

            migrationBuilder.DropIndex(
                name: "IX_Volumes_ConnectomeId",
                table: "Volumes");

            migrationBuilder.DropIndex(
                name: "IX_Resource_ConnectomeId",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "ConnectomeId",
                table: "Volumes");

            migrationBuilder.DropColumn(
                name: "ConnectomeId",
                table: "Resource");

            migrationBuilder.Sql(@"
DELETE FROM GrantedUserPermissions WHERE ResourceId IN (SELECT Id FROM Resource WHERE ResourceTypeId = 'Connectome');
DELETE FROM GrantedGroupPermissions WHERE ResourceId IN (SELECT Id FROM Resource WHERE ResourceTypeId = 'Connectome');
DELETE FROM ResourceToGroupAssignments WHERE ResourceId IN (SELECT Id FROM Resource WHERE ResourceTypeId = 'Connectome');
DELETE FROM ContextServiceAssignments WHERE ResourceId IN (SELECT Id FROM Resource WHERE ResourceTypeId = 'Connectome');
DELETE FROM Resource WHERE ResourceTypeId = 'Connectome';");

            migrationBuilder.DeleteData(
                table: "ResourceTypes",
                keyColumn: "Id",
                keyValue: "Connectome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ConnectomeId",
                table: "Volumes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ConnectomeId",
                table: "Resource",
                type: "bigint",
                nullable: true);

            migrationBuilder.InsertData(
                table: "ResourceTypes",
                columns: new[] { "Id", "Description" },
                values: new object[] { "Connectome", null });

            migrationBuilder.CreateIndex(
                name: "IX_Volumes_ConnectomeId",
                table: "Volumes",
                column: "ConnectomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_ConnectomeId",
                table: "Resource",
                column: "ConnectomeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_Resource_ConnectomeId",
                table: "Resource",
                column: "ConnectomeId",
                principalTable: "Resource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Volumes_Resource_ConnectomeId",
                table: "Volumes",
                column: "ConnectomeId",
                principalTable: "Resource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
