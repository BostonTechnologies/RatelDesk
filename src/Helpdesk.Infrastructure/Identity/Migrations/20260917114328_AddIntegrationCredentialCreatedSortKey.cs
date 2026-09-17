using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationCredentialCreatedSortKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatedAtUnixMilliseconds",
                table: "IntegrationCredentials",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE "IntegrationCredentials"
                SET "CreatedAtUnixMilliseconds" = CAST(EXTRACT(EPOCH FROM "CreatedAtUtc") * 1000 AS bigint)
                WHERE "CreatedAtUnixMilliseconds" = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentials_OwnerUserId_CreatedAtUnixMillisecond~",
                table: "IntegrationCredentials",
                columns: new[] { "OwnerUserId", "CreatedAtUnixMilliseconds", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationCredentials_OwnerUserId_CreatedAtUnixMillisecond~",
                table: "IntegrationCredentials");

            migrationBuilder.DropColumn(
                name: "CreatedAtUnixMilliseconds",
                table: "IntegrationCredentials");
        }
    }
}
