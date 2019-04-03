using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace IdentityServer.Data.Migrations
{
    public partial class AddNestedGroups2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupAssignments_Group_OrganizationId",
                table: "GroupAssignments");

            migrationBuilder.RenameColumn(
                name: "OrganizationId",
                table: "GroupAssignments",
                newName: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupAssignments_Group_GroupId",
                table: "GroupAssignments",
                column: "GroupId",
                principalTable: "Group",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupAssignments_Group_GroupId",
                table: "GroupAssignments");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "GroupAssignments",
                newName: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupAssignments_Group_OrganizationId",
                table: "GroupAssignments",
                column: "OrganizationId",
                principalTable: "Group",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
