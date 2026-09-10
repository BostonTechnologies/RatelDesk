namespace Helpdesk.Application.RequestTasks;

public sealed class AutomationBindingNotReadyException(string message) : InvalidOperationException(message);
