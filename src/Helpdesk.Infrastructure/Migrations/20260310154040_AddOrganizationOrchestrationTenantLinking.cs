using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationOrchestrationTenantLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrchestrationTenantId",
                table: "Organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OrchestrationTenantLinkedAtUtc",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrchestrationTenantName",
                table: "Organizations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrchestrationTenantId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "OrchestrationTenantLinkedAtUtc",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "OrchestrationTenantName",
                table: "Organizations");
        }
    }
}
