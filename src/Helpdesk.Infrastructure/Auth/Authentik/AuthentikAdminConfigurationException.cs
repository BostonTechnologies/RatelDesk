namespace Helpdesk.Infrastructure.Auth.Authentik;

public sealed class AuthentikAdminConfigurationException(string message) : InvalidOperationException(message);
