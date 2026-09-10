using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_SlaClockPauseResume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "AccumulatedPauseDuration",
                table: "TicketSlaStates",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastResumedAt",
                table: "TicketSlaStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PauseReason",
                table: "TicketSlaStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedAt",
                table: "TicketSlaStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PausedByUserId",
                table: "TicketSlaStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResumeAt",
                table: "TicketSlaStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoResumeAfterHours",
                table: "SlaPolicies",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccumulatedPauseDuration",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "LastResumedAt",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "PauseReason",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "PausedAt",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "PausedByUserId",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "ResumeAt",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "AutoResumeAfterHours",
                table: "SlaPolicies");
        }
    }
}
