using System;
using System.Linq;
using System.Threading.Tasks;

namespace Helpdesk.Application.Services.Tickets;

public class TicketRefGeneratorService : ITicketRefGeneratorService
{
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private static readonly Random Random = new();

    public Task<string> NextReferenceAsync(string prefix)
    {
        var part1 = GeneratePart(3);
        var part2 = GeneratePart(3);
        var part3 = GeneratePart(3);
        var reference = $"{prefix.ToUpper()}-{part1}-{part2}-{part3}";
        return Task.FromResult(reference);
    }

    private static string GeneratePart(int length)
    {
        return new string(Enumerable.Repeat(Chars, length)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }
}
