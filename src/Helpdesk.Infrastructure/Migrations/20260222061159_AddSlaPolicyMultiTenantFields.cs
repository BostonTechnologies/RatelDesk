using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaPolicyMultiTenantFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppliesTo",
                table: "SlaPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "SlaPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeType",
                table: "SlaPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "SlaPolicies",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_ScopeType_TenantId_AppliesTo_IsActive",
                table: "SlaPolicies",
                columns: new[] { "ScopeType", "TenantId", "AppliesTo", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_ScopeType_TenantId_AppliesTo_IsActive",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "AppliesTo",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "ScopeType",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SlaPolicies");
        }
    }
}
