using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLot2_Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop any legacy CREDIT_* columns that may exist from earlier drafts.
            // ALTER TABLE IF EXISTS is safe when the table does not yet exist.
            migrationBuilder.Sql(
                """
                ALTER TABLE IF EXISTS "COACH_PARAMETERS"
                    DROP COLUMN IF EXISTS "COACH_CREDIT_VALUE_EUROS",
                    DROP COLUMN IF EXISTS "COACH_CREDIT_BALANCE",
                    DROP COLUMN IF EXISTS "COACH_CREDIT_RATE";
                """);

            migrationBuilder.CreateTable(
                name: "COACH_PARAMETERS",
                columns: table => new
                {
                    COACH_PARAMETER_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    COACH_PARAMETER_HOURLY_RATE_EUROS = table.Column<decimal>(type: "numeric(8,2)", nullable: false, defaultValue: 50.00m),
                    COACH_PARAMETER_CANCELLATION_DELAY_HOURS = table.Column<int>(type: "integer", nullable: false, defaultValue: 24),
                    COACH_PARAMETER_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    COACH_PARAMETER_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COACH_PARAMETERS", x => x.COACH_PARAMETER_ID);
                    table.ForeignKey(
                        name: "FK_COACH_PARAMETERS_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OFFER_PROGRAMS",
                columns: table => new
                {
                    OFFER_PROGRAM_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OFFER_PROGRAM_NAME = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OFFER_PROGRAM_IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    OFFER_PROGRAM_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OFFER_PROGRAMS", x => x.OFFER_PROGRAM_ID);
                    table.ForeignKey(
                        name: "FK_OFFER_PROGRAMS_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SESSION_SLOTS",
                columns: table => new
                {
                    SESSION_SLOT_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SESSION_SLOT_START_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SESSION_SLOT_END_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SESSION_SLOT_OFFER_TYPE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SESSION_SLOT_DURATION_MINUTES = table.Column<int>(type: "integer", nullable: false),
                    SESSION_SLOT_IS_AVAILABLE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SESSION_SLOT_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SESSION_SLOTS", x => x.SESSION_SLOT_ID);
                    table.CheckConstraint("CK_SESSION_SLOTS_END_AFTER_START", "\"SESSION_SLOT_END_DATE\" > \"SESSION_SLOT_START_DATE\"");
                    table.ForeignKey(
                        name: "FK_SESSION_SLOTS_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COACH_PARAMETERS_COACH_ID",
                table: "COACH_PARAMETERS",
                column: "COACH_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OFFER_PROGRAMS_ACTIVE_NAME",
                table: "OFFER_PROGRAMS",
                columns: new[] { "COACH_ID", "OFFER_PROGRAM_NAME" },
                unique: true,
                filter: "\"OFFER_PROGRAM_IS_ACTIVE\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_SESSION_SLOTS_COACH_ID",
                table: "SESSION_SLOTS",
                column: "COACH_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COACH_PARAMETERS");

            migrationBuilder.DropTable(
                name: "OFFER_PROGRAMS");

            migrationBuilder.DropTable(
                name: "SESSION_SLOTS");
        }
    }
}
