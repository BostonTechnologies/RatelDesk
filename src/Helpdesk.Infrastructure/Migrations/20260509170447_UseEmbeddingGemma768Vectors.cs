using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UseEmbeddingGemma768Vectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_embedding_org_hnsw;");
            migrationBuilder.Sql(@"TRUNCATE TABLE ""KnowledgeEmbeddings"";");
            migrationBuilder.Sql(@"
ALTER TABLE ""KnowledgeEmbeddings""
ALTER COLUMN ""Vector"" TYPE vector(768);
");
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_embedding_org_hnsw
ON ""KnowledgeEmbeddings""
USING hnsw (""Vector"" vector_l2_ops);
");
            migrationBuilder.Sql(@"
INSERT INTO ""AiProviders""
    (""Id"", ""Name"", ""ProviderType"", ""BaseUrl"", ""ApiKeyEncrypted"", ""IsEnabled"", ""DefaultModel"", ""ExtraHeadersJson"", ""CreatedAt"", ""UpdatedAt"")
VALUES
    ('83f1b5f6-6314-4f5d-817e-36bd447d3f97', 'Local embeddings', 'OpenAICompatible', 'http://localhost:11434', '', FALSE, 'embeddinggemma', NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT (""Id"") DO UPDATE SET
    ""Name"" = EXCLUDED.""Name"",
    ""ProviderType"" = EXCLUDED.""ProviderType"",
    ""BaseUrl"" = EXCLUDED.""BaseUrl"",
    ""IsEnabled"" = EXCLUDED.""IsEnabled"",
    ""DefaultModel"" = EXCLUDED.""DefaultModel"",
    ""UpdatedAt"" = CURRENT_TIMESTAMP;
");
            migrationBuilder.Sql(@"
INSERT INTO ""AiModels""
    (""Id"", ""AiProviderId"", ""Name"", ""IsEnabled"", ""ExtraHeadersJson"", ""CreatedAt"", ""UpdatedAt"")
SELECT '2d0f042e-75d7-496b-a1c1-922a7a1b246b',
       '83f1b5f6-6314-4f5d-817e-36bd447d3f97',
       'embeddinggemma',
       TRUE,
       NULL,
       CURRENT_TIMESTAMP,
       CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM ""AiModels"" WHERE ""Id"" = '2d0f042e-75d7-496b-a1c1-922a7a1b246b'
);
");
            migrationBuilder.Sql(@"
UPDATE ""OrganizationAiKbSettings""
SET ""EmbeddingProviderId"" = '83f1b5f6-6314-4f5d-817e-36bd447d3f97',
    ""EmbeddingModel"" = 'embeddinggemma',
    ""EmbeddingDimensions"" = 768;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_embedding_org_hnsw;");
            migrationBuilder.Sql(@"TRUNCATE TABLE ""KnowledgeEmbeddings"";");
            migrationBuilder.Sql(@"
ALTER TABLE ""KnowledgeEmbeddings""
ALTER COLUMN ""Vector"" TYPE vector(1536);
");
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_embedding_org_hnsw
ON ""KnowledgeEmbeddings""
USING hnsw (""Vector"" vector_l2_ops);
");
            migrationBuilder.Sql(@"
UPDATE ""OrganizationAiKbSettings""
SET ""EmbeddingProviderId"" = NULL
WHERE ""EmbeddingProviderId"" = '83f1b5f6-6314-4f5d-817e-36bd447d3f97';
");
            migrationBuilder.Sql(@"DELETE FROM ""AiModels"" WHERE ""Id"" = '2d0f042e-75d7-496b-a1c1-922a7a1b246b';");
            migrationBuilder.Sql(@"DELETE FROM ""AiProviders"" WHERE ""Id"" = '83f1b5f6-6314-4f5d-817e-36bd447d3f97';");
        }
    }
}
