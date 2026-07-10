using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260710202000_AddCollaboratorInvites")]
    public partial class AddCollaboratorInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollaboratorInvites",
                columns: table => new
                {
                    Token = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OrganizationalUnitId = table.Column<long>(type: "bigint", nullable: false),
                    VolumeId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaboratorInvites", x => x.Token);
                    table.ForeignKey(
                        name: "FK_CollaboratorInvites_Resource_OrganizationalUnitId",
                        column: x => x.OrganizationalUnitId,
                        principalTable: "Resource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollaboratorInvites_Resource_VolumeId",
                        column: x => x.VolumeId,
                        principalTable: "Resource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaboratorInvites_ExpiresAtUtc",
                table: "CollaboratorInvites",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CollaboratorInvites_OrganizationalUnitId",
                table: "CollaboratorInvites",
                column: "OrganizationalUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaboratorInvites_VolumeId",
                table: "CollaboratorInvites",
                column: "VolumeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollaboratorInvites");
        }
    }
}
