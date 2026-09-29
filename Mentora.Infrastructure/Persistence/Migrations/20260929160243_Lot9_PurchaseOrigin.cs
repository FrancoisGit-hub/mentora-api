using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot9_PurchaseOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ORDER_GRANT_NOTE",
                table: "ORDERS",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ORDER_ORIGIN",
                table: "ORDERS",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'MEMBER_PURCHASE'");

            migrationBuilder.Sql(
                """
                ALTER TABLE "ORDERS"
                ADD CONSTRAINT "CK_ORDERS_ORIGIN" CHECK (
                    "ORDER_ORIGIN" IN ('MEMBER_PURCHASE','COACH_GRANT')
                    AND ("ORDER_ORIGIN" <> 'COACH_GRANT' OR "ORDER_TOTAL_EUROS" = 0)
                    AND ("ORDER_ORIGIN" = 'COACH_GRANT' OR "ORDER_GRANT_NOTE" IS NULL)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ORDERS" DROP CONSTRAINT "CK_ORDERS_ORIGIN";
                """);

            migrationBuilder.DropColumn(
                name: "ORDER_GRANT_NOTE",
                table: "ORDERS");

            migrationBuilder.DropColumn(
                name: "ORDER_ORIGIN",
                table: "ORDERS");
        }
    }
}
