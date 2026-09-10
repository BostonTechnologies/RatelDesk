using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRichTextWorklogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "WorkLogs",
                newName: "NotesText");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "TicketTimelineEvents",
                newName: "MessageText");

            migrationBuilder.AddColumn<string>(
                name: "NotesHtml",
                table: "WorkLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageHtml",
                table: "TicketTimelineEvents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotesHtml",
                table: "WorkLogs");

            migrationBuilder.DropColumn(
                name: "MessageHtml",
                table: "TicketTimelineEvents");

            migrationBuilder.RenameColumn(
                name: "NotesText",
                table: "WorkLogs",
                newName: "Notes");

            migrationBuilder.RenameColumn(
                name: "MessageText",
                table: "TicketTimelineEvents",
                newName: "Message");
        }
    }
}
