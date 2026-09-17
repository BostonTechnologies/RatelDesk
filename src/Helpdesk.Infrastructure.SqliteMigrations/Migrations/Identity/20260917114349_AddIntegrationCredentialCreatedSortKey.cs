using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.SqliteMigrations.Migrations.Identity
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
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE "IntegrationCredentials"
                SET "CreatedAtUnixMilliseconds" =
                    CAST(strftime('%s', "CreatedAtUtc") AS INTEGER) * 1000 +
                    CAST(substr(strftime('%f', "CreatedAtUtc"), 4, 3) AS INTEGER)
                WHERE "CreatedAtUnixMilliseconds" = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentials_OwnerUserId_CreatedAtUnixMilliseconds_Id",
                table: "IntegrationCredentials",
                columns: new[] { "OwnerUserId", "CreatedAtUnixMilliseconds", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationCredentials_OwnerUserId_CreatedAtUnixMilliseconds_Id",
                table: "IntegrationCredentials");

            migrationBuilder.DropColumn(
                name: "CreatedAtUnixMilliseconds",
                table: "IntegrationCredentials");
        }
    }
}
