namespace Helpdesk.Application.Services.AI;

public class EmbeddingUnavailableException : Exception
{
    public EmbeddingUnavailableException(string message) : base(message) { }
}
