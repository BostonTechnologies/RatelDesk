using System.Runtime.CompilerServices;

namespace Helpdesk.Tests;

internal static class TestEnvironment
{
    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    [ModuleInitializer]
    public static void Initialize()
    {
        // Match Program and DesignTimeDbContextFactory before any PostgreSQL model
        // is cached, including store tests that do not start the API host.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__HelpdeskDb",
            "Host=localhost;Database=helpdesk_tests;Username=helpdesk;Password=helpdesk");
        Environment.SetEnvironmentVariable("Helpdesk__SkipDatabaseStartup", "true");
        Environment.SetEnvironmentVariable("ExchangeEmail__ClientSecret", "test-client-secret");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Helpdesk.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException($"Could not locate Helpdesk.sln from {AppContext.BaseDirectory}.");
    }
}
