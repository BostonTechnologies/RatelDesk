using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestTaskOrchestrationRuntimeBridge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutomationBindingId",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAutomationStatus",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAutomationUpdatedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrchestrationExternalRequestId",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrchestrationExternalRunId",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrchestrationJobDefinitionId",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrchestrationRequestDefinitionId",
                table: "Tickets",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutomationBindingId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "LastAutomationStatus",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "LastAutomationUpdatedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OrchestrationExternalRequestId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OrchestrationExternalRunId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OrchestrationJobDefinitionId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OrchestrationRequestDefinitionId",
                table: "Tickets");
        }
    }
}
