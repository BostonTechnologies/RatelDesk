namespace Helpdesk.Cli;

internal static class Program
{
    private static Task<int> Main(string[] args) => HelpdeskCli.RunAsync(args);
}
