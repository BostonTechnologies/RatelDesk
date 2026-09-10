using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestTaskApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequestTaskApprovals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RequestId = table.Column<string>(type: "text", nullable: false),
                    RequestTaskId = table.Column<string>(type: "text", nullable: false),
                    ApproverSource = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApproverId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApproverName = table.Column<string>(type: "text", nullable: false),
                    ApproverEmail = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OrganizationName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ViewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TokenSentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTaskApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestTaskApprovals_Tickets_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestTaskApprovals_Tickets_RequestTaskId",
                        column: x => x.RequestTaskId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_ApproverEmail",
                table: "RequestTaskApprovals",
                column: "ApproverEmail");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_RequestId",
                table: "RequestTaskApprovals",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_RequestTaskId",
                table: "RequestTaskApprovals",
                column: "RequestTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_RequestTaskId_ApproverEmail",
                table: "RequestTaskApprovals",
                columns: new[] { "RequestTaskId", "ApproverEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_Status",
                table: "RequestTaskApprovals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_ViewedAtUtc",
                table: "RequestTaskApprovals",
                column: "ViewedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestTaskApprovals");
        }
    }
}
