using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace IdentityServer.Data.Migrations
{
    public partial class RenameOrganizationsToGroups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "Organization", schema: "dbo", newName: "Group", newSchema: "dbo");
            migrationBuilder.RenameTable(name: "OrganizationAssignments", schema: "dbo", newName: "GroupAssignments", newSchema: "dbo");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "Group", schema: "dbo", newName: "Organization", newSchema: "dbo");
            migrationBuilder.RenameTable(name: "GroupAssignments", schema: "dbo", newName: "OrganizationAssignments", newSchema: "dbo");
        }
    }
}
