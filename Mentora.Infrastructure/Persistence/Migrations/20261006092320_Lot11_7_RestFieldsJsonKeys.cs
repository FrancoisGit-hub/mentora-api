using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lot 11.7: data-only migration. The two rest fields seen by clients are renamed in the JSON
    /// stored in PROGRAM_TEMPLATES.PROGRAM_TEMPLATE_BODY and SESSION_TEMPLATES.SESSION_TEMPLATE_CONTENT:
    ///   "restSeconds"      -> "restSecondsAfterSeries"
    ///   "restAfterSeconds" -> "restSecondsAfterExercices"
    /// No schema operation: columns, CHECKs and prefixed entity properties keep their names.
    /// The tokens are matched WITH their quotes, so "restBetweenRoundsSeconds" or any longer key can
    /// never match. Safe only because these strings appear in the bodies solely as JSON keys (see the
    /// read-only check query in docs/review/lot11/11.7). Only rows that contain an old token are updated.
    /// </summary>
    public partial class Lot11_7_RestFieldsJsonKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "PROGRAM_TEMPLATES"
                SET "PROGRAM_TEMPLATE_BODY" = replace(replace("PROGRAM_TEMPLATE_BODY"::text,
                        '"restSeconds"', '"restSecondsAfterSeries"'),
                        '"restAfterSeconds"', '"restSecondsAfterExercices"')::jsonb
                WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"restSeconds"%'
                   OR "PROGRAM_TEMPLATE_BODY"::text LIKE '%"restAfterSeconds"%';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "SESSION_TEMPLATES"
                SET "SESSION_TEMPLATE_CONTENT" = replace(replace("SESSION_TEMPLATE_CONTENT"::text,
                        '"restSeconds"', '"restSecondsAfterSeries"'),
                        '"restAfterSeconds"', '"restSecondsAfterExercices"')::jsonb
                WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"restSeconds"%'
                   OR "SESSION_TEMPLATE_CONTENT"::text LIKE '%"restAfterSeconds"%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "PROGRAM_TEMPLATES"
                SET "PROGRAM_TEMPLATE_BODY" = replace(replace("PROGRAM_TEMPLATE_BODY"::text,
                        '"restSecondsAfterSeries"', '"restSeconds"'),
                        '"restSecondsAfterExercices"', '"restAfterSeconds"')::jsonb
                WHERE "PROGRAM_TEMPLATE_BODY"::text LIKE '%"restSecondsAfterSeries"%'
                   OR "PROGRAM_TEMPLATE_BODY"::text LIKE '%"restSecondsAfterExercices"%';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "SESSION_TEMPLATES"
                SET "SESSION_TEMPLATE_CONTENT" = replace(replace("SESSION_TEMPLATE_CONTENT"::text,
                        '"restSecondsAfterSeries"', '"restSeconds"'),
                        '"restSecondsAfterExercices"', '"restAfterSeconds"')::jsonb
                WHERE "SESSION_TEMPLATE_CONTENT"::text LIKE '%"restSecondsAfterSeries"%'
                   OR "SESSION_TEMPLATE_CONTENT"::text LIKE '%"restSecondsAfterExercices"%';
                """);
        }
    }
}
