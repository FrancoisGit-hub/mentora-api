using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot11_2_ProgramTreeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. New columns
            migrationBuilder.AddColumn<string>(
                name: "PROGRAM_SESSION_NATURE",
                table: "PROGRAM_SESSIONS",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "STANDARD");

            migrationBuilder.AddColumn<int>(
                name: "PROGRAM_CIRCUIT_REST_AFTER_SECONDS",
                table: "PROGRAM_CIRCUITS",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PROGRAM_BLOCK_LOAD_PERCENT",
                table: "PROGRAM_BLOCKS",
                type: "numeric(4,1)",
                nullable: true);

            // 2. A_DISTANCE -> AUTONOME: drop the old CHECK, convert rows and jsonb bodies.
            migrationBuilder.Sql(
                """ALTER TABLE "PROGRAM_SESSIONS" DROP CONSTRAINT "CK_PROGRAM_SESSIONS_A_DISTANCE_HAS_NO_BOOKING";""");

            migrationBuilder.Sql(
                """UPDATE "PROGRAM_SESSIONS" SET "PROGRAM_SESSION_TYPE" = 'AUTONOME' WHERE "PROGRAM_SESSION_TYPE" = 'A_DISTANCE';""");

            // Safe only because the string "A_DISTANCE" appears in bodies solely as a session type value.
            migrationBuilder.Sql(
                """
                UPDATE "PROGRAM_TEMPLATES"
                SET "PROGRAM_TEMPLATE_BODY" = replace("PROGRAM_TEMPLATE_BODY"::text, '"A_DISTANCE"', '"AUTONOME"')::jsonb
                WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"A_DISTANCE"%';
                """);

            // 3. CHECK constraints
            migrationBuilder.Sql(
                """
                ALTER TABLE "PROGRAM_SESSIONS"
                ADD CONSTRAINT "CK_PROGRAM_SESSIONS_AUTONOME_HAS_NO_BOOKING"
                CHECK ("PROGRAM_SESSION_TYPE" <> 'AUTONOME' OR "PROGRAM_SESSION_SESSION_ID" IS NULL);
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "PROGRAM_SESSIONS"
                ADD CONSTRAINT "CK_PROGRAM_SESSIONS_TYPE"
                CHECK ("PROGRAM_SESSION_TYPE" IN ('PRESENTIEL_SOLO', 'PRESENTIEL_GROUPE', 'VISIO', 'AUTONOME', 'PRESENTIEL_OU_VISIO'));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "PROGRAM_SESSIONS"
                ADD CONSTRAINT "CK_PROGRAM_SESSIONS_NATURE"
                CHECK ("PROGRAM_SESSION_NATURE" IN ('STANDARD', 'EVALUATION'));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "PROGRAM_BLOCKS"
                ADD CONSTRAINT "CK_PROGRAM_BLOCKS_LOAD_PERCENT"
                CHECK ("PROGRAM_BLOCK_LOAD_PERCENT" IS NULL
                       OR ("PROGRAM_BLOCK_LEVEL" = 'MESOCYCLE' AND "PROGRAM_BLOCK_LOAD_PERCENT" IN (100, 102.5, 105, 110)));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "PROGRAM_CIRCUITS"
                ADD CONSTRAINT "CK_PROGRAM_CIRCUITS_REST_AFTER_SECONDS"
                CHECK ("PROGRAM_CIRCUIT_REST_AFTER_SECONDS" IS NULL OR "PROGRAM_CIRCUIT_REST_AFTER_SECONDS" BETWEEN 0 AND 600);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // PRESENTIEL_OU_VISIO has no old equivalent: refuse rather than corrupt data silently.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "PROGRAM_SESSIONS" WHERE "PROGRAM_SESSION_TYPE" = 'PRESENTIEL_OU_VISIO')
                       OR EXISTS (SELECT 1 FROM "PROGRAM_TEMPLATES" WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"PRESENTIEL_OU_VISIO"%')
                    THEN
                        RAISE EXCEPTION 'Cannot roll back Lot11_2_ProgramTreeFields: PRESENTIEL_OU_VISIO is still used by PROGRAM_SESSIONS rows or PROGRAM_TEMPLATES bodies and has no previous equivalent. Change those sessions to another type first.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql("""ALTER TABLE "PROGRAM_CIRCUITS" DROP CONSTRAINT "CK_PROGRAM_CIRCUITS_REST_AFTER_SECONDS";""");
            migrationBuilder.Sql("""ALTER TABLE "PROGRAM_BLOCKS" DROP CONSTRAINT "CK_PROGRAM_BLOCKS_LOAD_PERCENT";""");
            migrationBuilder.Sql("""ALTER TABLE "PROGRAM_SESSIONS" DROP CONSTRAINT "CK_PROGRAM_SESSIONS_NATURE";""");
            migrationBuilder.Sql("""ALTER TABLE "PROGRAM_SESSIONS" DROP CONSTRAINT "CK_PROGRAM_SESSIONS_TYPE";""");
            migrationBuilder.Sql("""ALTER TABLE "PROGRAM_SESSIONS" DROP CONSTRAINT "CK_PROGRAM_SESSIONS_AUTONOME_HAS_NO_BOOKING";""");

            migrationBuilder.Sql(
                """UPDATE "PROGRAM_SESSIONS" SET "PROGRAM_SESSION_TYPE" = 'A_DISTANCE' WHERE "PROGRAM_SESSION_TYPE" = 'AUTONOME';""");

            migrationBuilder.Sql(
                """
                UPDATE "PROGRAM_TEMPLATES"
                SET "PROGRAM_TEMPLATE_BODY" = replace("PROGRAM_TEMPLATE_BODY"::text, '"AUTONOME"', '"A_DISTANCE"')::jsonb
                WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"AUTONOME"%';
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "PROGRAM_SESSIONS"
                ADD CONSTRAINT "CK_PROGRAM_SESSIONS_A_DISTANCE_HAS_NO_BOOKING"
                CHECK ("PROGRAM_SESSION_TYPE" <> 'A_DISTANCE' OR "PROGRAM_SESSION_SESSION_ID" IS NULL);
                """);

            migrationBuilder.DropColumn(
                name: "PROGRAM_SESSION_NATURE",
                table: "PROGRAM_SESSIONS");

            migrationBuilder.DropColumn(
                name: "PROGRAM_CIRCUIT_REST_AFTER_SECONDS",
                table: "PROGRAM_CIRCUITS");

            migrationBuilder.DropColumn(
                name: "PROGRAM_BLOCK_LOAD_PERCENT",
                table: "PROGRAM_BLOCKS");
        }
    }
}
