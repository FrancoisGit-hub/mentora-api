using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLot2_4_Sessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SESSIONS",
                columns: table => new
                {
                    SESSION_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SESSION_VOUCHER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    SESSION_SLOT_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    SESSION_MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    SESSION_COACH_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    SESSION_PRODUCT_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    SESSION_OFFER_TYPE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SESSION_DURATION_MINUTES = table.Column<int>(type: "integer", nullable: false),
                    SESSION_SPORT = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SESSION_SCHEDULED_AT = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SESSION_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'SCHEDULED'"),
                    SESSION_VISIO_URL = table.Column<string>(type: "text", nullable: true),
                    SESSION_CANCELLATION_REASON = table.Column<string>(type: "text", nullable: true),
                    SESSION_CANCELLED_BY = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SESSION_CANCELLED_AT = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SESSION_COMPLETED_AT = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SESSION_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SESSION_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SESSIONS", x => x.SESSION_ID);
                    table.ForeignKey(
                        name: "FK_SESSIONS_COACHES_SESSION_COACH_ID",
                        column: x => x.SESSION_COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SESSIONS_MEMBERS_SESSION_MEMBER_ID",
                        column: x => x.SESSION_MEMBER_ID,
                        principalTable: "MEMBERS",
                        principalColumn: "MEMBER_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SESSIONS_SESSION_SLOTS_SESSION_SLOT_ID",
                        column: x => x.SESSION_SLOT_ID,
                        principalTable: "SESSION_SLOTS",
                        principalColumn: "SESSION_SLOT_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SESSIONS_SESSION_VOUCHERS_SESSION_VOUCHER_ID",
                        column: x => x.SESSION_VOUCHER_ID,
                        principalTable: "SESSION_VOUCHERS",
                        principalColumn: "VOUCHER_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SESSION_VOUCHERS_VOUCHER_RESERVED_SESSION_ID",
                table: "SESSION_VOUCHERS",
                column: "VOUCHER_RESERVED_SESSION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESSIONS_COACH_SCHEDULED_AT",
                table: "SESSIONS",
                columns: new[] { "SESSION_COACH_ID", "SESSION_SCHEDULED_AT" });

            migrationBuilder.CreateIndex(
                name: "IX_SESSIONS_MEMBER_STATUS",
                table: "SESSIONS",
                columns: new[] { "SESSION_MEMBER_ID", "SESSION_STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_SESSIONS_SESSION_SLOT_ID",
                table: "SESSIONS",
                column: "SESSION_SLOT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESSIONS_SESSION_VOUCHER_ID",
                table: "SESSIONS",
                column: "SESSION_VOUCHER_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_SESSION_VOUCHERS_RESERVED_SESSION",
                table: "SESSION_VOUCHERS",
                column: "VOUCHER_RESERVED_SESSION_ID",
                principalTable: "SESSIONS",
                principalColumn: "SESSION_ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SESSION_VOUCHERS_RESERVED_SESSION",
                table: "SESSION_VOUCHERS");

            migrationBuilder.DropTable(
                name: "SESSIONS");

            migrationBuilder.DropIndex(
                name: "IX_SESSION_VOUCHERS_VOUCHER_RESERVED_SESSION_ID",
                table: "SESSION_VOUCHERS");
        }
    }
}
