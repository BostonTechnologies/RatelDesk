using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_SlaReportingAndCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "TicketSlaStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CompletedWithinResolutionSla",
                table: "TicketSlaStates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CompletedWithinResponseSla",
                table: "TicketSlaStates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaStates_Status_CompletedAt",
                table: "TicketSlaStates",
                columns: new[] { "Status", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrganizationId_ClosedAt",
                table: "Tickets",
                columns: new[] { "OrganizationId", "ClosedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketSlaStates_Status_CompletedAt",
                table: "TicketSlaStates");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_OrganizationId_ClosedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "CompletedWithinResolutionSla",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "CompletedWithinResponseSla",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Tickets");
        }
    }
}
