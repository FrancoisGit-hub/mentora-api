using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot6_5_Agenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "COACH_PARAMETER_MISSED_SESSION_BEHAVIOR",
                table: "COACH_PARAMETERS",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'SKIP'");

            // Partial unique index: a booking (SESSIONS row) may be linked from at most one
            // program session per member. Not unique on SESSION_ID alone — a group session
            // legitimately links to one program session per participant.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_PROGRAM_SESSIONS_BOOKING_MEMBER"
                  ON "PROGRAM_SESSIONS" ("PROGRAM_SESSION_SESSION_ID", "PROGRAM_SESSION_MEMBER_ID")
                  WHERE "PROGRAM_SESSION_SESSION_ID" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX "IX_PROGRAM_SESSIONS_BOOKING_MEMBER";""");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_MISSED_SESSION_BEHAVIOR",
                table: "COACH_PARAMETERS");
        }
    }
}
