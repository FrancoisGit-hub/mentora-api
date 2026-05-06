using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLot2_3_1b_PackDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PRODUCT_PACK_DISCOUNT_PERCENT",
                table: "PRODUCT_PACKS",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ORDER_ITEM_ORIGINAL_UNIT_PRICE_EUROS",
                table: "ORDER_ITEMS",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ORDER_ITEM_PACK_DISCOUNT_PERCENT_APPLIED",
                table: "ORDER_ITEMS",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PRODUCT_PACKS_DISCOUNT_PERCENT_RANGE",
                table: "PRODUCT_PACKS",
                sql: "\"PRODUCT_PACK_DISCOUNT_PERCENT\" >= 0 AND \"PRODUCT_PACK_DISCOUNT_PERCENT\" <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PRODUCT_PACKS_DISCOUNT_PERCENT_RANGE",
                table: "PRODUCT_PACKS");

            migrationBuilder.DropColumn(
                name: "PRODUCT_PACK_DISCOUNT_PERCENT",
                table: "PRODUCT_PACKS");

            migrationBuilder.DropColumn(
                name: "ORDER_ITEM_ORIGINAL_UNIT_PRICE_EUROS",
                table: "ORDER_ITEMS");

            migrationBuilder.DropColumn(
                name: "ORDER_ITEM_PACK_DISCOUNT_PERCENT_APPLIED",
                table: "ORDER_ITEMS");
        }
    }
}
