using Helpdesk.Application.Services.KB;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Shared.DTOs.Article;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Helpdesk.API.Background;

public sealed class AiSuggestionWorker : BackgroundService
{
    private readonly IAiSuggestionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiSuggestionWorker> _logger;

    public AiSuggestionWorker(IAiSuggestionQueue queue, IServiceScopeFactory scopeFactory, ILogger<AiSuggestionWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var ticketId in _queue.DequeueAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
                var sugg = scope.ServiceProvider.GetRequiredService<KnowledgeSuggestionService>();
                var retrieval = scope.ServiceProvider.GetRequiredService<IKnowledgeRetrievalService>();

                var ticket = await db.Tickets.OfType<Incident>().FirstOrDefaultAsync(t => t.Id == ticketId, stoppingToken);
                if (ticket is null) continue;

                var orgSettings = await db.OrganizationAiKbSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.OrganizationId == ticket.OrganizationId, stoppingToken);

                if (orgSettings is null || orgSettings.EnableAiSearch == false)
                {
                    _logger.LogInformation("AI search disabled for org {Org}. Skipping suggestions for {Ticket}", ticket.OrganizationId, ticketId);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(ticket.OrganizationId))
                {
                    _logger.LogInformation("Ticket {TicketId} has no organization. Skipping AI suggestions.", ticketId);
                    continue;
                }

                var retrievalResults = await retrieval.RetrieveAsync(ticket.OrganizationId, ticket.Description, 5, stoppingToken);
                await sugg.SuggestAsync(ticket, stoppingToken);
                var items = retrievalResults
                    .Select(result => new KnowledgeSuggestion(
                        result.Article.Id.ToString(),
                        result.Article.Title,
                        result.Evidence.FirstOrDefault()?.Text ?? result.Article.Summary ?? string.Empty,
                        $"/kb/{result.Article.Id}",
                        result.Score,
                        result.ConfidenceLabel,
                        result.IsHighConfidence,
                        !result.IsHighConfidence,
                        result.IsHighConfidence
                            ? null
                            : "Ask the requester to confirm the affected service, exact error text, and when the issue started before applying this guidance.",
                        result.Evidence
                            .Select(evidence => new KnowledgeSuggestionEvidence(
                                evidence.ChunkId,
                                evidence.SourceId,
                                evidence.Text,
                                evidence.Score))
                            .ToList(),
                        string.IsNullOrWhiteSpace(result.Article.AutomationBindingId)
                            ? null
                            : new KnowledgeArticleAutomationTargetDto
                            {
                                BindingId = result.Article.AutomationBindingId,
                                RequestFormId = result.Article.AutomationRequestFormId,
                                TaskTemplateId = result.Article.AutomationTaskTemplateId,
                                TaskTemplateName = result.Article.AutomationTaskTemplateName,
                                OrchestrationRequestDefinitionId = result.Article.AutomationOrchestrationRequestDefinitionId,
                                OrchestrationJobDefinitionId = result.Article.AutomationOrchestrationJobDefinitionId,
                                Enabled = true
                            }))
                    .ToList();

                var row = await db.TicketAiSuggestions.FindAsync(new object?[] { ticketId }, cancellationToken: stoppingToken);
                if (row is null)
                {
                    row = new TicketAiSuggestions { TicketId = ticketId };
                    db.TicketAiSuggestions.Add(row);
                }

                row.CreatedAt = DateTimeOffset.UtcNow;
                row.ItemsJson = System.Text.Json.JsonSerializer.Serialize(items);

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI suggestion job failed for ticket {TicketId}", ticketId);
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IErrorLogRepository>();
                    await repo.SaveAsync(new ErrorLog { Message = ex.ToString(), Page = "ai-suggestions", Timestamp = DateTime.UtcNow });
                }
                catch { }
            }
        }
    }
}
