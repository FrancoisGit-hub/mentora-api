using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot12_2a_ExerciseReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EXERCISE_CATEGORY_ID",
                table: "EXERCISES",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EXERCISE_GRIP_TYPE_ID",
                table: "EXERCISES",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EXERCISE_LATERALITY_ID",
                table: "EXERCISES",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EXERCISE_MOVEMENT_PATTERN_ID",
                table: "EXERCISES",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EXERCISE_STARTING_POSITION_ID",
                table: "EXERCISES",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EXERCISE_TYPE_ID",
                table: "EXERCISES",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DATA_PATCHES",
                columns: table => new
                {
                    DATA_PATCH_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DATA_PATCH_APPLIED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DATA_PATCHES", x => x.DATA_PATCH_NAME);
                });

            migrationBuilder.CreateTable(
                name: "EXERCISE_CATEGORIES",
                columns: table => new
                {
                    EXERCISE_CATEGORY_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EXERCISE_CATEGORY_CODE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EXERCISE_CATEGORY_LABEL = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EXERCISE_CATEGORY_SORT_ORDER = table.Column<int>(type: "integer", nullable: false),
                    EXERCISE_CATEGORY_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false),
                    EXERCISE_CATEGORY_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EXERCISE_CATEGORY_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXERCISE_CATEGORIES", x => x.EXERCISE_CATEGORY_ID);
                });

            migrationBuilder.CreateTable(
                name: "EXERCISE_LATERALITIES",
                columns: table => new
                {
                    EXERCISE_LATERALITY_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EXERCISE_LATERALITY_CODE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EXERCISE_LATERALITY_LABEL = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EXERCISE_LATERALITY_SORT_ORDER = table.Column<int>(type: "integer", nullable: false),
                    EXERCISE_LATERALITY_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false),
                    EXERCISE_LATERALITY_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EXERCISE_LATERALITY_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXERCISE_LATERALITIES", x => x.EXERCISE_LATERALITY_ID);
                });

            migrationBuilder.CreateTable(
                name: "EXERCISE_SECONDARY_MUSCLE_GROUPS",
                columns: table => new
                {
                    EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXERCISE_SECONDARY_MUSCLE_GROUPS", x => new { x.EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID, x.EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID });
                    table.ForeignKey(
                        name: "FK_EXERCISE_SECONDARY_MUSCLE_GROUPS_EXERCISE",
                        column: x => x.EXERCISE_SECONDARY_MUSCLE_GROUP_EXERCISE_ID,
                        principalTable: "EXERCISES",
                        principalColumn: "EXERCISE_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EXERCISE_SECONDARY_MUSCLE_GROUPS_MUSCLE_GROUP",
                        column: x => x.EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID,
                        principalTable: "MUSCLE_GROUPS",
                        principalColumn: "MUSCLE_GROUP_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EXERCISE_TYPES",
                columns: table => new
                {
                    EXERCISE_TYPE_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EXERCISE_TYPE_CODE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EXERCISE_TYPE_LABEL = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EXERCISE_TYPE_SORT_ORDER = table.Column<int>(type: "integer", nullable: false),
                    EXERCISE_TYPE_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false),
                    EXERCISE_TYPE_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EXERCISE_TYPE_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXERCISE_TYPES", x => x.EXERCISE_TYPE_ID);
                });

            migrationBuilder.CreateTable(
                name: "GRIP_TYPES",
                columns: table => new
                {
                    GRIP_TYPE_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    GRIP_TYPE_CODE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    GRIP_TYPE_LABEL = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    GRIP_TYPE_SORT_ORDER = table.Column<int>(type: "integer", nullable: false),
                    GRIP_TYPE_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false),
                    GRIP_TYPE_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GRIP_TYPE_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GRIP_TYPES", x => x.GRIP_TYPE_ID);
                });

            migrationBuilder.CreateTable(
                name: "MOVEMENT_PATTERNS",
                columns: table => new
                {
                    MOVEMENT_PATTERN_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MOVEMENT_PATTERN_CODE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MOVEMENT_PATTERN_LABEL = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MOVEMENT_PATTERN_SORT_ORDER = table.Column<int>(type: "integer", nullable: false),
                    MOVEMENT_PATTERN_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false),
                    MOVEMENT_PATTERN_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MOVEMENT_PATTERN_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOVEMENT_PATTERNS", x => x.MOVEMENT_PATTERN_ID);
                });

            migrationBuilder.CreateTable(
                name: "STARTING_POSITIONS",
                columns: table => new
                {
                    STARTING_POSITION_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    STARTING_POSITION_CODE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    STARTING_POSITION_LABEL = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    STARTING_POSITION_SORT_ORDER = table.Column<int>(type: "integer", nullable: false),
                    STARTING_POSITION_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false),
                    STARTING_POSITION_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    STARTING_POSITION_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STARTING_POSITIONS", x => x.STARTING_POSITION_ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_CATEGORY",
                table: "EXERCISES",
                column: "EXERCISE_CATEGORY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_GRIP_TYPE",
                table: "EXERCISES",
                column: "EXERCISE_GRIP_TYPE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_LATERALITY",
                table: "EXERCISES",
                column: "EXERCISE_LATERALITY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_MOVEMENT_PATTERN",
                table: "EXERCISES",
                column: "EXERCISE_MOVEMENT_PATTERN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_STARTING_POSITION",
                table: "EXERCISES",
                column: "EXERCISE_STARTING_POSITION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISES_TYPE",
                table: "EXERCISES",
                column: "EXERCISE_TYPE_ID");

            migrationBuilder.CreateIndex(
                name: "UX_EXERCISE_CATEGORIES_CODE",
                table: "EXERCISE_CATEGORIES",
                column: "EXERCISE_CATEGORY_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EXERCISE_CATEGORIES_LABEL",
                table: "EXERCISE_CATEGORIES",
                column: "EXERCISE_CATEGORY_LABEL",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EXERCISE_LATERALITIES_CODE",
                table: "EXERCISE_LATERALITIES",
                column: "EXERCISE_LATERALITY_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EXERCISE_LATERALITIES_LABEL",
                table: "EXERCISE_LATERALITIES",
                column: "EXERCISE_LATERALITY_LABEL",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXERCISE_SECONDARY_MUSCLE_GROUPS_MUSCLE_GROUP",
                table: "EXERCISE_SECONDARY_MUSCLE_GROUPS",
                column: "EXERCISE_SECONDARY_MUSCLE_GROUP_MUSCLE_GROUP_ID");

            migrationBuilder.CreateIndex(
                name: "UX_EXERCISE_TYPES_CODE",
                table: "EXERCISE_TYPES",
                column: "EXERCISE_TYPE_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EXERCISE_TYPES_LABEL",
                table: "EXERCISE_TYPES",
                column: "EXERCISE_TYPE_LABEL",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GRIP_TYPES_CODE",
                table: "GRIP_TYPES",
                column: "GRIP_TYPE_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GRIP_TYPES_LABEL",
                table: "GRIP_TYPES",
                column: "GRIP_TYPE_LABEL",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MOVEMENT_PATTERNS_CODE",
                table: "MOVEMENT_PATTERNS",
                column: "MOVEMENT_PATTERN_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MOVEMENT_PATTERNS_LABEL",
                table: "MOVEMENT_PATTERNS",
                column: "MOVEMENT_PATTERN_LABEL",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_STARTING_POSITIONS_CODE",
                table: "STARTING_POSITIONS",
                column: "STARTING_POSITION_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_STARTING_POSITIONS_LABEL",
                table: "STARTING_POSITIONS",
                column: "STARTING_POSITION_LABEL",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EXERCISES_EXERCISE_CATEGORIES_EXERCISE_CATEGORY_ID",
                table: "EXERCISES",
                column: "EXERCISE_CATEGORY_ID",
                principalTable: "EXERCISE_CATEGORIES",
                principalColumn: "EXERCISE_CATEGORY_ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EXERCISES_EXERCISE_LATERALITIES_EXERCISE_LATERALITY_ID",
                table: "EXERCISES",
                column: "EXERCISE_LATERALITY_ID",
                principalTable: "EXERCISE_LATERALITIES",
                principalColumn: "EXERCISE_LATERALITY_ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EXERCISES_EXERCISE_TYPES_EXERCISE_TYPE_ID",
                table: "EXERCISES",
                column: "EXERCISE_TYPE_ID",
                principalTable: "EXERCISE_TYPES",
                principalColumn: "EXERCISE_TYPE_ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EXERCISES_GRIP_TYPES_EXERCISE_GRIP_TYPE_ID",
                table: "EXERCISES",
                column: "EXERCISE_GRIP_TYPE_ID",
                principalTable: "GRIP_TYPES",
                principalColumn: "GRIP_TYPE_ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EXERCISES_MOVEMENT_PATTERNS_EXERCISE_MOVEMENT_PATTERN_ID",
                table: "EXERCISES",
                column: "EXERCISE_MOVEMENT_PATTERN_ID",
                principalTable: "MOVEMENT_PATTERNS",
                principalColumn: "MOVEMENT_PATTERN_ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EXERCISES_STARTING_POSITIONS_EXERCISE_STARTING_POSITION_ID",
                table: "EXERCISES",
                column: "EXERCISE_STARTING_POSITION_ID",
                principalTable: "STARTING_POSITIONS",
                principalColumn: "STARTING_POSITION_ID",
                onDelete: ReferentialAction.Restrict);

            // Raw SQL objects (CLAUDE.md: CHECKs named CK_<TABLE>_<RULE>, created here with their Down).
            foreach (var (table, prefix) in ReferenceTables)
            {
                migrationBuilder.Sql(
                    $$"""
                    ALTER TABLE "{{table}}"
                    ADD CONSTRAINT "CK_{{table}}_CODE_FORMAT"
                    CHECK ("{{prefix}}_CODE" ~ '^[A-Z0-9_]{1,40}$');
                    """);

                migrationBuilder.Sql(
                    $"""
                    ALTER TABLE "{table}"
                    ADD CONSTRAINT "CK_{table}_SORT_ORDER"
                    CHECK ("{prefix}_SORT_ORDER" >= 0);
                    """);
            }

            // D7: Mentora exercise names unique case-insensitively (inactive rows included).
            // IX_EXERCISES_COACH_NAME (exact, per coach) is unchanged.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "UX_EXERCISES_MENTORA_NAME_CI"
                ON "EXERCISES" (lower(btrim("EXERCISE_NAME")))
                WHERE "EXERCISE_COACH_ID" IS NULL;
                """);
        }

        private static readonly (string Table, string Prefix)[] ReferenceTables =
        [
            ("EXERCISE_CATEGORIES", "EXERCISE_CATEGORY"),
            ("EXERCISE_TYPES", "EXERCISE_TYPE"),
            ("EXERCISE_LATERALITIES", "EXERCISE_LATERALITY"),
            ("MOVEMENT_PATTERNS", "MOVEMENT_PATTERN"),
            ("STARTING_POSITIONS", "STARTING_POSITION"),
            ("GRIP_TYPES", "GRIP_TYPE"),
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX "UX_EXERCISES_MENTORA_NAME_CI";""");

            foreach (var (table, _) in ReferenceTables)
            {
                migrationBuilder.Sql($"""ALTER TABLE "{table}" DROP CONSTRAINT "CK_{table}_SORT_ORDER";""");
                migrationBuilder.Sql($"""ALTER TABLE "{table}" DROP CONSTRAINT "CK_{table}_CODE_FORMAT";""");
            }

            migrationBuilder.DropForeignKey(
                name: "FK_EXERCISES_EXERCISE_CATEGORIES_EXERCISE_CATEGORY_ID",
                table: "EXERCISES");

            migrationBuilder.DropForeignKey(
                name: "FK_EXERCISES_EXERCISE_LATERALITIES_EXERCISE_LATERALITY_ID",
                table: "EXERCISES");

            migrationBuilder.DropForeignKey(
                name: "FK_EXERCISES_EXERCISE_TYPES_EXERCISE_TYPE_ID",
                table: "EXERCISES");

            migrationBuilder.DropForeignKey(
                name: "FK_EXERCISES_GRIP_TYPES_EXERCISE_GRIP_TYPE_ID",
                table: "EXERCISES");

            migrationBuilder.DropForeignKey(
                name: "FK_EXERCISES_MOVEMENT_PATTERNS_EXERCISE_MOVEMENT_PATTERN_ID",
                table: "EXERCISES");

            migrationBuilder.DropForeignKey(
                name: "FK_EXERCISES_STARTING_POSITIONS_EXERCISE_STARTING_POSITION_ID",
                table: "EXERCISES");

            migrationBuilder.DropTable(
                name: "DATA_PATCHES");

            migrationBuilder.DropTable(
                name: "EXERCISE_CATEGORIES");

            migrationBuilder.DropTable(
                name: "EXERCISE_LATERALITIES");

            migrationBuilder.DropTable(
                name: "EXERCISE_SECONDARY_MUSCLE_GROUPS");

            migrationBuilder.DropTable(
                name: "EXERCISE_TYPES");

            migrationBuilder.DropTable(
                name: "GRIP_TYPES");

            migrationBuilder.DropTable(
                name: "MOVEMENT_PATTERNS");

            migrationBuilder.DropTable(
                name: "STARTING_POSITIONS");

            migrationBuilder.DropIndex(
                name: "IX_EXERCISES_CATEGORY",
                table: "EXERCISES");

            migrationBuilder.DropIndex(
                name: "IX_EXERCISES_GRIP_TYPE",
                table: "EXERCISES");

            migrationBuilder.DropIndex(
                name: "IX_EXERCISES_LATERALITY",
                table: "EXERCISES");

            migrationBuilder.DropIndex(
                name: "IX_EXERCISES_MOVEMENT_PATTERN",
                table: "EXERCISES");

            migrationBuilder.DropIndex(
                name: "IX_EXERCISES_STARTING_POSITION",
                table: "EXERCISES");

            migrationBuilder.DropIndex(
                name: "IX_EXERCISES_TYPE",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_CATEGORY_ID",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_GRIP_TYPE_ID",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_LATERALITY_ID",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_MOVEMENT_PATTERN_ID",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_STARTING_POSITION_ID",
                table: "EXERCISES");

            migrationBuilder.DropColumn(
                name: "EXERCISE_TYPE_ID",
                table: "EXERCISES");
        }
    }
}
