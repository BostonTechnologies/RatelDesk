using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Helpdesk.Infrastructure.Persistence;

#nullable disable

namespace Helpdesk.Infrastructure.Persistence.Migrations;

/// <summary>Removes the retired in-process provider, knowledge, vector, and review schema.</summary>
[DbContext(typeof(HelpdeskDbContext))]
[Migration("20260915120000_RemoveLegacyNativeAi")]
public partial class RemoveLegacyNativeAi : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "TicketAiFeedback" CASCADE;
            DROP TABLE IF EXISTS "TicketKnowledgeSuggestions" CASCADE;
            DROP TABLE IF EXISTS "TicketAiSuggestions" CASCADE;
            DROP TABLE IF EXISTS "KnowledgeEmbeddings" CASCADE;
            DROP TABLE IF EXISTS "KnowledgeBaseCategories" CASCADE;
            DROP TABLE IF EXISTS "KnowledgeBaseArticles" CASCADE;
            DROP TABLE IF EXISTS "AiModels" CASCADE;
            DROP TABLE IF EXISTS "AiProviders" CASCADE;
            DROP TABLE IF EXISTS "AiOperationAuditRecords" CASCADE;
            DROP TABLE IF EXISTS "OrganizationAiKbSettings" CASCADE;
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiUnderstanding";
            ALTER TABLE "Organizations" DROP COLUMN IF EXISTS "EnableAiIntake";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "SourceKnowledgeArticleId";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewStatus";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewGateState";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewOutputJson";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewCorrelationId";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewFailureReason";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewRequestedAt";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewCompletedAt";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewAcknowledgedAt";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewAcknowledgedByUserId";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewAcknowledgedByName";
            ALTER TABLE "Tickets" DROP COLUMN IF EXISTS "AiReviewAcknowledgementNotes";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("The retired native AI schema cannot be restored automatically.");
}
