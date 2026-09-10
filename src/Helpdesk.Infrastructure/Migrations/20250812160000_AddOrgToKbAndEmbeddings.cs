using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgToKbAndEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrganizationId",
                table: "KnowledgeBaseArticles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Service",
                table: "KnowledgeBaseArticles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrganizationId",
                table: "KnowledgeEmbeddings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "KnowledgeEmbeddings",
                type: "text",
                nullable: false,
                defaultValue: "KB");

            migrationBuilder.AddColumn<string>(
                name: "SourceId",
                table: "KnowledgeEmbeddings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChunkId",
                table: "KnowledgeEmbeddings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "KnowledgeEmbeddings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "LastScore",
                table: "KnowledgeEmbeddings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticles_OrganizationId",
                table: "KnowledgeBaseArticles",
                column: "OrganizationId");

            // Ensure pgvector is available (safe if already installed)
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS vector;");

            // ✅ Create HNSW index with the correct operator class.
            // Use vector_l2_ops unless you explicitly use cosine or inner-product.
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_embedding_org_hnsw
ON ""KnowledgeEmbeddings""
USING hnsw (""Vector"" vector_l2_ops);
");

            // btree helper for filters
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_embedding_org_source
ON ""KnowledgeEmbeddings"" (""OrganizationId"", ""SourceType"");
");
        }
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_embedding_org_hnsw;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_embedding_org_source;");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBaseArticles_OrganizationId",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(name: "OrganizationId", table: "KnowledgeBaseArticles");
            migrationBuilder.DropColumn(name: "Service", table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(name: "OrganizationId", table: "KnowledgeEmbeddings");
            migrationBuilder.DropColumn(name: "SourceType", table: "KnowledgeEmbeddings");
            migrationBuilder.DropColumn(name: "SourceId", table: "KnowledgeEmbeddings");
            migrationBuilder.DropColumn(name: "ChunkId", table: "KnowledgeEmbeddings");
            migrationBuilder.DropColumn(name: "Text", table: "KnowledgeEmbeddings");
            migrationBuilder.DropColumn(name: "LastScore", table: "KnowledgeEmbeddings");
        }
    }
}

