using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace IdentityServer.Data.Migrations
{
    public partial class AddNestedGroups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ParentID",
                table: "Group",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Group_ParentID",
                table: "Group",
                column: "ParentID");

            migrationBuilder.AddForeignKey(
                name: "FK_Group_Group_ParentID",
                table: "Group",
                column: "ParentID",
                principalTable: "Group",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Group_Group_ParentID",
                table: "Group");

            migrationBuilder.DropIndex(
                name: "IX_Group_ParentID",
                table: "Group");

            migrationBuilder.DropColumn(
                name: "ParentID",
                table: "Group");
        }
    }
}
