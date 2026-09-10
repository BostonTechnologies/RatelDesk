namespace Helpdesk.Shared.Models;

public class TenantSlaSettings
{
    public string TenantId { get; set; } = string.Empty;
    public bool UseBusinessHours { get; set; }
    public string? CalendarId { get; set; }
}
