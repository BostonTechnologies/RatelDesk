using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeApprovalHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "LifecycleState",
                table: "Tickets",
                type: "integer",
                nullable: true,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.Sql("""
                UPDATE "Tickets"
                SET "LifecycleState" = 0
                WHERE "Discriminator" = 'Change'
                  AND "LifecycleState" IS NULL;
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ViewedAtUtc",
                table: "ChangeApprovals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_ViewedAtUtc",
                table: "ChangeApprovals",
                column: "ViewedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChangeApprovals_ViewedAtUtc",
                table: "ChangeApprovals");

            migrationBuilder.DropColumn(
                name: "ViewedAtUtc",
                table: "ChangeApprovals");

            migrationBuilder.AlterColumn<int>(
                name: "LifecycleState",
                table: "Tickets",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldDefaultValue: 0);
        }
    }
}
