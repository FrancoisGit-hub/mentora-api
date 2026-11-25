using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot14_9b_Notifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NOTIFICATIONS",
                columns: table => new
                {
                    NOTIFICATION_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    NOTIFICATION_USER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    NOTIFICATION_TYPE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NOTIFICATION_TITLE = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NOTIFICATION_BODY = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NOTIFICATION_DATA = table.Column<string>(type: "jsonb", nullable: false),
                    NOTIFICATION_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    NOTIFICATION_ATTEMPT_COUNT = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NOTIFICATION_NEXT_ATTEMPT_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NOTIFICATION_SENT_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NOTIFICATION_ERROR = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NOTIFICATION_CLICKED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NOTIFICATION_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NOTIFICATION_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOTIFICATIONS", x => x.NOTIFICATION_ID);
                    table.ForeignKey(
                        name: "FK_NOTIFICATIONS_USERS",
                        column: x => x.NOTIFICATION_USER_ID,
                        principalTable: "USERS",
                        principalColumn: "USER_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATIONS_STATUS_NEXT_ATTEMPT",
                table: "NOTIFICATIONS",
                columns: new[] { "NOTIFICATION_STATUS", "NOTIFICATION_NEXT_ATTEMPT_DATE" });

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATIONS_USER_ID",
                table: "NOTIFICATIONS",
                column: "NOTIFICATION_USER_ID");

            // A CHECK that evaluates to NULL passes: every rule below is wrapped in COALESCE(..., false) so a NULL
            // is rejected (the columns are also NOT NULL).
            migrationBuilder.Sql(
                """
                ALTER TABLE "NOTIFICATIONS"
                ADD CONSTRAINT "CK_NOTIFICATIONS_TYPE"
                CHECK (COALESCE("NOTIFICATION_TYPE" IN ('CHAT_MESSAGE', 'SESSION_BOOKED', 'SESSION_CANCELLED', 'SESSION_REMINDER', 'MEMBER_JOINED'), false));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "NOTIFICATIONS"
                ADD CONSTRAINT "CK_NOTIFICATIONS_STATUS"
                CHECK (COALESCE("NOTIFICATION_STATUS" IN ('PENDING', 'SENT', 'NO_DEVICE', 'FAILED'), false));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "NOTIFICATIONS"
                ADD CONSTRAINT "CK_NOTIFICATIONS_ATTEMPT_COUNT"
                CHECK (COALESCE("NOTIFICATION_ATTEMPT_COUNT" BETWEEN 0 AND 10, false));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "NOTIFICATIONS"
                ADD CONSTRAINT "CK_NOTIFICATIONS_DATA"
                CHECK (COALESCE(jsonb_typeof("NOTIFICATION_DATA") = 'object', false));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "NOTIFICATIONS" DROP CONSTRAINT "CK_NOTIFICATIONS_DATA";""");
            migrationBuilder.Sql("""ALTER TABLE "NOTIFICATIONS" DROP CONSTRAINT "CK_NOTIFICATIONS_ATTEMPT_COUNT";""");
            migrationBuilder.Sql("""ALTER TABLE "NOTIFICATIONS" DROP CONSTRAINT "CK_NOTIFICATIONS_STATUS";""");
            migrationBuilder.Sql("""ALTER TABLE "NOTIFICATIONS" DROP CONSTRAINT "CK_NOTIFICATIONS_TYPE";""");

            migrationBuilder.DropTable(
                name: "NOTIFICATIONS");
        }
    }
}
