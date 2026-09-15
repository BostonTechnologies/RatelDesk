using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Helpdesk.Infrastructure.Persistence;

#nullable disable

namespace Helpdesk.Infrastructure.SqliteMigrations.Migrations.Helpdesk;

[DbContext(typeof(HelpdeskDbContext))]
[Migration("20260915120000_RemoveLegacyNativeAi")]
public partial class RemoveLegacyNativeAi : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "TicketAiFeedback";
            DROP TABLE IF EXISTS "TicketKnowledgeSuggestions";
            DROP TABLE IF EXISTS "TicketAiSuggestions";
            DROP TABLE IF EXISTS "KnowledgeEmbeddings";
            DROP TABLE IF EXISTS "KnowledgeBaseCategories";
            DROP TABLE IF EXISTS "KnowledgeBaseArticles";
            DROP TABLE IF EXISTS "AiModels";
            DROP TABLE IF EXISTS "AiProviders";
            DROP TABLE IF EXISTS "AiOperationAuditRecords";
            DROP TABLE IF EXISTS "OrganizationAiKbSettings";
            DROP INDEX IF EXISTS "IX_Tickets_OrganizationId_SourceKnowledgeArticleId";
            ALTER TABLE "Tickets" DROP COLUMN "AiUnderstanding";
            ALTER TABLE "Organizations" DROP COLUMN "EnableAiIntake";
            ALTER TABLE "Tickets" DROP COLUMN "SourceKnowledgeArticleId";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewStatus";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewGateState";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewOutputJson";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewCorrelationId";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewFailureReason";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewRequestedAt";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewCompletedAt";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewAcknowledgedAt";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewAcknowledgedByUserId";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewAcknowledgedByName";
            ALTER TABLE "Tickets" DROP COLUMN "AiReviewAcknowledgementNotes";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("The retired native AI schema cannot be restored automatically.");
}
