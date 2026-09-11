using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260311130000_AddVikingLaunchCodeVolumeName")]
    public partial class AddVikingLaunchCodeVolumeName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VolumeName",
                table: "VikingLaunchCodes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VolumeName",
                table: "VikingLaunchCodes");
        }
    }
}
