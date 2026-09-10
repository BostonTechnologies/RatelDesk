namespace Helpdesk.Infrastructure.Configuration;

public class EmailIngestionOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
}
