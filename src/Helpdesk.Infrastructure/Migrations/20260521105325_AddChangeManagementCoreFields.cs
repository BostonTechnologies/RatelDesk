using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeManagementCoreFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApproverUserIdsJson",
                table: "Tickets",
                type: "text",
                nullable: true,
                defaultValueSql: "'[]'");

            migrationBuilder.AddColumn<string>(
                name: "ImplementorUserId",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedForUserId",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItSupportOrganizationId",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ImplementorUserId",
                table: "Tickets",
                column: "ImplementorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RequestedForUserId",
                table: "Tickets",
                column: "RequestedForUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_ItSupportOrganizationId",
                table: "Organizations",
                column: "ItSupportOrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Organizations_Organizations_ItSupportOrganizationId",
                table: "Organizations",
                column: "ItSupportOrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Organizations_Organizations_ItSupportOrganizationId",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ImplementorUserId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_RequestedForUserId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_ItSupportOrganizationId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ApproverUserIdsJson",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ImplementorUserId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RequestedForUserId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ItSupportOrganizationId",
                table: "Organizations");
        }
    }
}
