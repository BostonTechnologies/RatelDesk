using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Helpdesk.Infrastructure.SqliteMigrations.Migrations.Helpdesk;

[DbContext(typeof(HelpdeskDbContext))]
[Migration("20260914070000_AddSqliteAuditDateOrdering")]
public sealed class AddSqliteAuditDateOrdering : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"AiInvestigationWorklogEntries\" ADD COLUMN \"OccurredUtcSortTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"OccurredUtc\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.CreateIndex(name: "IX_AiInvestigationWorklogEntries_OccurredUtcSortTicks", table: "AiInvestigationWorklogEntries", column: "OccurredUtcSortTicks");
        migrationBuilder.Sql("ALTER TABLE \"AiOperationAuditRecords\" ADD COLUMN \"CreatedAtSortTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"CreatedAt\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.CreateIndex(name: "IX_AiOperationAuditRecords_CreatedAtSortTicks", table: "AiOperationAuditRecords", column: "CreatedAtSortTicks");
        migrationBuilder.Sql("ALTER TABLE \"DatasetIngestCredentials\" ADD COLUMN \"CreatedAtUtcSortTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"CreatedAtUtc\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.CreateIndex(name: "IX_DatasetIngestCredentials_CreatedAtUtcSortTicks", table: "DatasetIngestCredentials", column: "CreatedAtUtcSortTicks");
        migrationBuilder.Sql("ALTER TABLE \"InboundEmailProcessingLogs\" ADD COLUMN \"CreatedAtUtcSortTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"CreatedAtUtc\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.CreateIndex(name: "IX_InboundEmailProcessingLogs_CreatedAtUtcSortTicks", table: "InboundEmailProcessingLogs", column: "CreatedAtUtcSortTicks");
        migrationBuilder.Sql("ALTER TABLE \"TicketAiFeedback\" ADD COLUMN \"CreatedAtSortTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"CreatedAt\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.CreateIndex(name: "IX_TicketAiFeedback_CreatedAtSortTicks", table: "TicketAiFeedback", column: "CreatedAtSortTicks");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_TicketAiFeedback_CreatedAtSortTicks", table: "TicketAiFeedback");
        migrationBuilder.Sql("ALTER TABLE \"TicketAiFeedback\" DROP COLUMN \"CreatedAtSortTicks\"");
        migrationBuilder.DropIndex(name: "IX_InboundEmailProcessingLogs_CreatedAtUtcSortTicks", table: "InboundEmailProcessingLogs");
        migrationBuilder.Sql("ALTER TABLE \"InboundEmailProcessingLogs\" DROP COLUMN \"CreatedAtUtcSortTicks\"");
        migrationBuilder.DropIndex(name: "IX_DatasetIngestCredentials_CreatedAtUtcSortTicks", table: "DatasetIngestCredentials");
        migrationBuilder.Sql("ALTER TABLE \"DatasetIngestCredentials\" DROP COLUMN \"CreatedAtUtcSortTicks\"");
        migrationBuilder.DropIndex(name: "IX_AiOperationAuditRecords_CreatedAtSortTicks", table: "AiOperationAuditRecords");
        migrationBuilder.Sql("ALTER TABLE \"AiOperationAuditRecords\" DROP COLUMN \"CreatedAtSortTicks\"");
        migrationBuilder.DropIndex(name: "IX_AiInvestigationWorklogEntries_OccurredUtcSortTicks", table: "AiInvestigationWorklogEntries");
        migrationBuilder.Sql("ALTER TABLE \"AiInvestigationWorklogEntries\" DROP COLUMN \"OccurredUtcSortTicks\"");
    }
}
