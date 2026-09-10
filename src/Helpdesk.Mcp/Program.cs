using Helpdesk.AgentClient;
using Helpdesk.Mcp;
using Helpdesk.Mcp.Configuration;
using Helpdesk.Mcp.Prompts;
using Helpdesk.Mcp.Resources;
using Helpdesk.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Mcp;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var configurationPath = Environment.GetEnvironmentVariable(HelpdeskMcpTarget.ConfigurationEnvironmentVariable);
            var configurationStore = new AgentClientConfigurationStore(configurationPath);
            var configuration = AgentClientConfigurationResolver.LoadIsolated(configurationPath);
            var target = HelpdeskMcpTarget.Resolve(configuration, configurationPath);
            var configurationSurface = new AgentClientConfigurationSurface(configurationStore);

            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

            builder.Services.AddHelpdeskMcpCore(configuration, configurationSurface, target, "stdio");
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithTools(HelpdeskToolDefinitions.Create())
                .WithResourcesFromAssembly(typeof(HelpdeskResources).Assembly)
                .WithPromptsFromAssembly(typeof(HelpdeskPrompts).Assembly);

            var host = builder.Build();
            host.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Helpdesk.Mcp.Target")
                .LogInformation("Helpdesk MCP target validated for {Instance} at {ApiBaseUrl}", target.Instance, target.CanonicalApiBaseUrl);
            await host.RunAsync();
            return 0;
        }
        catch (AgentClientValidationException exception)
        {
            await Console.Error.WriteLineAsync($"Helpdesk MCP startup validation failed: {exception.Message}");
            return 78;
        }
    }
}
