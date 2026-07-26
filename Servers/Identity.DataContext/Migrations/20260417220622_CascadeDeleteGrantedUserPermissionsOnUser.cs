using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteGrantedUserPermissionsOnUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GrantedUserPermissions_AspNetUsers_UserId",
                table: "GrantedUserPermissions");

            migrationBuilder.AddForeignKey(
                name: "FK_GrantedUserPermissions_AspNetUsers_UserId",
                table: "GrantedUserPermissions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GrantedUserPermissions_AspNetUsers_UserId",
                table: "GrantedUserPermissions");

            migrationBuilder.AddForeignKey(
                name: "FK_GrantedUserPermissions_AspNetUsers_UserId",
                table: "GrantedUserPermissions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
