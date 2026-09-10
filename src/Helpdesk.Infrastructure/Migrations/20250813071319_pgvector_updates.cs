using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class pgvector_updates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "LastScore",
                table: "KnowledgeEmbeddings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_embedding_org_hnsw
                ON ""KnowledgeEmbeddings""
                USING hnsw (""Vector"" vector_l2_ops);  -- or vector_cosine_ops / vector_ip_ops
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "LastScore",
                table: "KnowledgeEmbeddings",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldDefaultValue: 0.0);

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_embedding_org_hnsw;");
        }
    }
}
