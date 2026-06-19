using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationsAndMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CONVERSATIONS",
                columns: table => new
                {
                    CONVERSATION_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MEMBER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    COACH_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    CONVERSATION_VISIO_URL = table.Column<string>(type: "text", nullable: true),
                    CONVERSATION_CREATED_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    CONVERSATION_LAST_MESSAGE_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONVERSATIONS", x => x.CONVERSATION_ID);
                    table.ForeignKey(
                        name: "FK_CONVERSATIONS_COACHES_COACH_ID",
                        column: x => x.COACH_ID,
                        principalTable: "COACHES",
                        principalColumn: "COACH_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CONVERSATIONS_MEMBERS_MEMBER_ID",
                        column: x => x.MEMBER_ID,
                        principalTable: "MEMBERS",
                        principalColumn: "MEMBER_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MESSAGES",
                columns: table => new
                {
                    MESSAGE_ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CONVERSATION_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    MESSAGE_CONTENT = table.Column<string>(type: "text", nullable: false),
                    MESSAGE_SENDER_TYPE = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MESSAGE_SENDER_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    MESSAGE_IS_READ = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MESSAGE_SENT_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    MESSAGE_READ_DATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MESSAGES", x => x.MESSAGE_ID);
                    table.ForeignKey(
                        name: "FK_MESSAGES_CONVERSATIONS_CONVERSATION_ID",
                        column: x => x.CONVERSATION_ID,
                        principalTable: "CONVERSATIONS",
                        principalColumn: "CONVERSATION_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CONVERSATIONS_COACH_ID",
                table: "CONVERSATIONS",
                column: "COACH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CONVERSATIONS_MEMBER_COACH",
                table: "CONVERSATIONS",
                columns: new[] { "MEMBER_ID", "COACH_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MESSAGES_CONVERSATION_SENT_DESC",
                table: "MESSAGES",
                columns: new[] { "CONVERSATION_ID", "MESSAGE_SENT_DATE" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MESSAGES");

            migrationBuilder.DropTable(
                name: "CONVERSATIONS");
        }
    }
}
