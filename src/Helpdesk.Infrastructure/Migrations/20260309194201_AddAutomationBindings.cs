using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationBindings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestFormId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TaskTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrchestrationRequestDefinitionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OrchestrationRequestDefinitionName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OrchestrationJobDefinitionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OrchestrationJobDefinitionName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SyncState = table.Column<int>(type: "integer", nullable: false),
                    LastSyncHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastSyncVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncDirection = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastReviewedDriftAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastCorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationBindings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationBindings_OrganizationId_RequestFormId_TaskTemplat~",
                table: "AutomationBindings",
                columns: new[] { "OrganizationId", "RequestFormId", "TaskTemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationBindings_OrganizationId_OrchestrationRequestDefinitionId",
                table: "AutomationBindings",
                columns: new[] { "OrganizationId", "OrchestrationRequestDefinitionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationBindings");
        }
    }
}
