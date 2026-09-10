namespace Helpdesk.Shared.Services;

public record EmailIngestionMetrics(int Processed, int Rejected, int Skipped, int Errored);
