using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot14_2_OtpAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AUTH_OTP_ATTEMPT_COUNT",
                table: "AUTH_OTPS",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Lot 14.2 (14.C1): existing rows get 0. Run docs/review/lot14/14.2/precheck.sql before deploying.
            migrationBuilder.Sql("""ALTER TABLE "AUTH_OTPS" ADD CONSTRAINT "CK_AUTH_OTPS_ATTEMPT_COUNT" CHECK ("AUTH_OTP_ATTEMPT_COUNT" >= 0);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "AUTH_OTPS" DROP CONSTRAINT IF EXISTS "CK_AUTH_OTPS_ATTEMPT_COUNT";""");

            migrationBuilder.DropColumn(
                name: "AUTH_OTP_ATTEMPT_COUNT",
                table: "AUTH_OTPS");
        }
    }
}
