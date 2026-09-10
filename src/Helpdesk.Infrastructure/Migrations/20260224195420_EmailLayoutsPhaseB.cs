using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EmailLayoutsPhaseB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LayoutId",
                table: "EmailTemplates",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    HtmlContent = table.Column<string>(type: "text", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailLayouts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_LayoutId",
                table: "EmailTemplates",
                column: "LayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailLayouts_Name_TenantId",
                table: "EmailLayouts",
                columns: new[] { "Name", "TenantId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailTemplates_EmailLayouts_LayoutId",
                table: "EmailTemplates",
                column: "LayoutId",
                principalTable: "EmailLayouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailTemplates_EmailLayouts_LayoutId",
                table: "EmailTemplates");

            migrationBuilder.DropTable(
                name: "EmailLayouts");

            migrationBuilder.DropIndex(
                name: "IX_EmailTemplates_LayoutId",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "LayoutId",
                table: "EmailTemplates");
        }
    }
}
