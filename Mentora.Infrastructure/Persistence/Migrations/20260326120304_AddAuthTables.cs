using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "USERS",
                columns: table => new
                {
                    USER_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    USER_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    USER_MODIFICATION_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    USER_EMAIL = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    USER_PSW = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    USER_LOGIN = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    USER_IS_ENABLED = table.Column<bool>(type: "boolean", nullable: false),
                    USER_DISABLED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    USER_ROLE = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USERS", x => x.USER_ID);
                });

            migrationBuilder.CreateTable(
                name: "AUTH_OTPS",
                columns: table => new
                {
                    AUTH_OTP_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AUTH_OTP_CODE_HASH = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AUTH_OTP_EXPIRATION_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AUTH_OTP_IS_USED = table.Column<bool>(type: "boolean", nullable: false),
                    AUTH_OTP_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    USER_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUTH_OTPS", x => x.AUTH_OTP_ID);
                    table.ForeignKey(
                        name: "FK_AUTH_OTPS_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "USERS",
                        principalColumn: "USER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AUTH_REFRESH_TOKENS",
                columns: table => new
                {
                    AUTH_REFRESH_TOKEN_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AUTH_REFRESH_TOKEN_HASH = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AUTH_REFRESH_TOKEN_EXPIRATION_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AUTH_REFRESH_TOKEN_IS_REVOKED = table.Column<bool>(type: "boolean", nullable: false),
                    AUTH_REFRESH_TOKEN_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AUTH_REFRESH_TOKEN_REVOKED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    USER_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUTH_REFRESH_TOKENS", x => x.AUTH_REFRESH_TOKEN_ID);
                    table.ForeignKey(
                        name: "FK_AUTH_REFRESH_TOKENS_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "USERS",
                        principalColumn: "USER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "COACHES",
                columns: table => new
                {
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    COACH_FIRST_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    COACH_LAST_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    COACH_PHONE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    COACH_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    COACH_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false),
                    USER_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COACHES", x => x.COACH_ID);
                    table.ForeignKey(
                        name: "FK_COACHES_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "USERS",
                        principalColumn: "USER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MEMBERS",
                columns: table => new
                {
                    MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MEMBER_FIRST_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MEMBER_LAST_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MEMBER_PHONE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MEMBER_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MEMBER_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false),
                    MEMBER_HAS_ACTIVATED = table.Column<bool>(type: "boolean", nullable: false),
                    MEMBER_ACTIVATION_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    USER_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEMBERS", x => x.MEMBER_ID);
                    table.ForeignKey(
                        name: "FK_MEMBERS_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "USERS",
                        principalColumn: "USER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AGENT_TOKENS",
                columns: table => new
                {
                    AGENT_TOKEN_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AGENT_TOKEN_HASH = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AGENT_TOKEN_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AGENT_TOKEN_EXPIRATION_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AGENT_TOKEN_IS_REVOKED = table.Column<bool>(type: "boolean", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AGENT_TOKENS", x => x.AGENT_TOKEN_ID);
                    table.ForeignKey(
                        name: "FK_AGENT_TOKENS_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MEMBER_COACHES",
                columns: table => new
                {
                    MEMBER_COACH_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    IS_PRIMARY = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    STARTED_AT = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEMBER_COACHES", x => x.MEMBER_COACH_ID);
                    table.ForeignKey(
                        name: "FK_MEMBER_COACHES_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MEMBER_COACHES_MEMBERS_MEMBER_ID",
                        column: x => x.MEMBER_ID,
                        principalTable: "MEMBERS",
                        principalColumn: "MEMBER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AGENT_TOKENS_COACH_ID",
                table: "AGENT_TOKENS",
                column: "COACH_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AUTH_OTPS_USER_ID",
                table: "AUTH_OTPS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AUTH_REFRESH_TOKENS_USER_ID",
                table: "AUTH_REFRESH_TOKENS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_COACHES_USER_ID",
                table: "COACHES",
                column: "USER_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MEMBER_COACHES_COACH_ID",
                table: "MEMBER_COACHES",
                column: "COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MEMBER_COACHES_MEMBER_ID_COACH_ID",
                table: "MEMBER_COACHES",
                columns: new[] { "MEMBER_ID", "COACH_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MEMBERS_USER_ID",
                table: "MEMBERS",
                column: "USER_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USERS_USER_EMAIL",
                table: "USERS",
                column: "USER_EMAIL",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AGENT_TOKENS");

            migrationBuilder.DropTable(
                name: "AUTH_OTPS");

            migrationBuilder.DropTable(
                name: "AUTH_REFRESH_TOKENS");

            migrationBuilder.DropTable(
                name: "MEMBER_COACHES");

            migrationBuilder.DropTable(
                name: "COACHES");

            migrationBuilder.DropTable(
                name: "MEMBERS");

            migrationBuilder.DropTable(
                name: "USERS");
        }
    }
}
