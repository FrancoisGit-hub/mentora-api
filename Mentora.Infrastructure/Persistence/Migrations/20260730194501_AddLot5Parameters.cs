using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLot5Parameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CONVERSATION_VISIO_URL",
                table: "CONVERSATIONS");

            migrationBuilder.AddColumn<string>(
                name: "USER_DELETION_REASON",
                table: "USERS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "USER_DELETION_REQUESTED_DATE",
                table: "USERS",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MEMBER_BIRTH_DATE",
                table: "MEMBERS",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MEMBER_GENDER",
                table: "MEMBERS",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "MEMBER_HEIGHT_CM",
                table: "MEMBERS",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MEMBER_COACH_PRESENTIAL_ADDRESS",
                table: "MEMBER_COACHES",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "COACH_PARAMETER_DEFAULT_SESSION_DURATION_MINUTES",
                table: "COACH_PARAMETERS",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "COACH_PARAMETER_IS_ACCEPTING_NEW_BOOKINGS",
                table: "COACH_PARAMETERS",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "COACH_PARAMETER_LANGUAGE",
                table: "COACH_PARAMETERS",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "FR");

            migrationBuilder.AddColumn<bool>(
                name: "COACH_PARAMETER_LATE_CANCELLATION_REFUNDS",
                table: "COACH_PARAMETERS",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "COACH_PARAMETER_MAX_BOOKING_HORIZON_DAYS",
                table: "COACH_PARAMETERS",
                type: "integer",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.AddColumn<int>(
                name: "COACH_PARAMETER_MIN_BOOKING_NOTICE_HOURS",
                table: "COACH_PARAMETERS",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<bool>(
                name: "COACH_PARAMETER_NOTIF_BOOKING_CANCELLED",
                table: "COACH_PARAMETERS",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "COACH_PARAMETER_NOTIF_MARKETING",
                table: "COACH_PARAMETERS",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "COACH_PARAMETER_NOTIF_MESSAGES",
                table: "COACH_PARAMETERS",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "COACH_PARAMETER_NOTIF_NEW_BOOKING",
                table: "COACH_PARAMETERS",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Added nullable first — backfilled below, then locked to NOT NULL — because a
            // straight non-nullable AddColumn has no correct value for existing rows.
            migrationBuilder.AddColumn<string>(
                name: "AUTH_REFRESH_TOKEN_USER_ROLE",
                table: "AUTH_REFRESH_TOKENS",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MEMBER_PARAMETERS",
                columns: table => new
                {
                    MEMBER_PARAMETER_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MEMBER_PARAMETER_LANGUAGE = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "FR"),
                    MEMBER_PARAMETER_NOTIF_MESSAGES = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MEMBER_PARAMETER_NOTIF_SESSION_REMINDERS = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MEMBER_PARAMETER_NOTIF_MARKETING = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MEMBER_PARAMETER_SESSION_REMINDER_HOURS_BEFORE = table.Column<int>(type: "integer", nullable: false, defaultValue: 24),
                    MEMBER_PARAMETER_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MEMBER_PARAMETER_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEMBER_PARAMETERS", x => x.MEMBER_PARAMETER_ID);
                    table.ForeignKey(
                        name: "FK_MEMBER_PARAMETERS_MEMBERS_MEMBER_ID",
                        column: x => x.MEMBER_ID,
                        principalTable: "MEMBERS",
                        principalColumn: "MEMBER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "USER_DEVICES",
                columns: table => new
                {
                    USER_DEVICE_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    USER_DEVICE_TOKEN = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    USER_DEVICE_PLATFORM = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    USER_DEVICE_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    USER_DEVICE_LAST_SEEN_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    USER_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USER_DEVICES", x => x.USER_DEVICE_ID);
                    table.ForeignKey(
                        name: "FK_USER_DEVICES_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "USERS",
                        principalColumn: "USER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MEMBER_PARAMETERS_MEMBER_ID",
                table: "MEMBER_PARAMETERS",
                column: "MEMBER_ID",
                unique: true);

            // Backfill: one default-valued MEMBER_PARAMETERS row per existing member.
            // All columns besides MEMBER_ID and the two audit dates take their DB defaults.
            migrationBuilder.Sql(
                """
                INSERT INTO "MEMBER_PARAMETERS" ("MEMBER_ID", "MEMBER_PARAMETER_CREATED_DATE",
                  "MEMBER_PARAMETER_UPDATED_DATE")
                SELECT "MEMBER_ID", NOW(), NOW() FROM "MEMBERS"
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_USER_DEVICES_USER_DEVICE_TOKEN",
                table: "USER_DEVICES",
                column: "USER_DEVICE_TOKEN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USER_DEVICES_USER_ID",
                table: "USER_DEVICES",
                column: "USER_ID");

            // Backfill: tokens already in flight keep working. MEMBER where the user has a
            // MEMBERS row, COACH where the user has a COACHES row.
            migrationBuilder.Sql(
                """
                UPDATE "AUTH_REFRESH_TOKENS" art
                SET "AUTH_REFRESH_TOKEN_USER_ROLE" = sub.role
                FROM (
                    SELECT u."USER_ID",
                           CASE
                               WHEN m."MEMBER_ID" IS NOT NULL THEN 'MEMBER'
                               WHEN c."COACH_ID" IS NOT NULL THEN 'COACH'
                           END AS role
                    FROM "USERS" u
                    LEFT JOIN "MEMBERS" m ON m."USER_ID" = u."USER_ID"
                    LEFT JOIN "COACHES" c ON c."USER_ID" = u."USER_ID"
                ) sub
                WHERE art."USER_ID" = sub."USER_ID";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "AUTH_REFRESH_TOKEN_USER_ROLE",
                table: "AUTH_REFRESH_TOKENS",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MEMBER_PARAMETERS");

            migrationBuilder.DropTable(
                name: "USER_DEVICES");

            migrationBuilder.DropColumn(
                name: "USER_DELETION_REASON",
                table: "USERS");

            migrationBuilder.DropColumn(
                name: "USER_DELETION_REQUESTED_DATE",
                table: "USERS");

            migrationBuilder.DropColumn(
                name: "MEMBER_BIRTH_DATE",
                table: "MEMBERS");

            migrationBuilder.DropColumn(
                name: "MEMBER_GENDER",
                table: "MEMBERS");

            migrationBuilder.DropColumn(
                name: "MEMBER_HEIGHT_CM",
                table: "MEMBERS");

            migrationBuilder.DropColumn(
                name: "MEMBER_COACH_PRESENTIAL_ADDRESS",
                table: "MEMBER_COACHES");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_DEFAULT_SESSION_DURATION_MINUTES",
                table: "COACH_PARAMETERS");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_IS_ACCEPTING_NEW_BOOKINGS",
                table: "COACH_PARAMETERS");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_LANGUAGE",
                table: "COACH_PARAMETERS");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_LATE_CANCELLATION_REFUNDS",
                table: "COACH_PARAMETERS");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_MAX_BOOKING_HORIZON_DAYS",
                table: "COACH_PARAMETERS");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_MIN_BOOKING_NOTICE_HOURS",
                table: "COACH_PARAMETERS");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_NOTIF_BOOKING_CANCELLED",
                table: "COACH_PARAMETERS");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_NOTIF_MARKETING",
                table: "COACH_PARAMETERS");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_NOTIF_MESSAGES",
                table: "COACH_PARAMETERS");

            migrationBuilder.DropColumn(
                name: "COACH_PARAMETER_NOTIF_NEW_BOOKING",
                table: "COACH_PARAMETERS");

            migrationBuilder.DropColumn(
                name: "AUTH_REFRESH_TOKEN_USER_ROLE",
                table: "AUTH_REFRESH_TOKENS");

            migrationBuilder.AddColumn<string>(
                name: "CONVERSATION_VISIO_URL",
                table: "CONVERSATIONS",
                type: "text",
                nullable: true);
        }
    }
}
