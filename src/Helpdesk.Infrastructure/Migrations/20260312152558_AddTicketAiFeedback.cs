using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAiFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketAiFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<string>(type: "text", nullable: false),
                    FeedbackType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FeedbackValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ArticleId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAiFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAiFeedback_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAiFeedback_TicketId_ArticleId_FeedbackType_FeedbackVa~",
                table: "TicketAiFeedback",
                columns: new[] { "TicketId", "ArticleId", "FeedbackType", "FeedbackValue" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAiFeedback_TicketId_CreatedAt",
                table: "TicketAiFeedback",
                columns: new[] { "TicketId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAiFeedback_TicketId_RequestId_FeedbackType_FeedbackVa~",
                table: "TicketAiFeedback",
                columns: new[] { "TicketId", "RequestId", "FeedbackType", "FeedbackValue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketAiFeedback");
        }
    }
}
