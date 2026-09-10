using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationAiRuntimeHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableProviderFallback",
                table: "OrganizationAiKbSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxProviderAttempts",
                table: "OrganizationAiKbSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumAutomationFeedbackCount",
                table: "OrganizationAiKbSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "MinimumAutomationResolvedRate",
                table: "OrganizationAiKbSettings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumSuggestionFeedbackCount",
                table: "OrganizationAiKbSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "MinimumSuggestionHelpfulRate",
                table: "OrganizationAiKbSettings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableProviderFallback",
                table: "OrganizationAiKbSettings");

            migrationBuilder.DropColumn(
                name: "MaxProviderAttempts",
                table: "OrganizationAiKbSettings");

            migrationBuilder.DropColumn(
                name: "MinimumAutomationFeedbackCount",
                table: "OrganizationAiKbSettings");

            migrationBuilder.DropColumn(
                name: "MinimumAutomationResolvedRate",
                table: "OrganizationAiKbSettings");

            migrationBuilder.DropColumn(
                name: "MinimumSuggestionFeedbackCount",
                table: "OrganizationAiKbSettings");

            migrationBuilder.DropColumn(
                name: "MinimumSuggestionHelpfulRate",
                table: "OrganizationAiKbSettings");
        }
    }
}
