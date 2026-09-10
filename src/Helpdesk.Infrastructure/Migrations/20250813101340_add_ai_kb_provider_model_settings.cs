using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class add_ai_kb_provider_model_settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AllowListCsv",
                table: "OrganizationAiKbSettings",
                newName: "ProviderId");

            migrationBuilder.AddColumn<string>(
                name: "AllowedServicesCsv",
                table: "OrganizationAiKbSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "OrganizationAiKbSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuggestionLimit",
                table: "OrganizationAiKbSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedServicesCsv",
                table: "OrganizationAiKbSettings");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "OrganizationAiKbSettings");

            migrationBuilder.DropColumn(
                name: "SuggestionLimit",
                table: "OrganizationAiKbSettings");

            migrationBuilder.RenameColumn(
                name: "ProviderId",
                table: "OrganizationAiKbSettings",
                newName: "AllowListCsv");
        }
    }
}
