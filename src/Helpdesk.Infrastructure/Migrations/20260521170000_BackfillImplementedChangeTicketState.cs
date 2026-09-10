using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillImplementedChangeTicketState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Tickets"
                SET "State" = 5,
                    "ClosedAt" = COALESCE("ClosedAt", CURRENT_TIMESTAMP)
                WHERE "Discriminator" = 'Change'
                  AND "LifecycleState" IN (5, 6)
                  AND "State" <> 5;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
