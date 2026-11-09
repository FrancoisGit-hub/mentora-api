using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot12_1_AdminAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADMIN_USERS",
                columns: table => new
                {
                    ADMIN_USER_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ADMIN_USER_EMAIL = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ADMIN_USER_IS_ENABLED = table.Column<bool>(type: "boolean", nullable: false),
                    ADMIN_USER_LAST_LOGIN_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ADMIN_USER_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ADMIN_USER_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADMIN_USERS", x => x.ADMIN_USER_ID);
                });

            migrationBuilder.CreateTable(
                name: "ADMIN_OTPS",
                columns: table => new
                {
                    ADMIN_OTP_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ADMIN_OTP_ADMIN_USER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ADMIN_OTP_CODE_HASH = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ADMIN_OTP_EXPIRATION_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ADMIN_OTP_IS_USED = table.Column<bool>(type: "boolean", nullable: false),
                    ADMIN_OTP_FAILED_ATTEMPTS = table.Column<int>(type: "integer", nullable: false),
                    ADMIN_OTP_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADMIN_OTPS", x => x.ADMIN_OTP_ID);
                    table.ForeignKey(
                        name: "FK_ADMIN_OTPS_ADMIN_USERS_ADMIN_OTP_ADMIN_USER_ID",
                        column: x => x.ADMIN_OTP_ADMIN_USER_ID,
                        principalTable: "ADMIN_USERS",
                        principalColumn: "ADMIN_USER_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ADMIN_REFRESH_TOKENS",
                columns: table => new
                {
                    ADMIN_REFRESH_TOKEN_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ADMIN_REFRESH_TOKEN_ADMIN_USER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ADMIN_REFRESH_TOKEN_HASH = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ADMIN_REFRESH_TOKEN_EXPIRATION_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ADMIN_REFRESH_TOKEN_IS_REVOKED = table.Column<bool>(type: "boolean", nullable: false),
                    ADMIN_REFRESH_TOKEN_REVOKED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ADMIN_REFRESH_TOKEN_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADMIN_REFRESH_TOKENS", x => x.ADMIN_REFRESH_TOKEN_ID);
                    table.ForeignKey(
                        name: "FK_ADMIN_REFRESH_TOKENS_ADMIN_USERS_ADMIN_REFRESH_TOKEN_ADMIN_~",
                        column: x => x.ADMIN_REFRESH_TOKEN_ADMIN_USER_ID,
                        principalTable: "ADMIN_USERS",
                        principalColumn: "ADMIN_USER_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ADMIN_OTPS_ADMIN_USER",
                table: "ADMIN_OTPS",
                column: "ADMIN_OTP_ADMIN_USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ADMIN_REFRESH_TOKENS_ADMIN_USER",
                table: "ADMIN_REFRESH_TOKENS",
                column: "ADMIN_REFRESH_TOKEN_ADMIN_USER_ID");

            migrationBuilder.CreateIndex(
                name: "UX_ADMIN_USERS_EMAIL",
                table: "ADMIN_USERS",
                column: "ADMIN_USER_EMAIL",
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE "ADMIN_USERS"
                ADD CONSTRAINT "CK_ADMIN_USERS_EMAIL_LOWERCASE"
                CHECK ("ADMIN_USER_EMAIL" = lower("ADMIN_USER_EMAIL"));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "ADMIN_OTPS"
                ADD CONSTRAINT "CK_ADMIN_OTPS_FAILED_ATTEMPTS"
                CHECK ("ADMIN_OTP_FAILED_ATTEMPTS" >= 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "ADMIN_OTPS" DROP CONSTRAINT "CK_ADMIN_OTPS_FAILED_ATTEMPTS";""");
            migrationBuilder.Sql("""ALTER TABLE "ADMIN_USERS" DROP CONSTRAINT "CK_ADMIN_USERS_EMAIL_LOWERCASE";""");

            migrationBuilder.DropTable(
                name: "ADMIN_OTPS");

            migrationBuilder.DropTable(
                name: "ADMIN_REFRESH_TOKENS");

            migrationBuilder.DropTable(
                name: "ADMIN_USERS");
        }
    }
}
