using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Helpdesk.Infrastructure.SqliteMigrations.Migrations.Helpdesk;

[DbContext(typeof(HelpdeskDbContext))]
[Migration("20260914071000_AddSqliteAutomationBindingDateOrdering")]
public sealed class AddSqliteAutomationBindingDateOrdering : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"AutomationBindings\" ADD COLUMN \"UpdatedAtUtcSortTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"UpdatedAtUtc\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.CreateIndex(name: "IX_AutomationBindings_SyncState_UpdatedAtUtcSortTicks", table: "AutomationBindings", columns: ["SyncState", "UpdatedAtUtcSortTicks"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AutomationBindings_SyncState_UpdatedAtUtcSortTicks", table: "AutomationBindings");
        migrationBuilder.Sql("ALTER TABLE \"AutomationBindings\" DROP COLUMN \"UpdatedAtUtcSortTicks\"");
    }
}
