using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lot 14.9a: data-only migration (no schema operation). The circuit mode STANDARD is renamed
    /// SERIE_SIMPLE in PROGRAM_CIRCUITS.PROGRAM_CIRCUIT_MODE and, as the exact text token
    /// '"mode": "STANDARD"', in PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY and
    /// SESSION_TEMPLATES.SESSION_TEMPLATE_CONTENT (jsonb::text form, then cast back to jsonb).
    /// The token includes the key, so a session nature ("nature": "STANDARD" or the SESSION_TEMPLATE_NATURE
    /// column) can never match. Only rows that contain the token are updated.
    /// Down refuses (exception) when any mode other than SERIE_SIMPLE, INTERVAL or STANDARD is stored,
    /// because the previous code cannot read the new modes; otherwise it reverses the rewrite.
    /// </summary>
    public partial class Lot14_9a_CircuitModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "PROGRAM_CIRCUITS"
                SET "PROGRAM_CIRCUIT_MODE" = 'SERIE_SIMPLE'
                WHERE "PROGRAM_CIRCUIT_MODE" = 'STANDARD';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "PROGRAM_TEMPLATES"
                SET "PROGRAM_TEMPLATE_BODY" = replace("PROGRAM_TEMPLATE_BODY"::text,
                        '"mode": "STANDARD"', '"mode": "SERIE_SIMPLE"')::jsonb
                WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"mode": "STANDARD"%';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "SESSION_TEMPLATES"
                SET "SESSION_TEMPLATE_CONTENT" = replace("SESSION_TEMPLATE_CONTENT"::text,
                        '"mode": "STANDARD"', '"mode": "SERIE_SIMPLE"')::jsonb
                WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"mode": "STANDARD"%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "PROGRAM_CIRCUITS"
                        WHERE "PROGRAM_CIRCUIT_MODE" NOT IN ('SERIE_SIMPLE', 'INTERVAL', 'STANDARD'))
                    OR EXISTS (
                        SELECT 1 FROM "PROGRAM_TEMPLATES" t
                        CROSS JOIN LATERAL regexp_matches(t."PROGRAM_TEMPLATE_BODY"::text, '"mode": "([^"]*)"', 'g') AS m(arr)
                        WHERE m.arr[1] NOT IN ('SERIE_SIMPLE', 'INTERVAL', 'STANDARD'))
                    OR EXISTS (
                        SELECT 1 FROM "SESSION_TEMPLATES" s
                        CROSS JOIN LATERAL regexp_matches(s."SESSION_TEMPLATE_CONTENT"::text, '"mode": "([^"]*)"', 'g') AS m(arr)
                        WHERE m.arr[1] NOT IN ('SERIE_SIMPLE', 'INTERVAL', 'STANDARD'))
                    THEN
                        RAISE EXCEPTION 'Cannot revert Lot14_9a_CircuitModes: circuits use a mode other than SERIE_SIMPLE, INTERVAL or STANDARD. Convert or delete them first.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "PROGRAM_CIRCUITS"
                SET "PROGRAM_CIRCUIT_MODE" = 'STANDARD'
                WHERE "PROGRAM_CIRCUIT_MODE" = 'SERIE_SIMPLE';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "PROGRAM_TEMPLATES"
                SET "PROGRAM_TEMPLATE_BODY" = replace("PROGRAM_TEMPLATE_BODY"::text,
                        '"mode": "SERIE_SIMPLE"', '"mode": "STANDARD"')::jsonb
                WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"mode": "SERIE_SIMPLE"%';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "SESSION_TEMPLATES"
                SET "SESSION_TEMPLATE_CONTENT" = replace("SESSION_TEMPLATE_CONTENT"::text,
                        '"mode": "SERIE_SIMPLE"', '"mode": "STANDARD"')::jsonb
                WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"mode": "SERIE_SIMPLE"%';
                """);
        }
    }
}
