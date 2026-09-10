using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestAutomationSourceLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceAutomationBindingId",
                table: "Tickets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceKnowledgeArticleId",
                table: "Tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceTicketId",
                table: "Tickets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrganizationId_SourceKnowledgeArticleId",
                table: "Tickets",
                columns: new[] { "OrganizationId", "SourceKnowledgeArticleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrganizationId_SourceTicketId",
                table: "Tickets",
                columns: new[] { "OrganizationId", "SourceTicketId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_OrganizationId_SourceKnowledgeArticleId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_OrganizationId_SourceTicketId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SourceAutomationBindingId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SourceKnowledgeArticleId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SourceTicketId",
                table: "Tickets");
        }
    }
}
