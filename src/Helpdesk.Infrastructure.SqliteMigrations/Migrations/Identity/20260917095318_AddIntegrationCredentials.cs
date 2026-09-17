using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.SqliteMigrations.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddIntegrationCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SecretHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Permissions = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationCredentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentials_OwnerUserId",
                table: "IntegrationCredentials",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentials_Purpose_ExpiresAtUtc_RevokedAtUtc",
                table: "IntegrationCredentials",
                columns: new[] { "Purpose", "ExpiresAtUtc", "RevokedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationCredentials");
        }
    }
}
