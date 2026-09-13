using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalCustomerAccountLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerAuthLinks_CustomerId",
                table: "CustomerAuthLinks");

            migrationBuilder.AddColumn<string>(
                name: "LocalAccountId",
                table: "CustomerAuthLinks",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuthLinks_CustomerId",
                table: "CustomerAuthLinks",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuthLinks_LocalAccountId",
                table: "CustomerAuthLinks",
                column: "LocalAccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerAuthLinks_CustomerId",
                table: "CustomerAuthLinks");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAuthLinks_LocalAccountId",
                table: "CustomerAuthLinks");

            migrationBuilder.DropColumn(
                name: "LocalAccountId",
                table: "CustomerAuthLinks");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuthLinks_CustomerId",
                table: "CustomerAuthLinks",
                column: "CustomerId",
                unique: true);
        }
    }
}
