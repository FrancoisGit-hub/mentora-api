using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot12_3a_MuxVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EXERCISE_VIDEO_MUX_ASSET_ID",
                table: "EXERCISE_VIDEOS",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_VIDEO_ERROR",
                table: "EXERCISE_VIDEOS",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_VIDEO_MUX_UPLOAD_ID",
                table: "EXERCISE_VIDEOS",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EXERCISE_VIDEO_UPLOAD_EXPIRES_DATE",
                table: "EXERCISE_VIDEOS",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MUX_WEBHOOK_EVENTS",
                columns: table => new
                {
                    MUX_WEBHOOK_EVENT_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MUX_WEBHOOK_EVENT_MUX_ID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MUX_WEBHOOK_EVENT_TYPE = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MUX_WEBHOOK_EVENT_OBJECT_ID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MUX_WEBHOOK_EVENT_STATUS = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MUX_WEBHOOK_EVENT_RESULT = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MUX_WEBHOOK_EVENT_PAYLOAD = table.Column<string>(type: "text", nullable: false),
                    MUX_WEBHOOK_EVENT_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MUX_WEBHOOK_EVENTS", x => x.MUX_WEBHOOK_EVENT_ID);
                });

            migrationBuilder.CreateIndex(
                name: "UX_EXERCISE_VIDEOS_MUX_UPLOAD",
                table: "EXERCISE_VIDEOS",
                column: "EXERCISE_VIDEO_MUX_UPLOAD_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MUX_WEBHOOK_EVENTS_CREATED_DATE",
                table: "MUX_WEBHOOK_EVENTS",
                column: "MUX_WEBHOOK_EVENT_CREATED_DATE");

            migrationBuilder.CreateIndex(
                name: "UX_MUX_WEBHOOK_EVENTS_MUX_ID",
                table: "MUX_WEBHOOK_EVENTS",
                column: "MUX_WEBHOOK_EVENT_MUX_ID",
                unique: true);

            // Lot 12.3a — named CHECKs (raw SQL, project convention).
            migrationBuilder.Sql("""ALTER TABLE "EXERCISE_VIDEOS" DROP CONSTRAINT "CK_EXERCISE_VIDEOS_STATUS";""");
            migrationBuilder.Sql(
                """
                ALTER TABLE "EXERCISE_VIDEOS"
                ADD CONSTRAINT "CK_EXERCISE_VIDEOS_STATUS" CHECK (
                    "EXERCISE_VIDEO_STATUS" IN ('UPLOADING','WAITING','READY','ERRORED')
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "EXERCISE_VIDEOS"
                ADD CONSTRAINT "CK_EXERCISE_VIDEOS_MUX_REF" CHECK (
                    "EXERCISE_VIDEO_MUX_UPLOAD_ID" IS NOT NULL OR "EXERCISE_VIDEO_MUX_ASSET_ID" IS NOT NULL
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "MUX_WEBHOOK_EVENTS"
                ADD CONSTRAINT "CK_MUX_WEBHOOK_EVENTS_STATUS" CHECK (
                    "MUX_WEBHOOK_EVENT_STATUS" IN ('PROCESSED','IGNORED')
                );
                """);

            migrationBuilder.Sql("""ALTER TABLE "ADMIN_AUDIT_LOG" DROP CONSTRAINT "CK_ADMIN_AUDIT_LOG_ACTION";""");
            migrationBuilder.Sql(
                """
                ALTER TABLE "ADMIN_AUDIT_LOG"
                ADD CONSTRAINT "CK_ADMIN_AUDIT_LOG_ACTION"
                CHECK ("ADMIN_AUDIT_LOG_ACTION" IN ('EXERCISE_CREATED', 'EXERCISE_UPDATED', 'EXERCISE_ACTIVATED', 'EXERCISE_DEACTIVATED', 'EXERCISE_VIDEO_UPLOAD_CREATED', 'EXERCISE_VIDEO_REMOVED'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refuse clearly (nothing changed yet) when rows the previous schema cannot hold exist.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "EXERCISE_VIDEOS" WHERE "EXERCISE_VIDEO_STATUS" = 'UPLOADING') THEN
                        RAISE EXCEPTION 'Lot12_3a_MuxVideos Down refused: EXERCISE_VIDEOS holds UPLOADING rows. Remove them first.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "EXERCISE_VIDEOS" WHERE "EXERCISE_VIDEO_MUX_ASSET_ID" IS NULL) THEN
                        RAISE EXCEPTION 'Lot12_3a_MuxVideos Down refused: EXERCISE_VIDEOS holds rows without a MUX asset id. Remove them first.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "ADMIN_AUDIT_LOG"
                               WHERE "ADMIN_AUDIT_LOG_ACTION" IN ('EXERCISE_VIDEO_UPLOAD_CREATED', 'EXERCISE_VIDEO_REMOVED')) THEN
                        RAISE EXCEPTION 'Lot12_3a_MuxVideos Down refused: ADMIN_AUDIT_LOG holds EXERCISE_VIDEO_* rows. Remove them first.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""ALTER TABLE "ADMIN_AUDIT_LOG" DROP CONSTRAINT "CK_ADMIN_AUDIT_LOG_ACTION";""");
            migrationBuilder.Sql(
                """
                ALTER TABLE "ADMIN_AUDIT_LOG"
                ADD CONSTRAINT "CK_ADMIN_AUDIT_LOG_ACTION"
                CHECK ("ADMIN_AUDIT_LOG_ACTION" IN ('EXERCISE_CREATED', 'EXERCISE_UPDATED', 'EXERCISE_ACTIVATED', 'EXERCISE_DEACTIVATED'));
                """);

            migrationBuilder.Sql("""ALTER TABLE "MUX_WEBHOOK_EVENTS" DROP CONSTRAINT "CK_MUX_WEBHOOK_EVENTS_STATUS";""");
            migrationBuilder.Sql("""ALTER TABLE "EXERCISE_VIDEOS" DROP CONSTRAINT "CK_EXERCISE_VIDEOS_MUX_REF";""");
            migrationBuilder.Sql("""ALTER TABLE "EXERCISE_VIDEOS" DROP CONSTRAINT "CK_EXERCISE_VIDEOS_STATUS";""");
            migrationBuilder.Sql(
                """
                ALTER TABLE "EXERCISE_VIDEOS"
                ADD CONSTRAINT "CK_EXERCISE_VIDEOS_STATUS" CHECK (
                    "EXERCISE_VIDEO_STATUS" IN ('WAITING','READY','ERRORED')
                );
                """);

            migrationBuilder.DropTable(
                name: "MUX_WEBHOOK_EVENTS");

            migrationBuilder.DropIndex(
                name: "UX_EXERCISE_VIDEOS_MUX_UPLOAD",
                table: "EXERCISE_VIDEOS");

            migrationBuilder.DropColumn(
                name: "EXERCISE_VIDEO_ERROR",
                table: "EXERCISE_VIDEOS");

            migrationBuilder.DropColumn(
                name: "EXERCISE_VIDEO_MUX_UPLOAD_ID",
                table: "EXERCISE_VIDEOS");

            migrationBuilder.DropColumn(
                name: "EXERCISE_VIDEO_UPLOAD_EXPIRES_DATE",
                table: "EXERCISE_VIDEOS");

            migrationBuilder.AlterColumn<string>(
                name: "EXERCISE_VIDEO_MUX_ASSET_ID",
                table: "EXERCISE_VIDEOS",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                // No defaultValue: the guard above guarantees no NULL is left, and a '' back-fill would
                // collide in UX_EXERCISE_VIDEOS_MUX_ASSET.
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
