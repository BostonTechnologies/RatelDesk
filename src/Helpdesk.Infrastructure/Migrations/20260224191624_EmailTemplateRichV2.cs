using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EmailTemplateRichV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "EmailTemplates",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "NOW() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "EmailTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedUtc",
                table: "EmailTemplates",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "EmailTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE "EmailTemplates"
                SET "IsSystem" = TRUE
                WHERE "Name" IN ('NewTicketConfirmation', 'TicketUpdated', 'AccessDenied', 'WelcomeNewUser');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "EmailTemplates");
        }
    }
}
