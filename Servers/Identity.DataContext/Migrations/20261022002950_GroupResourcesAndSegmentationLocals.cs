using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class GroupResourcesAndSegmentationLocals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResourceToGroupAssignments",
                columns: table => new
                {
                    GroupId = table.Column<long>(type: "bigint", nullable: false),
                    ResourceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceToGroupAssignments", x => new { x.GroupId, x.ResourceId });
                    table.ForeignKey(
                        name: "FK_ResourceToGroupAssignments_Resource_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Resource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ResourceToGroupAssignments_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SegmentationServiceLocalResources",
                columns: table => new
                {
                    SegmentationServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ResourceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentationServiceLocalResources", x => new { x.SegmentationServiceId, x.ResourceId });
                    table.ForeignKey(
                        name: "FK_SegmentationServiceLocalResources_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SegmentationServiceLocalResources_Resource_SegmentationServiceId",
                        column: x => x.SegmentationServiceId,
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceToGroupAssignments_ResourceId",
                table: "ResourceToGroupAssignments",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentationServiceLocalResources_ResourceId",
                table: "SegmentationServiceLocalResources",
                column: "ResourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceToGroupAssignments");

            migrationBuilder.DropTable(
                name: "SegmentationServiceLocalResources");
        }
    }
}
