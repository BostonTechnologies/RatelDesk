using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeChunkMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChunkIndex",
                table: "KnowledgeEmbeddings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DocumentTitle",
                table: "KnowledgeEmbeddings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "KnowledgeEmbeddings",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateIndex(
                name: "ix_embedding_org_document_chunk",
                table: "KnowledgeEmbeddings",
                columns: new[] { "OrganizationId", "SourceType", "SourceId", "ChunkIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_embedding_org_document_chunk",
                table: "KnowledgeEmbeddings");

            migrationBuilder.DropColumn(
                name: "ChunkIndex",
                table: "KnowledgeEmbeddings");

            migrationBuilder.DropColumn(
                name: "DocumentTitle",
                table: "KnowledgeEmbeddings");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "KnowledgeEmbeddings");
        }
    }
}
