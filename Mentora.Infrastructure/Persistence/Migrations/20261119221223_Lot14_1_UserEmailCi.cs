using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot14_1_UserEmailCi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lot 14.1 (14.A4): case-insensitive uniqueness of the email. The case-sensitive
            // IX_USERS_USER_EMAIL stays. Fails if two rows differ only by case: run
            // docs/review/lot14/14.1/precheck.sql before deploying.
            migrationBuilder.Sql("""CREATE UNIQUE INDEX "UX_USERS_EMAIL_CI" ON "USERS" (lower("USER_EMAIL"));""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "UX_USERS_EMAIL_CI";""");
        }
    }
}
