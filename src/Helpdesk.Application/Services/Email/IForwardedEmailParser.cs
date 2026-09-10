namespace Helpdesk.Application.Services.Email;

public interface IForwardedEmailParser
{
    ForwardedEmailParseResult Parse(string? htmlBody, string? textBody);
}
