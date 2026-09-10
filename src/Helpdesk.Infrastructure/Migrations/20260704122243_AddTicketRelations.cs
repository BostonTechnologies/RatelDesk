using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTicketId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetTicketId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RelationType = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedByUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketRelations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRelations_SourceTicketId",
                table: "TicketRelations",
                column: "SourceTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRelations_SourceTicketId_TargetTicketId_RelationType",
                table: "TicketRelations",
                columns: new[] { "SourceTicketId", "TargetTicketId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketRelations_TargetTicketId",
                table: "TicketRelations",
                column: "TargetTicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketRelations");
        }
    }
}
