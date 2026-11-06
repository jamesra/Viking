using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <summary>
    /// Correction and segmentation services are assigned resources the same way: one ContextServiceAssignments table
    /// replaces Resource.CorrectionServiceId and SegmentationServiceLocalResources. Both are copied in before they go.
    /// </summary>
    public partial class ContextServiceAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContextServiceAssignments",
                columns: table => new
                {
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ResourceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContextServiceAssignments", x => new { x.ServiceId, x.ResourceId });
                    table.ForeignKey(
                        name: "FK_ContextServiceAssignments_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContextServiceAssignments_Resource_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContextServiceAssignments_ResourceId",
                table: "ContextServiceAssignments",
                column: "ResourceId");

            migrationBuilder.Sql(@"
INSERT INTO ContextServiceAssignments (ServiceId, ResourceId)
SELECT SegmentationServiceId, ResourceId FROM SegmentationServiceLocalResources;

INSERT INTO ContextServiceAssignments (ServiceId, ResourceId)
SELECT CorrectionServiceId, Id FROM Resource WHERE CorrectionServiceId IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_Resource_Resource_CorrectionServiceId",
                table: "Resource");

            migrationBuilder.DropTable(
                name: "SegmentationServiceLocalResources");

            migrationBuilder.DropIndex(
                name: "IX_Resource_CorrectionServiceId",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "CorrectionServiceId",
                table: "Resource");
        }

        /// <summary>
        /// Restores the two old stores. A correction service assigned a group or unit loses that row, since the old column
        /// held contexts only.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CorrectionServiceId",
                table: "Resource",
                type: "bigint",
                nullable: true);

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
                name: "IX_Resource_CorrectionServiceId",
                table: "Resource",
                column: "CorrectionServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentationServiceLocalResources_ResourceId",
                table: "SegmentationServiceLocalResources",
                column: "ResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_Resource_CorrectionServiceId",
                table: "Resource",
                column: "CorrectionServiceId",
                principalTable: "Resource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
INSERT INTO SegmentationServiceLocalResources (SegmentationServiceId, ResourceId)
SELECT a.ServiceId, a.ResourceId FROM ContextServiceAssignments a
JOIN Resource s ON s.Id = a.ServiceId WHERE s.ResourceTypeId = 'SegmentationService';

UPDATE c SET CorrectionServiceId = a.ServiceId
FROM Resource c
JOIN ContextServiceAssignments a ON a.ResourceId = c.Id
JOIN Resource s ON s.Id = a.ServiceId
WHERE s.ResourceTypeId = 'CorrectionService' AND c.ResourceTypeId = 'AnnotationContext';");

            migrationBuilder.DropTable(
                name: "ContextServiceAssignments");
        }
    }
}
