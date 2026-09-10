namespace Helpdesk.Application.Services.AI;

public sealed class SecretReentryRequiredException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
