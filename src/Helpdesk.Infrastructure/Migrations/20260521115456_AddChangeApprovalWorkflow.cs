using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LifecycleState",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChangeApprovals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ChangeId = table.Column<string>(type: "text", nullable: false),
                    ApproverId = table.Column<string>(type: "text", nullable: true),
                    ApproverName = table.Column<string>(type: "text", nullable: false),
                    ApproverEmail = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TokenSentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeApprovals_Tickets_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_ApproverEmail",
                table: "ChangeApprovals",
                column: "ApproverEmail");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_ChangeId",
                table: "ChangeApprovals",
                column: "ChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_ChangeId_ApproverEmail",
                table: "ChangeApprovals",
                columns: new[] { "ChangeId", "ApproverEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_Status",
                table: "ChangeApprovals",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeApprovals");

            migrationBuilder.DropColumn(
                name: "LifecycleState",
                table: "Tickets");
        }
    }
}
