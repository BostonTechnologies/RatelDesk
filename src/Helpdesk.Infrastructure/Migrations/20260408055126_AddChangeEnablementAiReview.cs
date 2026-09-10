using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeEnablementAiReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ChangeType",
                table: "Tickets",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AiReviewAcknowledgedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiReviewAcknowledgedByName",
                table: "Tickets",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiReviewAcknowledgedByUserId",
                table: "Tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiReviewAcknowledgementNotes",
                table: "Tickets",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AiReviewCompletedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiReviewCorrelationId",
                table: "Tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiReviewFailureReason",
                table: "Tickets",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiReviewGateState",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiReviewOutputJson",
                table: "Tickets",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AiReviewRequestedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiReviewStatus",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeTemplateJson",
                table: "Tickets",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiReviewAcknowledgedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiReviewAcknowledgedByName",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiReviewAcknowledgedByUserId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiReviewAcknowledgementNotes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiReviewCompletedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiReviewCorrelationId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiReviewFailureReason",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiReviewGateState",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiReviewOutputJson",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiReviewRequestedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AiReviewStatus",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ChangeTemplateJson",
                table: "Tickets");

            migrationBuilder.AlterColumn<string>(
                name: "ChangeType",
                table: "Tickets",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);
        }
    }
}
