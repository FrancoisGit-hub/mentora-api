using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLot2_2_Catalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PRODUCT_PACKS",
                columns: table => new
                {
                    PRODUCT_PACK_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PRODUCT_PACK_NAME = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PRODUCT_PACK_DESCRIPTION = table.Column<string>(type: "text", nullable: true),
                    PRODUCT_PACK_PRICE_EUROS = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    PRODUCT_PACK_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'DRAFT'"),
                    PRODUCT_PACK_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PRODUCT_PACK_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCT_PACKS", x => x.PRODUCT_PACK_ID);
                    table.CheckConstraint("CK_PRODUCT_PACKS_PRICE_NON_NEGATIVE", "\"PRODUCT_PACK_PRICE_EUROS\" >= 0");
                    table.ForeignKey(
                        name: "FK_PRODUCT_PACKS_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRODUCTS",
                columns: table => new
                {
                    PRODUCT_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PRODUCT_NAME = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PRODUCT_DESCRIPTION = table.Column<string>(type: "text", nullable: true),
                    PRODUCT_OFFER_TYPE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PRODUCT_OFFER_NATURE = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, defaultValue: "CLASSIQUE"),
                    PRODUCT_DURATION_MINUTES = table.Column<int>(type: "integer", nullable: false),
                    PRODUCT_PRICE_EUROS = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    PRODUCT_SPORT = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValueSql: "'TRAINING'"),
                    PRODUCT_LOCATION = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PRODUCT_TAGS = table.Column<List<string>>(type: "jsonb", nullable: true),
                    PRODUCT_STATUS = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'DRAFT'"),
                    PRODUCT_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PRODUCT_UPDATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OFFER_PROGRAM_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCTS", x => x.PRODUCT_ID);
                    table.CheckConstraint("CK_PRODUCTS_DURATION_POSITIVE", "\"PRODUCT_DURATION_MINUTES\" > 0");
                    table.CheckConstraint("CK_PRODUCTS_PRICE_NON_NEGATIVE", "\"PRODUCT_PRICE_EUROS\" >= 0");
                    table.ForeignKey(
                        name: "FK_PRODUCTS_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PRODUCTS_OFFER_PROGRAMS_OFFER_PROGRAM_ID",
                        column: x => x.OFFER_PROGRAM_ID,
                        principalTable: "OFFER_PROGRAMS",
                        principalColumn: "OFFER_PROGRAM_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRODUCT_PACK_ITEMS",
                columns: table => new
                {
                    PRODUCT_PACK_ITEM_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PRODUCT_PACK_ITEM_QUANTITY = table.Column<int>(type: "integer", nullable: false),
                    PRODUCT_PACK_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PRODUCT_ID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCT_PACK_ITEMS", x => x.PRODUCT_PACK_ITEM_ID);
                    table.CheckConstraint("CK_PRODUCT_PACK_ITEMS_QUANTITY_MIN_1", "\"PRODUCT_PACK_ITEM_QUANTITY\" >= 1");
                    table.ForeignKey(
                        name: "FK_PRODUCT_PACK_ITEMS_PRODUCTS_PRODUCT_ID",
                        column: x => x.PRODUCT_ID,
                        principalTable: "PRODUCTS",
                        principalColumn: "PRODUCT_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PRODUCT_PACK_ITEMS_PRODUCT_PACKS_PRODUCT_PACK_ID",
                        column: x => x.PRODUCT_PACK_ID,
                        principalTable: "PRODUCT_PACKS",
                        principalColumn: "PRODUCT_PACK_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_PACK_ITEMS_PACK_PRODUCT_UNIQUE",
                table: "PRODUCT_PACK_ITEMS",
                columns: new[] { "PRODUCT_PACK_ID", "PRODUCT_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_PACK_ITEMS_PRODUCT_ID",
                table: "PRODUCT_PACK_ITEMS",
                column: "PRODUCT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_PACKS_COACH_STATUS",
                table: "PRODUCT_PACKS",
                columns: new[] { "COACH_ID", "PRODUCT_PACK_STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_COACH_STATUS",
                table: "PRODUCTS",
                columns: new[] { "COACH_ID", "PRODUCT_STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_OFFER_PROGRAM_ID",
                table: "PRODUCTS",
                column: "OFFER_PROGRAM_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PRODUCT_PACK_ITEMS");

            migrationBuilder.DropTable(
                name: "PRODUCTS");

            migrationBuilder.DropTable(
                name: "PRODUCT_PACKS");
        }
    }
}
