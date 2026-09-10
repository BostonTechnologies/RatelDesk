namespace Helpdesk.Application.Tickets;

public interface IPublicTicketLinkSigner
{
    string GenerateToken(string trackingId, string email, DateTimeOffset expires);

    bool ValidateToken(string token, string trackingId, string email);
}
