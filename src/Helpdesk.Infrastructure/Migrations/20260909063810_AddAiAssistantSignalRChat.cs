using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAssistantSignalRChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiAssistantChatConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<string>(type: "text", nullable: false),
                    TicketId = table.Column<string>(type: "text", nullable: false),
                    TicketType = table.Column<string>(type: "text", nullable: false),
                    AiAssistantSessionId = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false),
                    ActiveMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastActivityUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssistantChatConversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAssistantChatEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    CallId = table.Column<string>(type: "text", nullable: true),
                    OptionsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssistantChatEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAssistantChatEvents_AiAssistantChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AiAssistantChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiAssistantChatInteractions",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallId = table.Column<string>(type: "text", nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: false),
                    SelectedKey = table.Column<string>(type: "text", nullable: true),
                    AnsweredByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssistantChatInteractions", x => new { x.ConversationId, x.CallId });
                    table.ForeignKey(
                        name: "FK_AiAssistantChatInteractions_AiAssistantChatConversations_Conversati~",
                        column: x => x.ConversationId,
                        principalTable: "AiAssistantChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantChatConversations_OrganizationId_TicketType_TicketId",
                table: "AiAssistantChatConversations",
                columns: new[] { "OrganizationId", "TicketType", "TicketId" },
                unique: true,
                filter: "\"State\" <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantChatEvents_ConversationId_ClientMessageId",
                table: "AiAssistantChatEvents",
                columns: new[] { "ConversationId", "ClientMessageId" },
                unique: true,
                filter: "\"ClientMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantChatEvents_ConversationId_Sequence",
                table: "AiAssistantChatEvents",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAssistantChatEvents");

            migrationBuilder.DropTable(
                name: "AiAssistantChatInteractions");

            migrationBuilder.DropTable(
                name: "AiAssistantChatConversations");
        }
    }
}
