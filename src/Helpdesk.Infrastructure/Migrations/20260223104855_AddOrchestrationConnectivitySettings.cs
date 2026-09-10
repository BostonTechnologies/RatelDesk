using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrchestrationConnectivitySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "M2MConnectivitySettings" (
                    "Id" uuid NOT NULL,
                    "Enabled" boolean NOT NULL,
                    "RemoteBaseUrl" character varying(1024),
                    "RemoteAudience" character varying(256),
                    "RemoteSystemName" character varying(128),
                    "RemoteTokenEndpoint" character varying(1024),
                    "RemoteAuthority" character varying(512),
                    "ClientId" character varying(256),
                    "UpdatedAtUtc" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_M2MConnectivitySettings" PRIMARY KEY ("Id")
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "M2MConnectivitySettings");
        }
    }
}
