using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6a_TaskDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "Tickets",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UnblockedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RequestId_IsBlocked",
                table: "Tickets",
                columns: new[] { "RequestId", "IsBlocked" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_RequestId_IsBlocked",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "UnblockedAt",
                table: "Tickets");
        }
    }
}
