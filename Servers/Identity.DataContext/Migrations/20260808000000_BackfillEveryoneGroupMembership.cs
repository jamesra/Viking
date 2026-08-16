using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillEveryoneGroupMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO UserToGroupAssignments (UserId, GroupId)
SELECT u.Id, -1
FROM AspNetUsers u
WHERE NOT EXISTS (
    SELECT 1
    FROM UserToGroupAssignments a
    WHERE a.UserId = u.Id AND a.GroupId = -1
);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not remove Everyone memberships on down — they may have been created by registration.
        }
    }
}
