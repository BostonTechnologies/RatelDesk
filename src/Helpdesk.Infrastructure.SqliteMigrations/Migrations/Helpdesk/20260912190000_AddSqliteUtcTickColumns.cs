using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Helpdesk.Infrastructure.SqliteMigrations.Migrations.Helpdesk;

[DbContext(typeof(HelpdeskDbContext))]
[Migration("20260912190000_AddSqliteUtcTickColumns")]
public partial class AddSqliteUtcTickColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Tickets\" ADD COLUMN \"DueAtUtcTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"DueAt\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.Sql("ALTER TABLE \"Tickets\" ADD COLUMN \"NextRetryAtUtcTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"NextRetryAt\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.Sql("ALTER TABLE \"TicketSlaStates\" ADD COLUMN \"CompletedAtUtcTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"CompletedAt\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.Sql("ALTER TABLE \"TicketSlaStates\" ADD COLUMN \"ResolutionDueAtUtcTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"ResolutionDueAt\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.Sql("ALTER TABLE \"TicketSlaStates\" ADD COLUMN \"ResponseDueAtUtcTicks\" INTEGER GENERATED ALWAYS AS (CAST((julianday(\"ResponseDueAt\") - 2440587.5) * 864000000000 + 621355968000000000 AS INTEGER)) VIRTUAL");
        migrationBuilder.CreateIndex(name: "IX_Tickets_Status_DueAtUtcTicks", table: "Tickets", columns: new[] { "Status", "DueAtUtcTicks" });
        migrationBuilder.CreateIndex(name: "IX_Tickets_Status_NextRetryAtUtcTicks", table: "Tickets", columns: new[] { "Status", "NextRetryAtUtcTicks" });
        migrationBuilder.CreateIndex(name: "IX_TicketSlaStates_Status_CompletedAtUtcTicks", table: "TicketSlaStates", columns: new[] { "Status", "CompletedAtUtcTicks" });
        migrationBuilder.CreateIndex(name: "IX_TicketSlaStates_ResolutionDueAtUtcTicks", table: "TicketSlaStates", column: "ResolutionDueAtUtcTicks");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_TicketSlaStates_Status_CompletedAtUtcTicks", table: "TicketSlaStates");
        migrationBuilder.DropIndex(name: "IX_TicketSlaStates_ResolutionDueAtUtcTicks", table: "TicketSlaStates");
        migrationBuilder.DropIndex(name: "IX_Tickets_Status_DueAtUtcTicks", table: "Tickets");
        migrationBuilder.DropIndex(name: "IX_Tickets_Status_NextRetryAtUtcTicks", table: "Tickets");
        migrationBuilder.Sql("ALTER TABLE \"TicketSlaStates\" DROP COLUMN \"CompletedAtUtcTicks\"");
        migrationBuilder.Sql("ALTER TABLE \"TicketSlaStates\" DROP COLUMN \"ResolutionDueAtUtcTicks\"");
        migrationBuilder.Sql("ALTER TABLE \"TicketSlaStates\" DROP COLUMN \"ResponseDueAtUtcTicks\"");
        migrationBuilder.Sql("ALTER TABLE \"Tickets\" DROP COLUMN \"DueAtUtcTicks\"");
        migrationBuilder.Sql("ALTER TABLE \"Tickets\" DROP COLUMN \"NextRetryAtUtcTicks\"");
    }
}
