using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.SqliteMigrations.Migrations.Helpdesk
{
    /// <inheritdoc />
    public partial class AddExternalIdentityDomainUserLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DomainUserId",
                table: "CustomerAuthLinks",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuthLinks_DomainUserId",
                table: "CustomerAuthLinks",
                column: "DomainUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerAuthLinks_DomainUserId",
                table: "CustomerAuthLinks");

            migrationBuilder.DropColumn(
                name: "DomainUserId",
                table: "CustomerAuthLinks");
        }
    }
}
