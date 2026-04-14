using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLot2SessionsAndCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COACH_PARAMETERS",
                columns: table => new
                {
                    COACH_PARAMETER_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    COACH_CREDIT_VALUE_EUROS = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 6.00m),
                    COACH_CANCELLATION_DELAY_HOURS = table.Column<int>(type: "integer", nullable: false, defaultValue: 24),
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
                name: "CREDIT_BALANCES",
                columns: table => new
                {
                    CREDIT_BALANCE_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CREDIT_BALANCE_AMOUNT = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CREDIT_BALANCE_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    USER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CREDIT_BALANCES", x => x.CREDIT_BALANCE_ID);
                    table.ForeignKey(
                        name: "FK_CREDIT_BALANCES_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CREDIT_BALANCES_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "USERS",
                        principalColumn: "USER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SESSION_SLOTS",
                columns: table => new
                {
                    SESSION_SLOT_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SESSION_SLOT_START_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SESSION_SLOT_END_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SESSION_SLOT_PRICE_EUROS = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    SESSION_SLOT_CREDITS_REQUIRED = table.Column<int>(type: "integer", nullable: false),
                    SESSION_SLOT_TYPE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SESSION_SLOT_IS_AVAILABLE = table.Column<bool>(type: "boolean", nullable: false),
                    SESSION_SLOT_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SESSION_SLOTS", x => x.SESSION_SLOT_ID);
                    table.ForeignKey(
                        name: "FK_SESSION_SLOTS_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SESSIONS",
                columns: table => new
                {
                    SESSION_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SESSION_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SESSION_CREDITS_CONSUMED = table.Column<int>(type: "integer", nullable: false),
                    SESSION_CANCEL_REASON = table.Column<string>(type: "text", nullable: true),
                    SESSION_CANCELLED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SESSION_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SESSION_SLOT_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    USER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SESSIONS", x => x.SESSION_ID);
                    table.ForeignKey(
                        name: "FK_SESSIONS_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SESSIONS_SESSION_SLOTS_SESSION_SLOT_ID",
                        column: x => x.SESSION_SLOT_ID,
                        principalTable: "SESSION_SLOTS",
                        principalColumn: "SESSION_SLOT_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SESSIONS_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "USERS",
                        principalColumn: "USER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CREDIT_TRANSACTIONS",
                columns: table => new
                {
                    CREDIT_TRANSACTION_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CREDIT_TRANSACTION_TYPE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CREDIT_TRANSACTION_AMOUNT = table.Column<int>(type: "integer", nullable: false),
                    CREDIT_TRANSACTION_EUROS = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    CREDIT_TRANSACTION_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SESSION_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    USER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CREDIT_TRANSACTIONS", x => x.CREDIT_TRANSACTION_ID);
                    table.ForeignKey(
                        name: "FK_CREDIT_TRANSACTIONS_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CREDIT_TRANSACTIONS_SESSIONS_SESSION_ID",
                        column: x => x.SESSION_ID,
                        principalTable: "SESSIONS",
                        principalColumn: "SESSION_ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CREDIT_TRANSACTIONS_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "USERS",
                        principalColumn: "USER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COACH_PARAMETERS_COACH_ID",
                table: "COACH_PARAMETERS",
                column: "COACH_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CREDIT_BALANCES_COACH_ID",
                table: "CREDIT_BALANCES",
                column: "COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CREDIT_BALANCES_USER_ID_COACH_ID",
                table: "CREDIT_BALANCES",
                columns: new[] { "USER_ID", "COACH_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CREDIT_TRANSACTIONS_COACH_ID",
                table: "CREDIT_TRANSACTIONS",
                column: "COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CREDIT_TRANSACTIONS_SESSION_ID",
                table: "CREDIT_TRANSACTIONS",
                column: "SESSION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CREDIT_TRANSACTIONS_USER_ID",
                table: "CREDIT_TRANSACTIONS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESSION_SLOTS_COACH_ID",
                table: "SESSION_SLOTS",
                column: "COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESSIONS_COACH_ID",
                table: "SESSIONS",
                column: "COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESSIONS_SESSION_SLOT_ID",
                table: "SESSIONS",
                column: "SESSION_SLOT_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SESSIONS_USER_ID",
                table: "SESSIONS",
                column: "USER_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COACH_PARAMETERS");

            migrationBuilder.DropTable(
                name: "CREDIT_BALANCES");

            migrationBuilder.DropTable(
                name: "CREDIT_TRANSACTIONS");

            migrationBuilder.DropTable(
                name: "SESSIONS");

            migrationBuilder.DropTable(
                name: "SESSION_SLOTS");
        }
    }
}
