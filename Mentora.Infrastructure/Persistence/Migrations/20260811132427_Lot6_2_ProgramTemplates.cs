using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot6_2_ProgramTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PROGRAM_TEMPLATES",
                columns: table => new
                {
                    PROGRAM_TEMPLATE_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PROGRAM_TEMPLATE_COACH_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    PROGRAM_TEMPLATE_NAME = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PROGRAM_TEMPLATE_DESCRIPTION = table.Column<string>(type: "text", nullable: true),
                    PROGRAM_TEMPLATE_GOAL = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PROGRAM_TEMPLATE_DURATION_WEEKS = table.Column<int>(type: "integer", nullable: false),
                    PROGRAM_TEMPLATE_BODY = table.Column<string>(type: "jsonb", nullable: false),
                    PROGRAM_TEMPLATE_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PROGRAM_TEMPLATE_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PROGRAM_TEMPLATE_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROGRAM_TEMPLATES", x => x.PROGRAM_TEMPLATE_ID);
                    table.ForeignKey(
                        name: "FK_PROGRAM_TEMPLATES_COACHES_PROGRAM_TEMPLATE_COACH_ID",
                        column: x => x.PROGRAM_TEMPLATE_COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_TEMPLATES_COACH_ACTIVE",
                table: "PROGRAM_TEMPLATES",
                columns: new[] { "PROGRAM_TEMPLATE_COACH_ID", "PROGRAM_TEMPLATE_IS_ACTIVE" });

            // NULLS NOT DISTINCT: two Mentora templates (PROGRAM_TEMPLATE_COACH_ID IS NULL)
            // cannot share a name either. EF's HasIndex().IsUnique() cannot express this —
            // Postgres treats NULLs as distinct by default, so it must be created via raw SQL.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_PROGRAM_TEMPLATES_COACH_NAME"
                  ON "PROGRAM_TEMPLATES" ("PROGRAM_TEMPLATE_COACH_ID", "PROGRAM_TEMPLATE_NAME")
                  NULLS NOT DISTINCT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PROGRAM_TEMPLATES");
        }
    }
}
