namespace Helpdesk.Application.WorkLogs;

public interface IImageLinkSigner
{
    string GenerateToken(string scope, string id, string filename, DateTimeOffset expires);

    bool ValidateToken(string token, string scope, string id, string filename);
}
