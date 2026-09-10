using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase7b_SlaAdminStabilization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "AppliesTo"
                               ORDER BY "IsActive" DESC, "Id" DESC
                           ) AS rn
                    FROM "SlaPolicies"
                    WHERE "ScopeType" = 0
                )
                DELETE FROM "SlaPolicies" p
                USING ranked r
                WHERE p."Id" = r."Id"
                  AND r.rn > 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
