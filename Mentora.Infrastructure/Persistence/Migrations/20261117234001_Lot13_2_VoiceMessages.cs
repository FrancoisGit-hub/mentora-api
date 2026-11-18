using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot13_2_VoiceMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MESSAGE_AUDIO_CONTENT_TYPE",
                table: "MESSAGES",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MESSAGE_AUDIO_DURATION_MS",
                table: "MESSAGES",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MESSAGE_AUDIO_PATH",
                table: "MESSAGES",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MESSAGE_AUDIO_SIZE_BYTES",
                table: "MESSAGES",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<List<int>>(
                name: "MESSAGE_AUDIO_WAVEFORM",
                table: "MESSAGES",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MESSAGE_TYPE",
                table: "MESSAGES",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValueSql: "'TEXT'");

            // Lot 13.2: added after every AddColumn, so the 'TEXT' default has already filled the
            // existing rows. The business limits (size, duration, bar count and range) are enforced
            // by the service from 13.3, not here.
            // Every audio column of the AUDIO branch carries an explicit IS NOT NULL: a CHECK that
            // evaluates to NULL passes in PostgreSQL, so "MESSAGE_AUDIO_DURATION_MS" > 0 (or IN, or
            // jsonb_typeof(...) = 'array') alone would accept a NULL value.
            migrationBuilder.Sql(
                """
                ALTER TABLE "MESSAGES"
                ADD CONSTRAINT "CK_MESSAGES_TYPE" CHECK (
                    "MESSAGE_TYPE" IN ('TEXT','AUDIO')
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "MESSAGES"
                ADD CONSTRAINT "CK_MESSAGES_AUDIO_FIELDS" CHECK (
                    (
                        "MESSAGE_TYPE" = 'TEXT'
                        AND "MESSAGE_AUDIO_PATH" IS NULL
                        AND "MESSAGE_AUDIO_CONTENT_TYPE" IS NULL
                        AND "MESSAGE_AUDIO_DURATION_MS" IS NULL
                        AND "MESSAGE_AUDIO_SIZE_BYTES" IS NULL
                        AND "MESSAGE_AUDIO_WAVEFORM" IS NULL
                    )
                    OR
                    (
                        "MESSAGE_TYPE" = 'AUDIO'
                        AND "MESSAGE_CONTENT" = ''
                        AND "MESSAGE_AUDIO_PATH" IS NOT NULL
                        AND length("MESSAGE_AUDIO_PATH") > 0
                        AND "MESSAGE_AUDIO_CONTENT_TYPE" IS NOT NULL
                        AND "MESSAGE_AUDIO_CONTENT_TYPE" IN ('audio/aac','audio/mp4')
                        AND "MESSAGE_AUDIO_DURATION_MS" IS NOT NULL
                        AND "MESSAGE_AUDIO_DURATION_MS" > 0
                        AND "MESSAGE_AUDIO_SIZE_BYTES" IS NOT NULL
                        AND "MESSAGE_AUDIO_SIZE_BYTES" > 0
                        AND "MESSAGE_AUDIO_WAVEFORM" IS NOT NULL
                        AND jsonb_typeof("MESSAGE_AUDIO_WAVEFORM") = 'array'
                    )
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Guard first: dropping the columns would silently turn audio messages into empty text.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "MESSAGES" WHERE "MESSAGE_TYPE" = 'AUDIO') THEN
                        RAISE EXCEPTION 'Lot13_2 Down refused: audio messages exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""ALTER TABLE "MESSAGES" DROP CONSTRAINT "CK_MESSAGES_AUDIO_FIELDS";""");
            migrationBuilder.Sql("""ALTER TABLE "MESSAGES" DROP CONSTRAINT "CK_MESSAGES_TYPE";""");

            migrationBuilder.DropColumn(
                name: "MESSAGE_AUDIO_CONTENT_TYPE",
                table: "MESSAGES");

            migrationBuilder.DropColumn(
                name: "MESSAGE_AUDIO_DURATION_MS",
                table: "MESSAGES");

            migrationBuilder.DropColumn(
                name: "MESSAGE_AUDIO_PATH",
                table: "MESSAGES");

            migrationBuilder.DropColumn(
                name: "MESSAGE_AUDIO_SIZE_BYTES",
                table: "MESSAGES");

            migrationBuilder.DropColumn(
                name: "MESSAGE_AUDIO_WAVEFORM",
                table: "MESSAGES");

            migrationBuilder.DropColumn(
                name: "MESSAGE_TYPE",
                table: "MESSAGES");
        }
    }
}
