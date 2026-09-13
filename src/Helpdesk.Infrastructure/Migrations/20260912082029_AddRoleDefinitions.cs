using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "IsBuiltIn",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsProtected",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "Roles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerOrganizationId",
                table: "Roles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "Roles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Existing free-text roles predate stable definition keys. Keep every
            // legacy row distinct; new built-ins and custom roles receive their
            // intentional keys through the role-definition API.
            migrationBuilder.Sql("UPDATE \"Roles\" SET \"Key\" = 'legacy-' || \"Id\" WHERE \"Key\" IS NULL OR \"Key\" = '';");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "Roles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Permission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.Permission });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Key",
                table: "Roles",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Scope_OwnerOrganizationId",
                table: "Roles",
                columns: new[] { "Scope", "OwnerOrganizationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Key",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Scope_OwnerOrganizationId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsBuiltIn",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsProtected",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "OwnerOrganizationId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "Roles");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Roles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);
        }
    }
}
