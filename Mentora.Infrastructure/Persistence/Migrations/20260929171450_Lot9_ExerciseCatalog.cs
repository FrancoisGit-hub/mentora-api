using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot9_ExerciseCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_BREATHING",
                table: "EXERCISES",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_CATEGORY",
                table: "EXERCISES",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_COACH_TIP",
                table: "EXERCISES",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "EXERCISE_COMMON_MISTAKES",
                table: "EXERCISES",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_GRIP_TYPE",
                table: "EXERCISES",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_LATERALITY",
                table: "EXERCISES",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_MIN_LEVEL",
                table: "EXERCISES",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_MOVEMENT_PATTERN",
                table: "EXERCISES",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_NAME_EN",
                table: "EXERCISES",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_PRECAUTIONS",
                table: "EXERCISES",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EXERCISE_PRIMARY_EQUIPMENT_ID",
                table: "EXERCISES",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EXERCISE_PRIMARY_MUSCLE_GROUP_ID",
                table: "EXERCISES",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_SEARCH_TAGS",
                table: "EXERCISES",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EXERCISE_SECONDARY_EQUIPMENT_ID",
                table: "EXERCISES",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "EXERCISE_SECONDARY_MUSCLES",
                table: "EXERCISES",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_SOURCE_CODE",
                table: "EXERCISES",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_STARTING_POSITION",
                table: "EXERCISES",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXERCISE_TYPE",
                table: "EXERCISES",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EQUIPMENT",
                columns: table => new
                {
                    EQUIPMENT_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EQUIPMENT_SOURCE_CODE = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EQUIPMENT_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EQUIPMENT_CATEGORY = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    EQUIPMENT_SUBCATEGORY = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    EQUIPMENT_LEGACY_CODE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "OTHER"),
                    EQUIPMENT_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EQUIPMENT_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EQUIPMENT_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQUIPMENT", x => x.EQUIPMENT_ID);
                });

            migrationBuilder.CreateTable(
                name: "EXERCISE_VIDEOS",
                columns: table => new
                {
                    EXERCISE_VIDEO_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EXERCISE_VIDEO_EXERCISE_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    EXERCISE_VIDEO_MUX_ASSET_ID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EXERCISE_VIDEO_MUX_PLAYBACK_ID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EXERCISE_VIDEO_PLAYBACK_POLICY = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValueSql: "'PUBLIC'"),
                    EXERCISE_VIDEO_STATUS = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValueSql: "'WAITING'"),
                    EXERCISE_VIDEO_DURATION_SECONDS = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    EXERCISE_VIDEO_ASPECT_RATIO = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EXERCISE_VIDEO_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EXERCISE_VIDEO_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXERCISE_VIDEOS", x => x.EXERCISE_VIDEO_ID);
                    table.ForeignKey(
                        name: "FK_EXERCISE_VIDEOS_EXERCISES_EXERCISE_VIDEO_EXERCISE_ID",
                        column: x => x.EXERCISE_VIDEO_EXERCISE_ID,
                        principalTable: "EXERCISES",
                        principalColumn: "EXERCISE_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MUSCLE_GROUPS",
                columns: table => new
                {
                    MUSCLE_GROUP_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MUSCLE_GROUP_NAME = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MUSCLE_GROUP_SORT_ORDER = table.Column<int>(type: "integer", nullable: false),
                    MUSCLE_GROUP_LEGACY_CODE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MUSCLE_GROUP_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MUSCLE_GROUP_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MUSCLE_GROUPS", x => x.MUSCLE_GROUP_ID);
                });

            migrationBuilder.CreateTable(
                name: "OBJECTIVES",
                columns: table => new
                {
                    OBJECTIVE_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OBJECTIVE_NAME = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OBJECTIVE_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OBJECTIVE_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OBJECTIVES", x => x.OBJECTIVE_ID);
                });

            migrationBuilder.CreateTable(
                name: "EXERCISE_OBJECTIVES",
                columns: table => new
                {
                    EXERCISE_OBJECTIVE_EXERCISE_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    EXERCISE_OBJECTIVE_OBJECTIVE_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXERCISE_OBJECTIVES", x => new { x.EXERCISE_OBJECTIVE_EXERCISE_ID, x.EXERCISE_OBJECTIVE_OBJECTIVE_ID });
                    table.ForeignKey(
                        name: "FK_EXERCISE_OBJECTIVES_EXERCISES_EXERCISE_OBJECTIVE_EXERCISE_ID",
                        column: x => x.EXERCISE_OBJECTIVE_EXERCISE_ID,
                        principalTable: "EXERCISES",
                        principalColumn: "EXERCISE_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EXERCISE_OBJECTIVES_OBJECTIVES_EXERCISE_OBJECTIVE_OBJECTIVE~",
                        column: x => x.EXERCISE_OBJECTIVE_OBJECTIVE_ID,
                        principalTable: "OBJECTIVES",
                        principalColumn: "OBJECTIVE_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_MIN_LEVEL",
                table: "EXERCISES",
                column: "EXERCISE_MIN_LEVEL");

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_PRIMARY_EQUIPMENT",
                table: "EXERCISES",
                column: "EXERCISE_PRIMARY_EQUIPMENT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_PRIMARY_MUSCLE_GROUP",
                table: "EXERCISES",
                column: "EXERCISE_PRIMARY_MUSCLE_GROUP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_SECONDARY_EQUIPMENT",
                table: "EXERCISES",
                column: "EXERCISE_SECONDARY_EQUIPMENT_ID");

            migrationBuilder.CreateIndex(
                name: "UX_EQUIPMENT_NAME",
                table: "EQUIPMENT",
                column: "EQUIPMENT_NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISE_OBJECTIVES_OBJECTIVE",
                table: "EXERCISE_OBJECTIVES",
                column: "EXERCISE_OBJECTIVE_OBJECTIVE_ID");

            migrationBuilder.CreateIndex(
                name: "UX_EXERCISE_VIDEOS_EXERCISE",
                table: "EXERCISE_VIDEOS",
                column: "EXERCISE_VIDEO_EXERCISE_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EXERCISE_VIDEOS_MUX_ASSET",
                table: "EXERCISE_VIDEOS",
                column: "EXERCISE_VIDEO_MUX_ASSET_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MUSCLE_GROUPS_NAME",
                table: "MUSCLE_GROUPS",
                column: "MUSCLE_GROUP_NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_OBJECTIVES_NAME",
                table: "OBJECTIVES",
                column: "OBJECTIVE_NAME",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EXERCISES_EQUIPMENT_EXERCISE_PRIMARY_EQUIPMENT_ID",
                table: "EXERCISES",
                column: "EXERCISE_PRIMARY_EQUIPMENT_ID",
                principalTable: "EQUIPMENT",
                principalColumn: "EQUIPMENT_ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EXERCISES_EQUIPMENT_EXERCISE_SECONDARY_EQUIPMENT_ID",
                table: "EXERCISES",
                column: "EXERCISE_SECONDARY_EQUIPMENT_ID",
                principalTable: "EQUIPMENT",
                principalColumn: "EQUIPMENT_ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EXERCISES_MUSCLE_GROUPS_EXERCISE_PRIMARY_MUSCLE_GROUP_ID",
                table: "EXERCISES",
                column: "EXERCISE_PRIMARY_MUSCLE_GROUP_ID",
                principalTable: "MUSCLE_GROUPS",
                principalColumn: "MUSCLE_GROUP_ID",
                onDelete: ReferentialAction.Restrict);

            // CHECK constraints and partial unique indexes: raw SQL (not expressible through the model).
            migrationBuilder.Sql(
                """
                ALTER TABLE "MUSCLE_GROUPS"
                ADD CONSTRAINT "CK_MUSCLE_GROUPS_LEGACY_CODE" CHECK (
                    "MUSCLE_GROUP_LEGACY_CODE" IN ('CHEST','BACK','SHOULDERS','BICEPS','TRICEPS','FOREARMS','ABS','QUADRICEPS','HAMSTRINGS','GLUTES','CALVES','FULL_BODY','CARDIO')
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "EQUIPMENT"
                ADD CONSTRAINT "CK_EQUIPMENT_LEGACY_CODE" CHECK (
                    "EQUIPMENT_LEGACY_CODE" IN ('BODYWEIGHT','BARBELL','DUMBBELL','KETTLEBELL','MACHINE','CABLE','ELASTIC','OTHER')
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "EXERCISE_VIDEOS"
                ADD CONSTRAINT "CK_EXERCISE_VIDEOS_POLICY" CHECK (
                    "EXERCISE_VIDEO_PLAYBACK_POLICY" IN ('PUBLIC','SIGNED')
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "EXERCISE_VIDEOS"
                ADD CONSTRAINT "CK_EXERCISE_VIDEOS_STATUS" CHECK (
                    "EXERCISE_VIDEO_STATUS" IN ('WAITING','READY','ERRORED')
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "EXERCISES"
                ADD CONSTRAINT "CK_EXERCISES_MIN_LEVEL" CHECK (
                    "EXERCISE_MIN_LEVEL" IS NULL OR "EXERCISE_MIN_LEVEL" IN ('BEGINNER','INTERMEDIATE','ADVANCED')
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "UX_EXERCISES_SOURCE_CODE"
                ON "EXERCISES" ("EXERCISE_SOURCE_CODE")
                WHERE "EXERCISE_SOURCE_CODE" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "UX_EQUIPMENT_SOURCE_CODE"
                ON "EQUIPMENT" ("EQUIPMENT_SOURCE_CODE")
                WHERE "EQUIPMENT_SOURCE_CODE" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX "UX_EQUIPMENT_SOURCE_CODE";
                DROP INDEX "UX_EXERCISES_SOURCE_CODE";
                ALTER TABLE "EXERCISES" DROP CONSTRAINT "CK_EXERCISES_MIN_LEVEL";
                ALTER TABLE "EXERCISE_VIDEOS" DROP CONSTRAINT "CK_EXERCISE_VIDEOS_STATUS";
                ALTER TABLE "EXERCISE_VIDEOS" DROP CONSTRAINT "CK_EXERCISE_VIDEOS_POLICY";
                ALTER TABLE "EQUIPMENT" DROP CONSTRAINT "CK_EQUIPMENT_LEGACY_CODE";
                ALTER TABLE "MUSCLE_GROUPS" DROP CONSTRAINT "CK_MUSCLE_GROUPS_LEGACY_CODE";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_EXERCISES_EQUIPMENT_EXERCISE_PRIMARY_EQUIPMENT_ID",
                table: "EXERCISES");

            migrationBuilder.DropForeignKey(
                name: "FK_EXERCISES_EQUIPMENT_EXERCISE_SECONDARY_EQUIPMENT_ID",
                table: "EXERCISES");

            migrationBuilder.DropForeignKey(
                name: "FK_EXERCISES_MUSCLE_GROUPS_EXERCISE_PRIMARY_MUSCLE_GROUP_ID",
                table: "EXERCISES");

            migrationBuilder.DropTable(
                name: "EQUIPMENT");

            migrationBuilder.DropTable(
                name: "EXERCISE_OBJECTIVES");

            migrationBuilder.DropTable(
                name: "EXERCISE_VIDEOS");

            migrationBuilder.DropTable(
                name: "MUSCLE_GROUPS");

            migrationBuilder.DropTable(
                name: "OBJECTIVES");

            migrationBuilder.DropIndex(
                name: "IX_EXERCISES_MIN_LEVEL",
                table: "EXERCISES");

            migrationBuilder.DropIndex(
                name: "IX_EXERCISES_PRIMARY_EQUIPMENT",
                table: "EXERCISES");

            migrationBuilder.DropIndex(
                name: "IX_EXERCISES_PRIMARY_MUSCLE_GROUP",
                table: "EXERCISES");

            migrationBuilder.DropIndex(
                name: "IX_EXERCISES_SECONDARY_EQUIPMENT",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_BREATHING",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_CATEGORY",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_COACH_TIP",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_COMMON_MISTAKES",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_GRIP_TYPE",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_LATERALITY",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_MIN_LEVEL",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_MOVEMENT_PATTERN",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_NAME_EN",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_PRECAUTIONS",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_PRIMARY_EQUIPMENT_ID",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_PRIMARY_MUSCLE_GROUP_ID",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_SEARCH_TAGS",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_SECONDARY_EQUIPMENT_ID",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_SECONDARY_MUSCLES",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_SOURCE_CODE",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_STARTING_POSITION",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_TYPE",
                table: "EXERCISES");
        }
    }
}
