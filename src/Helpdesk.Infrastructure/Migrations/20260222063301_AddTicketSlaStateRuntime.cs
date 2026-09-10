using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaStateRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketSlaStates",
                columns: table => new
                {
                    TicketId = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResponseDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolutionDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResponseBreached = table.Column<bool>(type: "boolean", nullable: false),
                    ResolutionBreached = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSlaStates", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_TicketSlaStates_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaStates_ResolutionDueAt",
                table: "TicketSlaStates",
                column: "ResolutionDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaStates_Status",
                table: "TicketSlaStates",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketSlaStates");
        }
    }
}
