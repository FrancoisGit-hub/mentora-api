using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot11_3_CoachSessionPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "COACH_IN_PERSON_LOCATIONS",
                table: "COACHES",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<bool>(
                name: "COACH_OFFERS_IN_PERSON_SOLO",
                table: "COACHES",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "COACH_OFFERS_VISIO",
                table: "COACHES",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                ALTER TABLE "COACHES" ADD CONSTRAINT "CK_COACHES_IN_PERSON_LOCATIONS" CHECK (
                    jsonb_typeof("COACH_IN_PERSON_LOCATIONS") = 'array'
                    AND jsonb_array_length("COACH_IN_PERSON_LOCATIONS") <= 10
                    AND ("COACH_OFFERS_IN_PERSON_SOLO" OR jsonb_array_length("COACH_IN_PERSON_LOCATIONS") = 0));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "COACHES" DROP CONSTRAINT "CK_COACHES_IN_PERSON_LOCATIONS";""");

            migrationBuilder.DropColumn(
                name: "COACH_IN_PERSON_LOCATIONS",
                table: "COACHES");

            migrationBuilder.DropColumn(
                name: "COACH_OFFERS_IN_PERSON_SOLO",
                table: "COACHES");

            migrationBuilder.DropColumn(
                name: "COACH_OFFERS_VISIO",
                table: "COACHES");
        }
    }
}
