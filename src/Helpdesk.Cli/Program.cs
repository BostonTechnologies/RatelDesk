namespace Helpdesk.Cli;

internal static class Program
{
    private static Task<int> Main(string[] args)
    {
        if (args is ["--version"])
        {
            Console.Out.WriteLine(BuildIdentity());
            return Task.FromResult(0);
        }
        return HelpdeskCli.RunAsync(args);
    }

    private static string BuildIdentity() => $"rateldesk {typeof(Program).Assembly.GetName().Version} ({typeof(Program).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().SingleOrDefault()?.InformationalVersion ?? "unknown"})";
}
