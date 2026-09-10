namespace Helpdesk.Application.Events;

public static class CorrelationConstants
{
    public const string HeaderName = "X-Correlation-Id";
    public const string HttpContextItemKey = "CorrelationId";
    public const string SmokeScenarioHeaderName = "X-Helpdesk-Smoke-Scenario";
    public const string SmokeScenarioHttpContextItemKey = "HelpdeskSmokeScenarioId";
}
