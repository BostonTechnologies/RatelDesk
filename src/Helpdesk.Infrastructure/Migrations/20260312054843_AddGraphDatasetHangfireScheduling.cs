using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGraphDatasetHangfireScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundSyncCronExpression",
                table: "TenantGraphDatasetSettings",
                type: "text",
                nullable: false,
                defaultValue: "0 */6 * * *");

            migrationBuilder.AddColumn<bool>(
                name: "BackgroundSyncEnabled",
                table: "TenantGraphDatasetSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundSyncCronExpression",
                table: "TenantGraphDatasetSettings");

            migrationBuilder.DropColumn(
                name: "BackgroundSyncEnabled",
                table: "TenantGraphDatasetSettings");
        }
    }
}
