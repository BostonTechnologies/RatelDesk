using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundEmailRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboundEmailProcessingLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MailboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    MailboxKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RuleId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ActionKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Matched = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TicketId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEmailProcessingLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboundEmailRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MailboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ConditionsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    ActionsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    StopProcessing = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEmailRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailProcessingLogs_MessageId_MailboxKey_RuleId_Act~1",
                table: "InboundEmailProcessingLogs",
                columns: new[] { "MessageId", "MailboxKey", "RuleId", "ActionKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailProcessingLogs_MessageId_MailboxKey_RuleId_Acti~",
                table: "InboundEmailProcessingLogs",
                columns: new[] { "MessageId", "MailboxKey", "RuleId", "ActionKey" },
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailProcessingLogs_TicketId",
                table: "InboundEmailProcessingLogs",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailRules_Enabled_ScopeType_TenantId_MailboxId_Prio~",
                table: "InboundEmailRules",
                columns: new[] { "Enabled", "ScopeType", "TenantId", "MailboxId", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboundEmailProcessingLogs");

            migrationBuilder.DropTable(
                name: "InboundEmailRules");
        }
    }
}
