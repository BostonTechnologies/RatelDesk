using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestTaskRuntimePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpectedRuntimeSeconds",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GraceSeconds",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardTimeoutSeconds",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeoutIncidentId",
                table: "Tickets",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedRuntimeSeconds",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "GraceSeconds",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "HardTimeoutSeconds",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TimeoutIncidentId",
                table: "Tickets");
        }
    }
}
