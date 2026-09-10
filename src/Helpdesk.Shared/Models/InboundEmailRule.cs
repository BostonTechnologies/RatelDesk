using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class InboundEmailRule
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public InboundEmailRuleScopeType ScopeType { get; set; } = InboundEmailRuleScopeType.Global;
    public string? TenantId { get; set; }
    public Guid? MailboxId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public int Priority { get; set; } = 100;
    public string ConditionsJson { get; set; } = "[]";
    public string ActionsJson { get; set; } = "[]";
    public bool StopProcessing { get; set; } = true;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
