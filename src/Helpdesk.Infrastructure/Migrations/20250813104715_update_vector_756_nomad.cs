using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class update_vector_756_nomad : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop ANN index before changing the vector dimension
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_embedding_org_hnsw;");

            // If safe to wipe old embeddings (most teams do when changing dims):
            // migrationBuilder.Sql(@"TRUNCATE TABLE ""KnowledgeEmbeddings"";");

            // Change column to 768 dims
            migrationBuilder.Sql(@"
ALTER TABLE ""KnowledgeEmbeddings""
ALTER COLUMN ""Vector"" TYPE vector(768);
");

            // Recreate HNSW index with correct opclass (L2 here; use cosine/ip if that's your metric)
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_embedding_org_hnsw
ON ""KnowledgeEmbeddings""
USING hnsw (""Vector"" vector_l2_ops);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_embedding_org_hnsw;");

            // Revert to 1536 dims
            migrationBuilder.Sql(@"
ALTER TABLE ""KnowledgeEmbeddings""
ALTER COLUMN ""Vector"" TYPE vector(1536);
");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_embedding_org_hnsw
ON ""KnowledgeEmbeddings""
USING hnsw (""Vector"" vector_l2_ops);
");
        }

    }
}
