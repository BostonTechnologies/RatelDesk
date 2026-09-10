using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class update_KB_Enhance02 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProviderId",
                table: "OrganizationAiKbSettings",
                newName: "KnowledgeProviderId");

            migrationBuilder.RenameColumn(
                name: "ModelName",
                table: "OrganizationAiKbSettings",
                newName: "KnowledgeModelName");

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingProviderId",
                table: "OrganizationAiKbSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddingProviderId",
                table: "OrganizationAiKbSettings");

            migrationBuilder.RenameColumn(
                name: "KnowledgeProviderId",
                table: "OrganizationAiKbSettings",
                newName: "ProviderId");

            migrationBuilder.RenameColumn(
                name: "KnowledgeModelName",
                table: "OrganizationAiKbSettings",
                newName: "ModelName");
        }
    }
}
