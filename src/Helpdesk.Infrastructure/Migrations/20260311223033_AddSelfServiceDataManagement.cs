using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfServiceDataManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatasetColumns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DatasetId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<int>(type: "integer", nullable: false),
                    IsKey = table.Column<bool>(type: "boolean", nullable: false),
                    IsDisplay = table.Column<bool>(type: "boolean", nullable: false),
                    IsSearchable = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetColumns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatasetDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    KeyColumn = table.Column<string>(type: "text", nullable: false),
                    DisplayColumn = table.Column<string>(type: "text", nullable: false),
                    SearchColumnsJson = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'[]'"),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatasetIngestCredentials",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DatasetId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    KeyHash = table.Column<string>(type: "text", nullable: false),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetIngestCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatasetRows",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DatasetId = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<string>(type: "text", nullable: false),
                    ExternalKey = table.Column<string>(type: "text", nullable: false),
                    DataJson = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    RowHash = table.Column<string>(type: "text", nullable: false),
                    SourceUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastIngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetRows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantGraphDatasetSettings",
                columns: table => new
                {
                    OrganizationId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    ClientId = table.Column<string>(type: "text", nullable: true),
                    ClientSecretProtected = table.Column<string>(type: "text", nullable: true),
                    EnableUsers = table.Column<bool>(type: "boolean", nullable: false),
                    EnableDevices = table.Column<bool>(type: "boolean", nullable: false),
                    EnableGroups = table.Column<bool>(type: "boolean", nullable: false),
                    EnableSharePointSites = table.Column<bool>(type: "boolean", nullable: false),
                    LastUsersSyncUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastDevicesSyncUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastGroupsSyncUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSharePointSitesSyncUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "text", nullable: true),
                    LastSyncMessage = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantGraphDatasetSettings", x => x.OrganizationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetColumns_DatasetId_Name",
                table: "DatasetColumns",
                columns: new[] { "DatasetId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetDefinitions_OrganizationId_Slug",
                table: "DatasetDefinitions",
                columns: new[] { "OrganizationId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetDefinitions_OrganizationId_SourceType",
                table: "DatasetDefinitions",
                columns: new[] { "OrganizationId", "SourceType" });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetIngestCredentials_DatasetId_KeyHash",
                table: "DatasetIngestCredentials",
                columns: new[] { "DatasetId", "KeyHash" });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetIngestCredentials_DatasetId_Name",
                table: "DatasetIngestCredentials",
                columns: new[] { "DatasetId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetRows_DatasetId_ExternalKey",
                table: "DatasetRows",
                columns: new[] { "DatasetId", "ExternalKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetRows_DatasetId_OrganizationId",
                table: "DatasetRows",
                columns: new[] { "DatasetId", "OrganizationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatasetColumns");

            migrationBuilder.DropTable(
                name: "DatasetDefinitions");

            migrationBuilder.DropTable(
                name: "DatasetIngestCredentials");

            migrationBuilder.DropTable(
                name: "DatasetRows");

            migrationBuilder.DropTable(
                name: "TenantGraphDatasetSettings");
        }
    }
}
