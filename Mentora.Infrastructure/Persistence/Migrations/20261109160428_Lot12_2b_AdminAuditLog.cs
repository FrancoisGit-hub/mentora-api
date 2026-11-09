using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot12_2b_AdminAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADMIN_AUDIT_LOG",
                columns: table => new
                {
                    ADMIN_AUDIT_LOG_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ADMIN_AUDIT_LOG_ADMIN_USER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ADMIN_AUDIT_LOG_ACTION = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ADMIN_AUDIT_LOG_ENTITY_TYPE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ADMIN_AUDIT_LOG_ENTITY_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ADMIN_AUDIT_LOG_CHANGES = table.Column<string>(type: "jsonb", nullable: false),
                    ADMIN_AUDIT_LOG_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADMIN_AUDIT_LOG", x => x.ADMIN_AUDIT_LOG_ID);
                    table.ForeignKey(
                        name: "FK_ADMIN_AUDIT_LOG_ADMIN_USER",
                        column: x => x.ADMIN_AUDIT_LOG_ADMIN_USER_ID,
                        principalTable: "ADMIN_USERS",
                        principalColumn: "ADMIN_USER_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ADMIN_AUDIT_LOG_ADMIN_USER",
                table: "ADMIN_AUDIT_LOG",
                column: "ADMIN_AUDIT_LOG_ADMIN_USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ADMIN_AUDIT_LOG_CREATED_DATE",
                table: "ADMIN_AUDIT_LOG",
                column: "ADMIN_AUDIT_LOG_CREATED_DATE");

            migrationBuilder.CreateIndex(
                name: "IX_ADMIN_AUDIT_LOG_ENTITY",
                table: "ADMIN_AUDIT_LOG",
                columns: new[] { "ADMIN_AUDIT_LOG_ENTITY_TYPE", "ADMIN_AUDIT_LOG_ENTITY_ID", "ADMIN_AUDIT_LOG_CREATED_DATE" });

            migrationBuilder.Sql(
                """
                ALTER TABLE "ADMIN_AUDIT_LOG"
                ADD CONSTRAINT "CK_ADMIN_AUDIT_LOG_ACTION"
                CHECK ("ADMIN_AUDIT_LOG_ACTION" IN ('EXERCISE_CREATED', 'EXERCISE_UPDATED', 'EXERCISE_ACTIVATED', 'EXERCISE_DEACTIVATED'));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "ADMIN_AUDIT_LOG"
                ADD CONSTRAINT "CK_ADMIN_AUDIT_LOG_ENTITY_TYPE"
                CHECK ("ADMIN_AUDIT_LOG_ENTITY_TYPE" IN ('EXERCISE'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "ADMIN_AUDIT_LOG" DROP CONSTRAINT "CK_ADMIN_AUDIT_LOG_ENTITY_TYPE";""");
            migrationBuilder.Sql("""ALTER TABLE "ADMIN_AUDIT_LOG" DROP CONSTRAINT "CK_ADMIN_AUDIT_LOG_ACTION";""");

            migrationBuilder.DropTable(
                name: "ADMIN_AUDIT_LOG");
        }
    }
}
