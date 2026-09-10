using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6g_CriticalPathFailurePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailurePolicy",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCritical",
                table: "Tickets",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Tickets",
                type: "integer",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RetryDelayMinutes",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowBlockReason",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowStatus",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WorkflowUpdatedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_NextRetryAt",
                table: "Tickets",
                columns: new[] { "Status", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_Status_NextRetryAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FailurePolicy",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsCritical",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RetryDelayMinutes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "WorkflowBlockReason",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "WorkflowStatus",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "WorkflowUpdatedAt",
                table: "Tickets");
        }
    }
}
