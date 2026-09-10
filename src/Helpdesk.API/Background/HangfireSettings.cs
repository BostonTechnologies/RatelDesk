namespace Helpdesk.API.Background;

public class HangfireSettings
{
    public bool Enabled { get; set; } = false;
    public bool SlaEvaluationEnabled { get; set; } = true;
    public string ConnectionStringName { get; set; } = "HelpdeskDb";
    public string QueueName { get; set; } = "default";
    public string SlaEvaluationCron { get; set; } = "*/10 * * * *";
    public string TaskEscalationCron { get; set; } = "*/1 * * * *";
    public string TaskRetryCron { get; set; } = "*/1 * * * *";
    public string ApprovalTimeoutCron { get; set; } = "*/5 * * * *";
    public int BatchSize { get; set; } = 500;
}
