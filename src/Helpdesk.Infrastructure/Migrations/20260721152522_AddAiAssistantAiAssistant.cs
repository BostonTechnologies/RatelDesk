using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAssistantAiAssistant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiInvestigationInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<string>(type: "text", nullable: false),
                    TicketId = table.Column<string>(type: "text", nullable: false),
                    TicketArea = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AiAssistantRunReference = table.Column<string>(type: "text", nullable: true),
                    FailureMessage = table.Column<string>(type: "text", nullable: true),
                    DispatchAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInvestigationInvocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiInvestigationWorklogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<string>(type: "text", nullable: false),
                    TicketId = table.Column<string>(type: "text", nullable: false),
                    TicketArea = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    ArtifactReferencesJson = table.Column<string>(type: "text", nullable: true),
                    CallbackEventId = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInvestigationWorklogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAssistantWebhookAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<string>(type: "text", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssistantWebhookAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAssistantWebhookConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    TicketAreasJson = table.Column<string>(type: "text", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    RouteName = table.Column<string>(type: "text", nullable: true),
                    PromptTemplate = table.Column<string>(type: "text", nullable: true),
                    PermittedInputsSchemaJson = table.Column<string>(type: "text", nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxBodyBytes = table.Column<int>(type: "integer", nullable: false),
                    SecretReference = table.Column<string>(type: "text", nullable: true),
                    LastDispatchUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastHealthMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssistantWebhookConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvestigationInvocations_CorrelationId",
                table: "AiInvestigationInvocations",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiInvestigationInvocations_OrganizationId_IdempotencyKey",
                table: "AiInvestigationInvocations",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiInvestigationInvocations_OrganizationId_TicketArea_Ticket~",
                table: "AiInvestigationInvocations",
                columns: new[] { "OrganizationId", "TicketArea", "TicketId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvestigationWorklogEntries_CallbackEventId",
                table: "AiInvestigationWorklogEntries",
                column: "CallbackEventId",
                unique: true,
                filter: "\"CallbackEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiInvestigationWorklogEntries_OrganizationId_TicketArea_Tic~",
                table: "AiInvestigationWorklogEntries",
                columns: new[] { "OrganizationId", "TicketArea", "TicketId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantWebhookAuditRecords_OrganizationId_CreatedUtc",
                table: "AiAssistantWebhookAuditRecords",
                columns: new[] { "OrganizationId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantWebhookConfigurations_OrganizationId_IsEnabled_IsArc~",
                table: "AiAssistantWebhookConfigurations",
                columns: new[] { "OrganizationId", "IsEnabled", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantWebhookConfigurations_OrganizationId_Name",
                table: "AiAssistantWebhookConfigurations",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiInvestigationInvocations");

            migrationBuilder.DropTable(
                name: "AiInvestigationWorklogEntries");

            migrationBuilder.DropTable(
                name: "AiAssistantWebhookAuditRecords");

            migrationBuilder.DropTable(
                name: "AiAssistantWebhookConfigurations");
        }
    }
}
