using Microsoft.EntityFrameworkCore.Migrations;

namespace Helpdesk.Infrastructure.SqliteMigrations.Migrations.Helpdesk;

[Migration("20260912190000_AddSqliteUtcTickColumns")]
public partial class AddSqliteUtcTickColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Tickets\" ADD COLUMN \"DueAtUtcTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"DueAt\") - 2440587.5) * 864000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.Sql("ALTER TABLE \"Tickets\" ADD COLUMN \"NextRetryAtUtcTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"NextRetryAt\") - 2440587.5) * 864000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.CreateIndex(name: "IX_Tickets_Status_DueAtUtcTicks", table: "Tickets", columns: new[] { "Status", "DueAtUtcTicks" });
        migrationBuilder.CreateIndex(name: "IX_Tickets_Status_NextRetryAtUtcTicks", table: "Tickets", columns: new[] { "Status", "NextRetryAtUtcTicks" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Tickets_Status_DueAtUtcTicks", table: "Tickets");
        migrationBuilder.DropIndex(name: "IX_Tickets_Status_NextRetryAtUtcTicks", table: "Tickets");
        migrationBuilder.DropColumn(name: "DueAtUtcTicks", table: "Tickets");
        migrationBuilder.DropColumn(name: "NextRetryAtUtcTicks", table: "Tickets");
    }
}
