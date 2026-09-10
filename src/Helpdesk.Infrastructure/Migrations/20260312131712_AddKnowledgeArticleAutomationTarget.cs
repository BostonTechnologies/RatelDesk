using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeArticleAutomationTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutomationBindingId",
                table: "KnowledgeBaseArticles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutomationRequestFormId",
                table: "KnowledgeBaseArticles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutomationOrchestrationJobDefinitionId",
                table: "KnowledgeBaseArticles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutomationOrchestrationRequestDefinitionId",
                table: "KnowledgeBaseArticles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AutomationTaskTemplateId",
                table: "KnowledgeBaseArticles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutomationTaskTemplateName",
                table: "KnowledgeBaseArticles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticles_OrganizationId_AutomationBindingId",
                table: "KnowledgeBaseArticles",
                columns: new[] { "OrganizationId", "AutomationBindingId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBaseArticles_OrganizationId_AutomationBindingId",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "AutomationBindingId",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "AutomationRequestFormId",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "AutomationOrchestrationJobDefinitionId",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "AutomationOrchestrationRequestDefinitionId",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "AutomationTaskTemplateId",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "AutomationTaskTemplateName",
                table: "KnowledgeBaseArticles");
        }
    }
}
