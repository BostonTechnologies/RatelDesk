using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillChangeTrackingIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Tickets"
                SET "TrackingId" = 'CHG-' ||
                    upper(substring(replace("Id", '-', '') from 1 for 3)) || '-' ||
                    upper(substring(replace("Id", '-', '') from 4 for 3)) || '-' ||
                    upper(substring(replace("Id", '-', '') from 7 for 3))
                WHERE "Discriminator" = 'Change'
                  AND ("TrackingId" IS NULL OR btrim("TrackingId") = '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
