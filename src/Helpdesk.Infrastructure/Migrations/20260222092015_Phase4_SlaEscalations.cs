using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4_SlaEscalations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SlaEscalationRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PolicyId = table.Column<string>(type: "text", nullable: false),
                    Metric = table.Column<int>(type: "integer", nullable: false),
                    TriggerPercent = table.Column<int>(type: "integer", nullable: false),
                    RecipientsJson = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'[]'"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaEscalationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaEscalationRules_SlaPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "SlaPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketSlaEscalationEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TicketId = table.Column<string>(type: "text", nullable: false),
                    Metric = table.Column<int>(type: "integer", nullable: false),
                    TriggerPercent = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PolicyId = table.Column<string>(type: "text", nullable: false),
                    RecipientsCsv = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSlaEscalationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketSlaEscalationEvents_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlaEscalationRules_PolicyId_Metric_TriggerPercent",
                table: "SlaEscalationRules",
                columns: new[] { "PolicyId", "Metric", "TriggerPercent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaEscalationEvents_TicketId_Metric_TriggerPercent",
                table: "TicketSlaEscalationEvents",
                columns: new[] { "TicketId", "Metric", "TriggerPercent" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlaEscalationRules");

            migrationBuilder.DropTable(
                name: "TicketSlaEscalationEvents");
        }
    }
}
