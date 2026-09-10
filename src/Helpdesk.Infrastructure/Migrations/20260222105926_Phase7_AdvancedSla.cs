using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_AdvancedSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_ScopeType_TenantId_AppliesTo_IsActive",
                table: "SlaPolicies");

            migrationBuilder.AddColumn<long>(
                name: "AccumulatedPauseWorkingSeconds",
                table: "TicketSlaStates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "CalendarId",
                table: "TicketSlaStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBusinessHours",
                table: "TicketSlaStates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MatchRank",
                table: "SlaPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "SlaPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceId",
                table: "SlaPolicies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetsJson",
                table: "SlaEscalationRules",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.CreateTable(
                name: "SlaReportSubscriptions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    WeeklyDay = table.Column<int>(type: "integer", nullable: true),
                    SendTimeLocal = table.Column<TimeSpan>(type: "interval", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    TargetsJson = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'[]'"),
                    LookbackDays = table.Column<int>(type: "integer", nullable: false),
                    IncludeCsvAttachment = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeExcelAttachment = table.Column<bool>(type: "boolean", nullable: false),
                    TicketType = table.Column<int>(type: "integer", nullable: true),
                    ServiceId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaReportSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantSlaSettings",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    UseBusinessHours = table.Column<bool>(type: "boolean", nullable: false),
                    CalendarId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSlaSettings", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "WorkingCalendars",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    WeeklyRulesJson = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'[]'"),
                    ExceptionsJson = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'[]'"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaReportSendEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SubscriptionId = table.Column<string>(type: "text", nullable: false),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaReportSendEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaReportSendEvents_SlaReportSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "SlaReportSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaStates_CalendarId",
                table: "TicketSlaStates",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_ScopeType_AppliesTo_ServiceId_Priority_IsActive",
                table: "SlaPolicies",
                columns: new[] { "ScopeType", "AppliesTo", "ServiceId", "Priority", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_TenantId_AppliesTo_ServiceId_Priority_IsActive",
                table: "SlaPolicies",
                columns: new[] { "TenantId", "AppliesTo", "ServiceId", "Priority", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaReportSendEvents_SubscriptionId_PeriodStartUtc_PeriodEnd~",
                table: "SlaReportSendEvents",
                columns: new[] { "SubscriptionId", "PeriodStartUtc", "PeriodEndUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaReportSubscriptions_Frequency_IsActive",
                table: "SlaReportSubscriptions",
                columns: new[] { "Frequency", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaReportSubscriptions_TenantId_IsActive",
                table: "SlaReportSubscriptions",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSlaSettings_CalendarId",
                table: "TenantSlaSettings",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendars_ScopeType_IsActive",
                table: "WorkingCalendars",
                columns: new[] { "ScopeType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendars_TenantId_IsActive",
                table: "WorkingCalendars",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlaReportSendEvents");

            migrationBuilder.DropTable(
                name: "TenantSlaSettings");

            migrationBuilder.DropTable(
                name: "WorkingCalendars");

            migrationBuilder.DropTable(
                name: "SlaReportSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_TicketSlaStates_CalendarId",
                table: "TicketSlaStates");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_ScopeType_AppliesTo_ServiceId_Priority_IsActive",
                table: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_TenantId_AppliesTo_ServiceId_Priority_IsActive",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "AccumulatedPauseWorkingSeconds",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "CalendarId",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "IsBusinessHours",
                table: "TicketSlaStates");

            migrationBuilder.DropColumn(
                name: "MatchRank",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "TargetsJson",
                table: "SlaEscalationRules");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_ScopeType_TenantId_AppliesTo_IsActive",
                table: "SlaPolicies",
                columns: new[] { "ScopeType", "TenantId", "AppliesTo", "IsActive" });
        }
    }
}
