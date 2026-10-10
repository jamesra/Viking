using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnotationServerPinned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AnnotationServerPinned",
                table: "Resource",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnotationServerPinned",
                table: "Resource");
        }
    }
}
