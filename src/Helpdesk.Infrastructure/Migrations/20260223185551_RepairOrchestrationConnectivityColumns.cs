using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RepairOrchestrationConnectivityColumns : Migration
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
                        AND column_name = 'RemoteTokenEndpoint'
                    ) THEN
                        ALTER TABLE ""M2MConnectivitySettings""
                        ADD COLUMN ""RemoteTokenEndpoint"" character varying(1024) NULL;
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'M2MConnectivitySettings'
                        AND column_name = 'RemoteAuthority'
                    ) THEN
                        ALTER TABLE ""M2MConnectivitySettings""
                        ADD COLUMN ""RemoteAuthority"" character varying(512) NULL;
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
                        AND column_name = 'RemoteAuthority'
                    ) THEN
                        ALTER TABLE ""M2MConnectivitySettings""
                        DROP COLUMN ""RemoteAuthority"";
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'M2MConnectivitySettings'
                        AND column_name = 'RemoteTokenEndpoint'
                    ) THEN
                        ALTER TABLE ""M2MConnectivitySettings""
                        DROP COLUMN ""RemoteTokenEndpoint"";
                    END IF;
                END
                $$;
            ");
        }
    }
}
