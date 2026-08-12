using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot6_3_Programs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PROGRAMS",
                columns: table => new
                {
                    PROGRAM_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PROGRAM_COACH_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_TEMPLATE_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    PROGRAM_NAME = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PROGRAM_GOAL = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PROGRAM_START_DATE = table.Column<DateOnly>(type: "date", nullable: false),
                    PROGRAM_DURATION_WEEKS = table.Column<int>(type: "integer", nullable: false),
                    PROGRAM_WEEK_OFFSET = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PROGRAM_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'ACTIVE'"),
                    PROGRAM_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PROGRAM_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROGRAMS", x => x.PROGRAM_ID);
                    table.ForeignKey(
                        name: "FK_PROGRAMS_COACHES_PROGRAM_COACH_ID",
                        column: x => x.PROGRAM_COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PROGRAMS_MEMBERS_PROGRAM_MEMBER_ID",
                        column: x => x.PROGRAM_MEMBER_ID,
                        principalTable: "MEMBERS",
                        principalColumn: "MEMBER_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PROGRAM_BLOCKS",
                columns: table => new
                {
                    PROGRAM_BLOCK_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PROGRAM_BLOCK_PROGRAM_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_BLOCK_PARENT_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    PROGRAM_BLOCK_LEVEL = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PROGRAM_BLOCK_NAME = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PROGRAM_BLOCK_POSITION = table.Column<int>(type: "integer", nullable: false),
                    PROGRAM_BLOCK_WEEK_NUMBER = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROGRAM_BLOCKS", x => x.PROGRAM_BLOCK_ID);
                    table.ForeignKey(
                        name: "FK_PROGRAM_BLOCKS_PROGRAMS_PROGRAM_BLOCK_PROGRAM_ID",
                        column: x => x.PROGRAM_BLOCK_PROGRAM_ID,
                        principalTable: "PROGRAMS",
                        principalColumn: "PROGRAM_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PROGRAM_BLOCKS_PROGRAM_BLOCKS_PROGRAM_BLOCK_PARENT_ID",
                        column: x => x.PROGRAM_BLOCK_PARENT_ID,
                        principalTable: "PROGRAM_BLOCKS",
                        principalColumn: "PROGRAM_BLOCK_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PROGRAM_SESSIONS",
                columns: table => new
                {
                    PROGRAM_SESSION_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PROGRAM_SESSION_PROGRAM_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_SESSION_BLOCK_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_SESSION_COACH_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_SESSION_MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_SESSION_SESSION_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    PROGRAM_SESSION_NAME = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PROGRAM_SESSION_TYPE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PROGRAM_SESSION_DAY_OF_WEEK = table.Column<int>(type: "integer", nullable: false),
                    PROGRAM_SESSION_POSITION = table.Column<int>(type: "integer", nullable: false),
                    PROGRAM_SESSION_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'PLANNED'"),
                    PROGRAM_SESSION_COMPLETED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PROGRAM_SESSION_MEMBER_FEEDBACK = table.Column<string>(type: "text", nullable: true),
                    PROGRAM_SESSION_COACH_NOTE = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROGRAM_SESSIONS", x => x.PROGRAM_SESSION_ID);
                    table.ForeignKey(
                        name: "FK_PROGRAM_SESSIONS_PROGRAMS_PROGRAM_SESSION_PROGRAM_ID",
                        column: x => x.PROGRAM_SESSION_PROGRAM_ID,
                        principalTable: "PROGRAMS",
                        principalColumn: "PROGRAM_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PROGRAM_SESSIONS_PROGRAM_BLOCKS_PROGRAM_SESSION_BLOCK_ID",
                        column: x => x.PROGRAM_SESSION_BLOCK_ID,
                        principalTable: "PROGRAM_BLOCKS",
                        principalColumn: "PROGRAM_BLOCK_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PROGRAM_SESSIONS_SESSIONS_PROGRAM_SESSION_SESSION_ID",
                        column: x => x.PROGRAM_SESSION_SESSION_ID,
                        principalTable: "SESSIONS",
                        principalColumn: "SESSION_ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PROGRAM_CIRCUITS",
                columns: table => new
                {
                    PROGRAM_CIRCUIT_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PROGRAM_CIRCUIT_PROGRAM_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_CIRCUIT_PROGRAM_SESSION_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_CIRCUIT_NAME = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PROGRAM_CIRCUIT_POSITION = table.Column<int>(type: "integer", nullable: false),
                    PROGRAM_CIRCUIT_MODE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PROGRAM_CIRCUIT_ROUNDS = table.Column<int>(type: "integer", nullable: true),
                    PROGRAM_CIRCUIT_REST_BETWEEN_ROUNDS_SECONDS = table.Column<int>(type: "integer", nullable: true),
                    PROGRAM_CIRCUIT_NOTE = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROGRAM_CIRCUITS", x => x.PROGRAM_CIRCUIT_ID);
                    table.ForeignKey(
                        name: "FK_PROGRAM_CIRCUITS_PROGRAMS_PROGRAM_CIRCUIT_PROGRAM_ID",
                        column: x => x.PROGRAM_CIRCUIT_PROGRAM_ID,
                        principalTable: "PROGRAMS",
                        principalColumn: "PROGRAM_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PROGRAM_CIRCUITS_PROGRAM_SESSIONS_PROGRAM_CIRCUIT_PROGRAM_S~",
                        column: x => x.PROGRAM_CIRCUIT_PROGRAM_SESSION_ID,
                        principalTable: "PROGRAM_SESSIONS",
                        principalColumn: "PROGRAM_SESSION_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PROGRAM_EXERCISES",
                columns: table => new
                {
                    PROGRAM_EXERCISE_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PROGRAM_EXERCISE_PROGRAM_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_EXERCISE_CIRCUIT_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_EXERCISE_EXERCISE_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PROGRAM_EXERCISE_POSITION = table.Column<int>(type: "integer", nullable: false),
                    PROGRAM_EXERCISE_LOAD_TYPE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PROGRAM_EXERCISE_CUSTOM_NOTE = table.Column<string>(type: "text", nullable: true),
                    PROGRAM_EXERCISE_PRESCRIBED_SETS = table.Column<int>(type: "integer", nullable: true),
                    PROGRAM_EXERCISE_PRESCRIBED_REPS = table.Column<int>(type: "integer", nullable: true),
                    PROGRAM_EXERCISE_PRESCRIBED_WEIGHT_KG = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    PROGRAM_EXERCISE_REST_SECONDS = table.Column<int>(type: "integer", nullable: true),
                    PROGRAM_EXERCISE_WORK_SECONDS = table.Column<int>(type: "integer", nullable: true),
                    PROGRAM_EXERCISE_REST_WORK_SECONDS = table.Column<int>(type: "integer", nullable: true),
                    PROGRAM_EXERCISE_ACTUAL_SETS = table.Column<int>(type: "integer", nullable: true),
                    PROGRAM_EXERCISE_ACTUAL_REPS = table.Column<int>(type: "integer", nullable: true),
                    PROGRAM_EXERCISE_ACTUAL_WEIGHT_KG = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    PROGRAM_EXERCISE_ACTUAL_RPE = table.Column<int>(type: "integer", nullable: true),
                    PROGRAM_EXERCISE_MEMBER_FEEDBACK = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROGRAM_EXERCISES", x => x.PROGRAM_EXERCISE_ID);
                    table.ForeignKey(
                        name: "FK_PROGRAM_EXERCISES_EXERCISES_PROGRAM_EXERCISE_EXERCISE_ID",
                        column: x => x.PROGRAM_EXERCISE_EXERCISE_ID,
                        principalTable: "EXERCISES",
                        principalColumn: "EXERCISE_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PROGRAM_EXERCISES_PROGRAMS_PROGRAM_EXERCISE_PROGRAM_ID",
                        column: x => x.PROGRAM_EXERCISE_PROGRAM_ID,
                        principalTable: "PROGRAMS",
                        principalColumn: "PROGRAM_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PROGRAM_EXERCISES_PROGRAM_CIRCUITS_PROGRAM_EXERCISE_CIRCUIT~",
                        column: x => x.PROGRAM_EXERCISE_CIRCUIT_ID,
                        principalTable: "PROGRAM_CIRCUITS",
                        principalColumn: "PROGRAM_CIRCUIT_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_BLOCKS_PROGRAM_BLOCK_PARENT_ID",
                table: "PROGRAM_BLOCKS",
                column: "PROGRAM_BLOCK_PARENT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_BLOCKS_PROGRAM_BLOCK_PROGRAM_ID",
                table: "PROGRAM_BLOCKS",
                column: "PROGRAM_BLOCK_PROGRAM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_CIRCUITS_PROGRAM_CIRCUIT_PROGRAM_ID",
                table: "PROGRAM_CIRCUITS",
                column: "PROGRAM_CIRCUIT_PROGRAM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_CIRCUITS_PROGRAM_CIRCUIT_PROGRAM_SESSION_ID",
                table: "PROGRAM_CIRCUITS",
                column: "PROGRAM_CIRCUIT_PROGRAM_SESSION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_EXERCISES_PROGRAM_EXERCISE_CIRCUIT_ID",
                table: "PROGRAM_EXERCISES",
                column: "PROGRAM_EXERCISE_CIRCUIT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_EXERCISES_PROGRAM_EXERCISE_EXERCISE_ID",
                table: "PROGRAM_EXERCISES",
                column: "PROGRAM_EXERCISE_EXERCISE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_EXERCISES_PROGRAM_EXERCISE_PROGRAM_ID",
                table: "PROGRAM_EXERCISES",
                column: "PROGRAM_EXERCISE_PROGRAM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_SESSIONS_PROGRAM_SESSION_BLOCK_ID",
                table: "PROGRAM_SESSIONS",
                column: "PROGRAM_SESSION_BLOCK_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_SESSIONS_PROGRAM_SESSION_PROGRAM_ID",
                table: "PROGRAM_SESSIONS",
                column: "PROGRAM_SESSION_PROGRAM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAM_SESSIONS_PROGRAM_SESSION_SESSION_ID",
                table: "PROGRAM_SESSIONS",
                column: "PROGRAM_SESSION_SESSION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAMS_PROGRAM_COACH_ID",
                table: "PROGRAMS",
                column: "PROGRAM_COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROGRAMS_PROGRAM_MEMBER_ID",
                table: "PROGRAMS",
                column: "PROGRAM_MEMBER_ID");

            // Partial unique index: at most one ACTIVE program per member. EF's HasIndex()
            // cannot express this the way the task wants it kept — explicit raw SQL, same
            // treatment as the NULLS NOT DISTINCT indexes on EXERCISES / PROGRAM_TEMPLATES.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_PROGRAMS_MEMBER_ACTIVE"
                  ON "PROGRAMS" ("PROGRAM_MEMBER_ID")
                  WHERE "PROGRAM_STATUS" = 'ACTIVE';
                """);

            // CHECK constraints — cross-column business rules kept as explicit raw SQL rather
            // than the fluent HasCheckConstraint API.
            migrationBuilder.Sql(
                """
                ALTER TABLE "PROGRAM_BLOCKS" ADD CONSTRAINT "CK_PROGRAM_BLOCKS_MACROCYCLE_HAS_NO_PARENT"
                  CHECK ("PROGRAM_BLOCK_LEVEL" <> 'MACROCYCLE' OR "PROGRAM_BLOCK_PARENT_ID" IS NULL);
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "PROGRAM_BLOCKS" ADD CONSTRAINT "CK_PROGRAM_BLOCKS_WEEK_NUMBER_MICROCYCLE"
                  CHECK (("PROGRAM_BLOCK_LEVEL" = 'MICROCYCLE' AND "PROGRAM_BLOCK_WEEK_NUMBER" IS NOT NULL)
                      OR ("PROGRAM_BLOCK_LEVEL" <> 'MICROCYCLE' AND "PROGRAM_BLOCK_WEEK_NUMBER" IS NULL));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "PROGRAM_SESSIONS" ADD CONSTRAINT "CK_PROGRAM_SESSIONS_A_DISTANCE_HAS_NO_BOOKING"
                  CHECK ("PROGRAM_SESSION_TYPE" <> 'A_DISTANCE' OR "PROGRAM_SESSION_SESSION_ID" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PROGRAM_EXERCISES");

            migrationBuilder.DropTable(
                name: "PROGRAM_CIRCUITS");

            migrationBuilder.DropTable(
                name: "PROGRAM_SESSIONS");

            migrationBuilder.DropTable(
                name: "PROGRAM_BLOCKS");

            migrationBuilder.DropTable(
                name: "PROGRAMS");
        }
    }
}
