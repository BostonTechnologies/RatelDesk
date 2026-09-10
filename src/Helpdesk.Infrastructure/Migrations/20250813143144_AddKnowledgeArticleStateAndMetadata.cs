using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeArticleStateAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByModel",
                table: "KnowledgeBaseArticles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRegeneratedAt",
                table: "KnowledgeBaseArticles",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedTicketId",
                table: "KnowledgeBaseArticles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "KnowledgeBaseArticles",
                type: "text",
                nullable: false,
                defaultValue: "Draft");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByModel",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "LastRegeneratedAt",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "LinkedTicketId",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "State",
                table: "KnowledgeBaseArticles");
        }
    }
}
