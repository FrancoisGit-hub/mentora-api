using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLot2_3_Purchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CARTS",
                columns: table => new
                {
                    CART_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CART_MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    CART_COACH_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    CART_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CART_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CARTS", x => x.CART_ID);
                    table.ForeignKey(
                        name: "FK_CARTS_COACHES_CART_COACH_ID",
                        column: x => x.CART_COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CARTS_MEMBERS_CART_MEMBER_ID",
                        column: x => x.CART_MEMBER_ID,
                        principalTable: "MEMBERS",
                        principalColumn: "MEMBER_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ORDERS",
                columns: table => new
                {
                    ORDER_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ORDER_MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ORDER_COACH_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ORDER_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'PENDING'"),
                    ORDER_TOTAL_EUROS = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ORDER_STRIPE_SESSION_ID = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ORDER_STRIPE_CHECKOUT_URL = table.Column<string>(type: "text", nullable: true),
                    ORDER_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ORDER_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ORDER_PAID_AT = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORDERS", x => x.ORDER_ID);
                    table.ForeignKey(
                        name: "FK_ORDERS_COACHES_ORDER_COACH_ID",
                        column: x => x.ORDER_COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORDERS_MEMBERS_ORDER_MEMBER_ID",
                        column: x => x.ORDER_MEMBER_ID,
                        principalTable: "MEMBERS",
                        principalColumn: "MEMBER_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "STRIPE_WEBHOOK_EVENTS",
                columns: table => new
                {
                    EVENT_ID = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EVENT_TYPE = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EVENT_PAYLOAD = table.Column<string>(type: "jsonb", nullable: false),
                    EVENT_RECEIVED_AT = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    EVENT_PROCESSED_AT = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EVENT_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'RECEIVED'"),
                    EVENT_PROCESSING_ERROR = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STRIPE_WEBHOOK_EVENTS", x => x.EVENT_ID);
                });

            migrationBuilder.CreateTable(
                name: "CART_ITEMS",
                columns: table => new
                {
                    CART_ITEM_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CART_ITEM_CART_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    CART_ITEM_PRODUCT_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    CART_ITEM_PACK_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    CART_ITEM_QUANTITY = table.Column<int>(type: "integer", nullable: false),
                    CART_ITEM_ADDED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CART_ITEMS", x => x.CART_ITEM_ID);
                    table.CheckConstraint("CK_CART_ITEMS_PRODUCT_OR_PACK_XOR", "((\"CART_ITEM_PRODUCT_ID\" IS NOT NULL)::int + (\"CART_ITEM_PACK_ID\" IS NOT NULL)::int) = 1");
                    table.ForeignKey(
                        name: "FK_CART_ITEMS_CARTS_CART_ITEM_CART_ID",
                        column: x => x.CART_ITEM_CART_ID,
                        principalTable: "CARTS",
                        principalColumn: "CART_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CART_ITEMS_PRODUCTS_CART_ITEM_PRODUCT_ID",
                        column: x => x.CART_ITEM_PRODUCT_ID,
                        principalTable: "PRODUCTS",
                        principalColumn: "PRODUCT_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CART_ITEMS_PRODUCT_PACKS_CART_ITEM_PACK_ID",
                        column: x => x.CART_ITEM_PACK_ID,
                        principalTable: "PRODUCT_PACKS",
                        principalColumn: "PRODUCT_PACK_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ORDER_ITEMS",
                columns: table => new
                {
                    ORDER_ITEM_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ORDER_ITEM_ORDER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ORDER_ITEM_PRODUCT_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ORDER_ITEM_PACK_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    ORDER_ITEM_PRODUCT_NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ORDER_ITEM_UNIT_PRICE_EUROS = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ORDER_ITEM_QUANTITY = table.Column<int>(type: "integer", nullable: false),
                    ORDER_ITEM_LINE_TOTAL_EUROS = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ORDER_ITEM_OFFER_TYPE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ORDER_ITEM_DURATION_MINUTES = table.Column<int>(type: "integer", nullable: false),
                    ORDER_ITEM_SPORT = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValueSql: "'TRAINING'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORDER_ITEMS", x => x.ORDER_ITEM_ID);
                    table.ForeignKey(
                        name: "FK_ORDER_ITEMS_ORDERS_ORDER_ITEM_ORDER_ID",
                        column: x => x.ORDER_ITEM_ORDER_ID,
                        principalTable: "ORDERS",
                        principalColumn: "ORDER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SESSION_VOUCHERS",
                columns: table => new
                {
                    VOUCHER_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    VOUCHER_ORDER_ITEM_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    VOUCHER_MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    VOUCHER_COACH_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    VOUCHER_PRODUCT_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    VOUCHER_OFFER_TYPE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VOUCHER_DURATION_MINUTES = table.Column<int>(type: "integer", nullable: false),
                    VOUCHER_SPORT = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValueSql: "'TRAINING'"),
                    VOUCHER_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'AVAILABLE'"),
                    VOUCHER_RESERVED_SESSION_ID = table.Column<Guid>(type: "uuid", nullable: true),
                    VOUCHER_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VOUCHER_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SESSION_VOUCHERS", x => x.VOUCHER_ID);
                    table.ForeignKey(
                        name: "FK_SESSION_VOUCHERS_COACHES_VOUCHER_COACH_ID",
                        column: x => x.VOUCHER_COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SESSION_VOUCHERS_MEMBERS_VOUCHER_MEMBER_ID",
                        column: x => x.VOUCHER_MEMBER_ID,
                        principalTable: "MEMBERS",
                        principalColumn: "MEMBER_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SESSION_VOUCHERS_ORDER_ITEMS_VOUCHER_ORDER_ITEM_ID",
                        column: x => x.VOUCHER_ORDER_ITEM_ID,
                        principalTable: "ORDER_ITEMS",
                        principalColumn: "ORDER_ITEM_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CART_ITEMS_CART_ITEM_CART_ID",
                table: "CART_ITEMS",
                column: "CART_ITEM_CART_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CART_ITEMS_CART_ITEM_PACK_ID",
                table: "CART_ITEMS",
                column: "CART_ITEM_PACK_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CART_ITEMS_CART_ITEM_PRODUCT_ID",
                table: "CART_ITEMS",
                column: "CART_ITEM_PRODUCT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CARTS_CART_COACH_ID",
                table: "CARTS",
                column: "CART_COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CARTS_MEMBER_COACH_UNIQUE",
                table: "CARTS",
                columns: new[] { "CART_MEMBER_ID", "CART_COACH_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ORDER_ITEMS_ORDER_ITEM_ORDER_ID",
                table: "ORDER_ITEMS",
                column: "ORDER_ITEM_ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORDERS_ORDER_COACH_ID",
                table: "ORDERS",
                column: "ORDER_COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORDERS_ORDER_MEMBER_ID",
                table: "ORDERS",
                column: "ORDER_MEMBER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORDERS_STRIPE_SESSION_ID_UNIQUE",
                table: "ORDERS",
                column: "ORDER_STRIPE_SESSION_ID",
                unique: true,
                filter: "\"ORDER_STRIPE_SESSION_ID\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SESSION_VOUCHERS_MEMBER_STATUS",
                table: "SESSION_VOUCHERS",
                columns: new[] { "VOUCHER_MEMBER_ID", "VOUCHER_STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_SESSION_VOUCHERS_VOUCHER_COACH_ID",
                table: "SESSION_VOUCHERS",
                column: "VOUCHER_COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESSION_VOUCHERS_VOUCHER_ORDER_ITEM_ID",
                table: "SESSION_VOUCHERS",
                column: "VOUCHER_ORDER_ITEM_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CART_ITEMS");

            migrationBuilder.DropTable(
                name: "SESSION_VOUCHERS");

            migrationBuilder.DropTable(
                name: "STRIPE_WEBHOOK_EVENTS");

            migrationBuilder.DropTable(
                name: "CARTS");

            migrationBuilder.DropTable(
                name: "ORDER_ITEMS");

            migrationBuilder.DropTable(
                name: "ORDERS");
        }
    }
}
