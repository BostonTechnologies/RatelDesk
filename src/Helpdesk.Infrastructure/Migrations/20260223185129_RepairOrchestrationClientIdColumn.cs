using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RepairOrchestrationClientIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'M2MConnectivitySettings'
                        AND column_name = 'ClientId'
                    ) THEN
                        ALTER TABLE ""M2MConnectivitySettings""
                        ADD COLUMN ""ClientId"" character varying(256) NULL;
                    END IF;
                END
                $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'M2MConnectivitySettings'
                        AND column_name = 'ClientId'
                    ) THEN
                        ALTER TABLE ""M2MConnectivitySettings""
                        DROP COLUMN ""ClientId"";
                    END IF;
                END
                $$;
            ");
        }
    }
}
