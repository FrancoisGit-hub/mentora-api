using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot10_Quiz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QUIZ_DEVICE_CLAIMS",
                columns: table => new
                {
                    QUIZ_DEVICE_CLAIM_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    QUIZ_DEVICE_CLAIM_DEVICE_ID = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QUIZ_DEVICE_CLAIM_COACH_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    QUIZ_DEVICE_CLAIM_POINTS = table.Column<int>(type: "integer", nullable: false),
                    QUIZ_DEVICE_CLAIM_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QUIZ_DEVICE_CLAIMS", x => x.QUIZ_DEVICE_CLAIM_ID);
                    table.ForeignKey(
                        name: "FK_QUIZ_DEVICE_CLAIMS_COACHES_QUIZ_DEVICE_CLAIM_COACH_ID",
                        column: x => x.QUIZ_DEVICE_CLAIM_COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QUIZ_QUESTIONS",
                columns: table => new
                {
                    QUIZ_QUESTION_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    QUIZ_QUESTION_SOURCE_CODE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QUIZ_QUESTION_SUBJECT_CODE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    QUIZ_QUESTION_SUBJECT = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    QUIZ_QUESTION_CHAPTER_CODE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    QUIZ_QUESTION_CHAPTER = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    QUIZ_QUESTION_CARD_CODE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    QUIZ_QUESTION_CARD_TITLE = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    QUIZ_QUESTION_TEXT = table.Column<string>(type: "text", nullable: false),
                    QUIZ_QUESTION_ANSWER_A = table.Column<string>(type: "text", nullable: false),
                    QUIZ_QUESTION_ANSWER_B = table.Column<string>(type: "text", nullable: false),
                    QUIZ_QUESTION_ANSWER_C = table.Column<string>(type: "text", nullable: false),
                    QUIZ_QUESTION_ANSWER_D = table.Column<string>(type: "text", nullable: false),
                    QUIZ_QUESTION_CORRECT_ANSWER = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    QUIZ_QUESTION_EXPLANATION = table.Column<string>(type: "text", nullable: true),
                    QUIZ_QUESTION_DIFFICULTY = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    QUIZ_QUESTION_POINTS = table.Column<int>(type: "integer", nullable: false),
                    QUIZ_QUESTION_SOURCE_STATUS = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    QUIZ_QUESTION_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    QUIZ_QUESTION_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QUIZ_QUESTION_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QUIZ_QUESTIONS", x => x.QUIZ_QUESTION_ID);
                });

            migrationBuilder.CreateTable(
                name: "QUIZ_SESSIONS",
                columns: table => new
                {
                    QUIZ_SESSION_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    QUIZ_SESSION_COACH_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    QUIZ_SESSION_DEVICE_ID = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    QUIZ_SESSION_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QUIZ_SESSION_QUESTION_COUNT = table.Column<int>(type: "integer", nullable: false),
                    QUIZ_SESSION_SCORE = table.Column<int>(type: "integer", nullable: true),
                    QUIZ_SESSION_CORRECT_COUNT = table.Column<int>(type: "integer", nullable: true),
                    QUIZ_SESSION_COMPLETED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QUIZ_SESSION_EXPIRES_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QUIZ_SESSION_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QUIZ_SESSION_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QUIZ_SESSIONS", x => x.QUIZ_SESSION_ID);
                    table.ForeignKey(
                        name: "FK_QUIZ_SESSIONS_COACHES_QUIZ_SESSION_COACH_ID",
                        column: x => x.QUIZ_SESSION_COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QUIZ_SESSION_QUESTIONS",
                columns: table => new
                {
                    QUIZ_SESSION_QUESTION_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    QUIZ_SESSION_QUESTION_SESSION_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    QUIZ_SESSION_QUESTION_QUESTION_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    QUIZ_SESSION_QUESTION_POSITION = table.Column<int>(type: "integer", nullable: false),
                    QUIZ_SESSION_QUESTION_CHOSEN_ANSWER = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    QUIZ_SESSION_QUESTION_IS_CORRECT = table.Column<bool>(type: "boolean", nullable: true),
                    QUIZ_SESSION_QUESTION_POINTS_EARNED = table.Column<int>(type: "integer", nullable: true),
                    QUIZ_SESSION_QUESTION_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QUIZ_SESSION_QUESTION_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QUIZ_SESSION_QUESTIONS", x => x.QUIZ_SESSION_QUESTION_ID);
                    table.ForeignKey(
                        name: "FK_QUIZ_SESSION_QUESTIONS_QUIZ_QUESTIONS_QUIZ_SESSION_QUESTION~",
                        column: x => x.QUIZ_SESSION_QUESTION_QUESTION_ID,
                        principalTable: "QUIZ_QUESTIONS",
                        principalColumn: "QUIZ_QUESTION_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QUIZ_SESSION_QUESTIONS_QUIZ_SESSIONS_QUIZ_SESSION_QUESTION_~",
                        column: x => x.QUIZ_SESSION_QUESTION_SESSION_ID,
                        principalTable: "QUIZ_SESSIONS",
                        principalColumn: "QUIZ_SESSION_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QUIZ_DEVICE_CLAIMS_COACH_ID",
                table: "QUIZ_DEVICE_CLAIMS",
                column: "QUIZ_DEVICE_CLAIM_COACH_ID");

            migrationBuilder.CreateIndex(
                name: "UX_QUIZ_DEVICE_CLAIMS_DEVICE_ID",
                table: "QUIZ_DEVICE_CLAIMS",
                column: "QUIZ_DEVICE_CLAIM_DEVICE_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_QUIZ_QUESTIONS_SOURCE_CODE",
                table: "QUIZ_QUESTIONS",
                column: "QUIZ_QUESTION_SOURCE_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QUIZ_SESSION_QUESTIONS_QUESTION_ID",
                table: "QUIZ_SESSION_QUESTIONS",
                column: "QUIZ_SESSION_QUESTION_QUESTION_ID");

            migrationBuilder.CreateIndex(
                name: "UX_QUIZ_SESSION_QUESTIONS_SESSION_POSITION",
                table: "QUIZ_SESSION_QUESTIONS",
                columns: new[] { "QUIZ_SESSION_QUESTION_SESSION_ID", "QUIZ_SESSION_QUESTION_POSITION" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_QUIZ_SESSION_QUESTIONS_SESSION_QUESTION",
                table: "QUIZ_SESSION_QUESTIONS",
                columns: new[] { "QUIZ_SESSION_QUESTION_SESSION_ID", "QUIZ_SESSION_QUESTION_QUESTION_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QUIZ_SESSIONS_COACH_ID",
                table: "QUIZ_SESSIONS",
                column: "QUIZ_SESSION_COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QUIZ_SESSIONS_DEVICE_ID",
                table: "QUIZ_SESSIONS",
                column: "QUIZ_SESSION_DEVICE_ID");

            // CHECK constraints: raw SQL (not expressible through the model).
            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_QUESTIONS"
                ADD CONSTRAINT "CK_QUIZ_QUESTIONS_CORRECT_ANSWER" CHECK (
                    "QUIZ_QUESTION_CORRECT_ANSWER" IN ('A','B','C','D')
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_QUESTIONS"
                ADD CONSTRAINT "CK_QUIZ_QUESTIONS_POINTS" CHECK (
                    "QUIZ_QUESTION_POINTS" >= 0
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_SESSIONS"
                ADD CONSTRAINT "CK_QUIZ_SESSIONS_STATUS" CHECK (
                    "QUIZ_SESSION_STATUS" IN ('IN_PROGRESS','COMPLETED')
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_SESSIONS"
                ADD CONSTRAINT "CK_QUIZ_SESSIONS_OWNER" CHECK (
                    "QUIZ_SESSION_COACH_ID" IS NOT NULL OR "QUIZ_SESSION_DEVICE_ID" IS NOT NULL
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_SESSIONS"
                ADD CONSTRAINT "CK_QUIZ_SESSIONS_QUESTION_COUNT" CHECK (
                    "QUIZ_SESSION_QUESTION_COUNT" BETWEEN 1 AND 50
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_SESSIONS"
                ADD CONSTRAINT "CK_QUIZ_SESSIONS_COMPLETION" CHECK (
                    (
                        "QUIZ_SESSION_STATUS" = 'COMPLETED'
                        AND "QUIZ_SESSION_SCORE" IS NOT NULL
                        AND "QUIZ_SESSION_CORRECT_COUNT" IS NOT NULL
                        AND "QUIZ_SESSION_COMPLETED_DATE" IS NOT NULL
                    )
                    OR (
                        "QUIZ_SESSION_STATUS" = 'IN_PROGRESS'
                        AND "QUIZ_SESSION_SCORE" IS NULL
                        AND "QUIZ_SESSION_CORRECT_COUNT" IS NULL
                        AND "QUIZ_SESSION_COMPLETED_DATE" IS NULL
                    )
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_SESSION_QUESTIONS"
                ADD CONSTRAINT "CK_QUIZ_SESSION_QUESTIONS_POSITION" CHECK (
                    "QUIZ_SESSION_QUESTION_POSITION" BETWEEN 1 AND 50
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_SESSION_QUESTIONS"
                ADD CONSTRAINT "CK_QUIZ_SESSION_QUESTIONS_CHOSEN_ANSWER" CHECK (
                    "QUIZ_SESSION_QUESTION_CHOSEN_ANSWER" IS NULL
                    OR "QUIZ_SESSION_QUESTION_CHOSEN_ANSWER" IN ('A','B','C','D')
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_SESSION_QUESTIONS"
                ADD CONSTRAINT "CK_QUIZ_SESSION_QUESTIONS_POINTS_EARNED" CHECK (
                    "QUIZ_SESSION_QUESTION_POINTS_EARNED" IS NULL
                    OR "QUIZ_SESSION_QUESTION_POINTS_EARNED" >= 0
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_SESSION_QUESTIONS"
                ADD CONSTRAINT "CK_QUIZ_SESSION_QUESTIONS_ANSWER_STATE" CHECK (
                    (
                        "QUIZ_SESSION_QUESTION_CHOSEN_ANSWER" IS NULL
                        AND "QUIZ_SESSION_QUESTION_IS_CORRECT" IS NULL
                        AND "QUIZ_SESSION_QUESTION_POINTS_EARNED" IS NULL
                    )
                    OR (
                        "QUIZ_SESSION_QUESTION_CHOSEN_ANSWER" IS NOT NULL
                        AND "QUIZ_SESSION_QUESTION_IS_CORRECT" IS NOT NULL
                        AND "QUIZ_SESSION_QUESTION_POINTS_EARNED" IS NOT NULL
                    )
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "QUIZ_SESSION_QUESTIONS" DROP CONSTRAINT "CK_QUIZ_SESSION_QUESTIONS_ANSWER_STATE";
                ALTER TABLE "QUIZ_SESSION_QUESTIONS" DROP CONSTRAINT "CK_QUIZ_SESSION_QUESTIONS_POINTS_EARNED";
                ALTER TABLE "QUIZ_SESSION_QUESTIONS" DROP CONSTRAINT "CK_QUIZ_SESSION_QUESTIONS_CHOSEN_ANSWER";
                ALTER TABLE "QUIZ_SESSION_QUESTIONS" DROP CONSTRAINT "CK_QUIZ_SESSION_QUESTIONS_POSITION";
                ALTER TABLE "QUIZ_SESSIONS" DROP CONSTRAINT "CK_QUIZ_SESSIONS_COMPLETION";
                ALTER TABLE "QUIZ_SESSIONS" DROP CONSTRAINT "CK_QUIZ_SESSIONS_QUESTION_COUNT";
                ALTER TABLE "QUIZ_SESSIONS" DROP CONSTRAINT "CK_QUIZ_SESSIONS_OWNER";
                ALTER TABLE "QUIZ_SESSIONS" DROP CONSTRAINT "CK_QUIZ_SESSIONS_STATUS";
                ALTER TABLE "QUIZ_QUESTIONS" DROP CONSTRAINT "CK_QUIZ_QUESTIONS_POINTS";
                ALTER TABLE "QUIZ_QUESTIONS" DROP CONSTRAINT "CK_QUIZ_QUESTIONS_CORRECT_ANSWER";
                """);

            migrationBuilder.DropTable(
                name: "QUIZ_DEVICE_CLAIMS");

            migrationBuilder.DropTable(
                name: "QUIZ_SESSION_QUESTIONS");

            migrationBuilder.DropTable(
                name: "QUIZ_QUESTIONS");

            migrationBuilder.DropTable(
                name: "QUIZ_SESSIONS");
        }
    }
}
