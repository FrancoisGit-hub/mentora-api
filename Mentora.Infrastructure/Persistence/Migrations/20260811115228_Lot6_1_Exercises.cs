using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot6_1_Exercises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EXERCISES",
                columns: table => new
                {
                    EXERCISE_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EXERCISE_COACH_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    EXERCISE_NAME = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EXERCISE_DESCRIPTION = table.Column<string>(type: "text", nullable: true),
                    EXERCISE_INSTRUCTIONS = table.Column<string>(type: "text", nullable: true),
                    EXERCISE_VIDEO_URL = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EXERCISE_IMAGE_URL = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EXERCISE_MUSCLE_GROUP = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EXERCISE_EQUIPMENT = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EXERCISE_IS_POLYARTICULAR = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EXERCISE_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EXERCISE_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EXERCISE_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXERCISES", x => x.EXERCISE_ID);
                    table.ForeignKey(
                        name: "FK_EXERCISES_COACHES_EXERCISE_COACH_ID",
                        column: x => x.EXERCISE_COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_COACH_ACTIVE",
                table: "EXERCISES",
                columns: new[] { "EXERCISE_COACH_ID", "EXERCISE_IS_ACTIVE" });

            // NULLS NOT DISTINCT: two Mentora entries (EXERCISE_COACH_ID IS NULL) cannot share
            // a name either. EF's HasIndex().IsUnique() cannot express this — Postgres treats
            // NULLs as distinct by default, so it must be created via raw SQL.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_EXERCISES_COACH_NAME"
                  ON "EXERCISES" ("EXERCISE_COACH_ID", "EXERCISE_NAME")
                  NULLS NOT DISTINCT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EXERCISES");
        }
    }
}
