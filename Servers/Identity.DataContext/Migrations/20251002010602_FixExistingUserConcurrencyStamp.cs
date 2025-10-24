using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixExistingUserConcurrencyStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix existing users that may have the old static concurrency stamp
            // This updates any users with the old static concurrency stamp to have a new dynamic one
            migrationBuilder.Sql(@"
                UPDATE [AspNetUsers] 
                SET [ConcurrencyStamp] = NEWID()
                WHERE [ConcurrencyStamp] = '00000000-0000-0000-0000-000000000002' 
                   OR [ConcurrencyStamp] IS NULL
                   OR [ConcurrencyStamp] = ''
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: This migration cannot be easily rolled back as we don't know 
            // which users had the old static concurrency stamp vs new dynamic ones.
            // The concurrency stamps are now properly dynamic and should remain so.
            // If rollback is needed, the database should be restored from backup.
        }
    }
}
