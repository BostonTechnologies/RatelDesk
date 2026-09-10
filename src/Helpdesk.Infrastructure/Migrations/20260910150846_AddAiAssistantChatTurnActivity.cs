using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAssistantChatTurnActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastTransportActivityAtUtc",
                table: "AiAssistantChatConversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TurnStartedAtUtc",
                table: "AiAssistantChatConversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantChatConversations_State_LastTransportActivityAtU~",
                table: "AiAssistantChatConversations",
                columns: new[] { "State", "LastTransportActivityAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiAssistantChatConversations_State_LastTransportActivityAtU~",
                table: "AiAssistantChatConversations");

            migrationBuilder.DropColumn(
                name: "LastTransportActivityAtUtc",
                table: "AiAssistantChatConversations");

            migrationBuilder.DropColumn(
                name: "TurnStartedAtUtc",
                table: "AiAssistantChatConversations");
        }
    }
}
