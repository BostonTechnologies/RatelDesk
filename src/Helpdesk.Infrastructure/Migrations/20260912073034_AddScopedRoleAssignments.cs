using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedRoleAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScopedRoleAssignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RoleKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrganizationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopedRoleAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScopedRoleAssignments_OrganizationId_RoleKey",
                table: "ScopedRoleAssignments",
                columns: new[] { "OrganizationId", "RoleKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ScopedRoleAssignments_UserId_RoleKey_OrganizationId",
                table: "ScopedRoleAssignments",
                columns: new[] { "UserId", "RoleKey", "OrganizationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScopedRoleAssignments");
        }
    }
}
