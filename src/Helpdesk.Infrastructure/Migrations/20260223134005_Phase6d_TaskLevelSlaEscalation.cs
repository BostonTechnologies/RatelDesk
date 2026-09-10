using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6d_TaskLevelSlaEscalation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EscalateAfterMinutes",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Escalated",
                table: "Tickets",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalatedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationRole",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationUserId",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlaBreached",
                table: "Tickets",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlaStartedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaskSlaMinutes",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Escalated_Status",
                table: "Tickets",
                columns: new[] { "Escalated", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_DueAt",
                table: "Tickets",
                columns: new[] { "Status", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_Escalated_Status",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_Status_DueAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalateAfterMinutes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Escalated",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalationRole",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalationUserId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaBreached",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaStartedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TaskSlaMinutes",
                table: "Tickets");
        }
    }
}
