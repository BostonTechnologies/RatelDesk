using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TicketCategoryLinksMultiSelect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketCategories_CategoryId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketCategories_Change_CategoryId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketCategories_Incident_CategoryId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_CategoryId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_Change_CategoryId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_Incident_CategoryId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Change_CategoryId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Incident_CategoryId",
                table: "Tickets");

            migrationBuilder.CreateTable(
                name: "ChangeCategoryLinks",
                columns: table => new
                {
                    ChangeId = table.Column<string>(type: "text", nullable: false),
                    TicketCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeCategoryLinks", x => new { x.ChangeId, x.TicketCategoryId });
                    table.ForeignKey(
                        name: "FK_ChangeCategoryLinks_TicketCategories_TicketCategoryId",
                        column: x => x.TicketCategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeCategoryLinks_Tickets_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentCategoryLinks",
                columns: table => new
                {
                    IncidentId = table.Column<string>(type: "text", nullable: false),
                    TicketCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentCategoryLinks", x => new { x.IncidentId, x.TicketCategoryId });
                    table.ForeignKey(
                        name: "FK_IncidentCategoryLinks_TicketCategories_TicketCategoryId",
                        column: x => x.TicketCategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentCategoryLinks_Tickets_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestCategoryLinks",
                columns: table => new
                {
                    RequestId = table.Column<string>(type: "text", nullable: false),
                    TicketCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestCategoryLinks", x => new { x.RequestId, x.TicketCategoryId });
                    table.ForeignKey(
                        name: "FK_RequestCategoryLinks_TicketCategories_TicketCategoryId",
                        column: x => x.TicketCategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestCategoryLinks_Tickets_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeCategoryLinks_TicketCategoryId",
                table: "ChangeCategoryLinks",
                column: "TicketCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentCategoryLinks_TicketCategoryId",
                table: "IncidentCategoryLinks",
                column: "TicketCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCategoryLinks_TicketCategoryId",
                table: "RequestCategoryLinks",
                column: "TicketCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeCategoryLinks");

            migrationBuilder.DropTable(
                name: "IncidentCategoryLinks");

            migrationBuilder.DropTable(
                name: "RequestCategoryLinks");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Change_CategoryId",
                table: "Tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Incident_CategoryId",
                table: "Tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CategoryId",
                table: "Tickets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Change_CategoryId",
                table: "Tickets",
                column: "Change_CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Incident_CategoryId",
                table: "Tickets",
                column: "Incident_CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketCategories_CategoryId",
                table: "Tickets",
                column: "CategoryId",
                principalTable: "TicketCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketCategories_Change_CategoryId",
                table: "Tickets",
                column: "Change_CategoryId",
                principalTable: "TicketCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketCategories_Incident_CategoryId",
                table: "Tickets",
                column: "Incident_CategoryId",
                principalTable: "TicketCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
