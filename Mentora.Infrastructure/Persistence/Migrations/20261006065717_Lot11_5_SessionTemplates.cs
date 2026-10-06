using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot11_5_SessionTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SESSION_TEMPLATES",
                columns: table => new
                {
                    SESSION_TEMPLATE_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    SESSION_TEMPLATE_SOURCE_CODE = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SESSION_TEMPLATE_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SESSION_TEMPLATE_DESCRIPTION = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SESSION_TEMPLATE_NATURE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SESSION_TEMPLATE_SESSION_TYPE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SESSION_TEMPLATE_CONTENT = table.Column<string>(type: "jsonb", nullable: false),
                    SESSION_TEMPLATE_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SESSION_TEMPLATE_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SESSION_TEMPLATE_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SESSION_TEMPLATES", x => x.SESSION_TEMPLATE_ID);
                    table.ForeignKey(
                        name: "FK_SESSION_TEMPLATES_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SESSION_TEMPLATES_COACH_ID",
                table: "SESSION_TEMPLATES",
                column: "COACH_ID");

            migrationBuilder.Sql("""
                ALTER TABLE "SESSION_TEMPLATES" ADD CONSTRAINT "CK_SESSION_TEMPLATES_SOURCE_CODE" CHECK (
                    ("COACH_ID" IS NULL AND "SESSION_TEMPLATE_SOURCE_CODE" IS NOT NULL)
                    OR ("COACH_ID" IS NOT NULL AND "SESSION_TEMPLATE_SOURCE_CODE" IS NULL));
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "SESSION_TEMPLATES" ADD CONSTRAINT "CK_SESSION_TEMPLATES_NATURE"
                CHECK ("SESSION_TEMPLATE_NATURE" IN ('STANDARD', 'EVALUATION'));
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "SESSION_TEMPLATES" ADD CONSTRAINT "CK_SESSION_TEMPLATES_SESSION_TYPE"
                CHECK ("SESSION_TEMPLATE_SESSION_TYPE" IN ('PRESENTIEL_SOLO', 'PRESENTIEL_GROUPE', 'VISIO', 'AUTONOME', 'PRESENTIEL_OU_VISIO'));
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "SESSION_TEMPLATES" ADD CONSTRAINT "CK_SESSION_TEMPLATES_CONTENT"
                CHECK (jsonb_typeof("SESSION_TEMPLATE_CONTENT") = 'array' AND jsonb_array_length("SESSION_TEMPLATE_CONTENT") >= 1);
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_SESSION_TEMPLATES_SOURCE_CODE"
                ON "SESSION_TEMPLATES" ("SESSION_TEMPLATE_SOURCE_CODE")
                WHERE "COACH_ID" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX "UX_SESSION_TEMPLATES_SOURCE_CODE";""");
            migrationBuilder.Sql("""ALTER TABLE "SESSION_TEMPLATES" DROP CONSTRAINT "CK_SESSION_TEMPLATES_CONTENT";""");
            migrationBuilder.Sql("""ALTER TABLE "SESSION_TEMPLATES" DROP CONSTRAINT "CK_SESSION_TEMPLATES_SESSION_TYPE";""");
            migrationBuilder.Sql("""ALTER TABLE "SESSION_TEMPLATES" DROP CONSTRAINT "CK_SESSION_TEMPLATES_NATURE";""");
            migrationBuilder.Sql("""ALTER TABLE "SESSION_TEMPLATES" DROP CONSTRAINT "CK_SESSION_TEMPLATES_SOURCE_CODE";""");

            migrationBuilder.DropTable(
                name: "SESSION_TEMPLATES");
        }
    }
}
