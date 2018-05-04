using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace IdentityServer.Data.Migrations
{
    public partial class AddOrgAssignments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Organization",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "Organization",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organization", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSelectedViewModel",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Selected = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSelectedViewModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationAssignments",
                columns: table => new
                {
                    OrganizationId = table.Column<long>(nullable: false),
                    UserId = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAssignments", x => new { x.OrganizationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_OrganizationAssignments_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAssignments_UserId",
                table: "OrganizationAssignments",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationAssignments");

            migrationBuilder.DropTable(
                name: "UserSelectedViewModel");

            migrationBuilder.DropTable(
                name: "Organization");

            migrationBuilder.AddColumn<string>(
                name: "Organization",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: "");
        }
    }
}
