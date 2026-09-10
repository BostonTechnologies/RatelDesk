using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_SlaBackgroundProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "TicketSlaEscalationEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                table: "TicketSlaEscalationEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "TicketSlaEscalationEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SendStatus",
                table: "TicketSlaEscalationEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE \"TicketSlaEscalationEvents\" SET \"SendStatus\" = 1;");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaStates_ResumeAt",
                table: "TicketSlaStates",
                column: "ResumeAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_State",
                table: "Tickets",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketSlaStates_ResumeAt",
                table: "TicketSlaStates");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_State",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "TicketSlaEscalationEvents");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "TicketSlaEscalationEvents");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "TicketSlaEscalationEvents");

            migrationBuilder.DropColumn(
                name: "SendStatus",
                table: "TicketSlaEscalationEvents");
        }
    }
}
