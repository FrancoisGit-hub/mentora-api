using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot6_4_GroupSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SESSION_VOUCHER_ID",
                table: "SESSIONS",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "SESSION_PRODUCT_ID",
                table: "SESSIONS",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "SESSION_MEMBER_ID",
                table: "SESSIONS",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "SESSION_MAX_PARTICIPANTS",
                table: "SESSIONS",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PRODUCT_MAX_PARTICIPANTS",
                table: "PRODUCTS",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SESSION_PARTICIPANTS",
                columns: table => new
                {
                    SESSION_PARTICIPANT_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SESSION_PARTICIPANT_SESSION_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    SESSION_PARTICIPANT_MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    SESSION_PARTICIPANT_VOUCHER_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    SESSION_PARTICIPANT_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'REGISTERED'"),
                    SESSION_PARTICIPANT_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SESSION_PARTICIPANT_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SESSION_PARTICIPANTS", x => x.SESSION_PARTICIPANT_ID);
                    table.ForeignKey(
                        name: "FK_SESSION_PARTICIPANTS_MEMBERS_SESSION_PARTICIPANT_MEMBER_ID",
                        column: x => x.SESSION_PARTICIPANT_MEMBER_ID,
                        principalTable: "MEMBERS",
                        principalColumn: "MEMBER_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SESSION_PARTICIPANTS_SESSIONS_SESSION_PARTICIPANT_SESSION_ID",
                        column: x => x.SESSION_PARTICIPANT_SESSION_ID,
                        principalTable: "SESSIONS",
                        principalColumn: "SESSION_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SESSION_PARTICIPANTS_SESSION_VOUCHERS_SESSION_PARTICIPANT_V~",
                        column: x => x.SESSION_PARTICIPANT_VOUCHER_ID,
                        principalTable: "SESSION_VOUCHERS",
                        principalColumn: "VOUCHER_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SESSION_PARTICIPANTS_SESSION_PARTICIPANT_MEMBER_ID",
                table: "SESSION_PARTICIPANTS",
                column: "SESSION_PARTICIPANT_MEMBER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESSION_PARTICIPANTS_SESSION_PARTICIPANT_SESSION_ID",
                table: "SESSION_PARTICIPANTS",
                column: "SESSION_PARTICIPANT_SESSION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESSION_PARTICIPANTS_SESSION_PARTICIPANT_VOUCHER_ID",
                table: "SESSION_PARTICIPANTS",
                column: "SESSION_PARTICIPANT_VOUCHER_ID");

            // Partial unique index: at most one active (non-cancelled) registration per
            // (session, member). Cancelling keeps the row for history and allows re-registration.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_SESSION_PARTICIPANTS_SESSION_MEMBER_ACTIVE"
                  ON "SESSION_PARTICIPANTS" ("SESSION_PARTICIPANT_SESSION_ID", "SESSION_PARTICIPANT_MEMBER_ID")
                  WHERE "SESSION_PARTICIPANT_STATUS" <> 'CANCELLED';
                """);

            // CHECK constraint — a group offer type implies no single member and no single
            // voucher (both live in SESSION_PARTICIPANTS instead); a non-group offer type implies
            // both are present. SESSION_PRODUCT_ID is deliberately left out: unlike member/
            // voucher, a group session's product is always set (explicit productId at creation),
            // so a "must be null for groups" rule would be wrong.
            migrationBuilder.Sql(
                """
                ALTER TABLE "SESSIONS" ADD CONSTRAINT "CK_SESSIONS_GROUP_HAS_NO_MEMBER"
                  CHECK (
                    ("SESSION_OFFER_TYPE" IN ('PRESENTIEL_GROUPE','VISIO_GROUPE')
                       AND "SESSION_MEMBER_ID" IS NULL
                       AND "SESSION_VOUCHER_ID" IS NULL)
                    OR
                    ("SESSION_OFFER_TYPE" NOT IN ('PRESENTIEL_GROUPE','VISIO_GROUPE')
                       AND "SESSION_MEMBER_ID" IS NOT NULL
                       AND "SESSION_VOUCHER_ID" IS NOT NULL)
                  );
                """);

            // V_SESSION_MEMBERS — the single "who is on this session" read path, covering both an
            // individual session's SESSION_MEMBER_ID and a group session's active participants.
            migrationBuilder.Sql(
                """
                CREATE VIEW "V_SESSION_MEMBERS" AS
                  SELECT "SESSION_ID", "SESSION_MEMBER_ID" AS "MEMBER_ID"
                  FROM "SESSIONS" WHERE "SESSION_MEMBER_ID" IS NOT NULL
                  UNION ALL
                  SELECT "SESSION_PARTICIPANT_SESSION_ID", "SESSION_PARTICIPANT_MEMBER_ID"
                  FROM "SESSION_PARTICIPANTS"
                  WHERE "SESSION_PARTICIPANT_STATUS" <> 'CANCELLED';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // View first — it depends on both SESSIONS and SESSION_PARTICIPANTS.
            migrationBuilder.Sql("""DROP VIEW "V_SESSION_MEMBERS";""");

            migrationBuilder.Sql("""ALTER TABLE "SESSIONS" DROP CONSTRAINT "CK_SESSIONS_GROUP_HAS_NO_MEMBER";""");

            migrationBuilder.DropTable(
                name: "SESSION_PARTICIPANTS");

            migrationBuilder.DropColumn(
                name: "SESSION_MAX_PARTICIPANTS",
                table: "SESSIONS");

            migrationBuilder.DropColumn(
                name: "PRODUCT_MAX_PARTICIPANTS",
                table: "PRODUCTS");

            migrationBuilder.AlterColumn<Guid>(
                name: "SESSION_VOUCHER_ID",
                table: "SESSIONS",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SESSION_PRODUCT_ID",
                table: "SESSIONS",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SESSION_MEMBER_ID",
                table: "SESSIONS",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
