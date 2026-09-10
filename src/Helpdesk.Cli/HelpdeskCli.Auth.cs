using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Helpdesk.Shared.DTOs.Notification;
using Helpdesk.Shared.DTOs.Service;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

internal static partial class HelpdeskCli
{
    private static Command BuildAuthCommand(CliRuntime runtime, GlobalOptions globals)
    {
        var auth = new Command("auth", "Configure and verify Authentik AI-agent authentication.");

        var configure = new Command("configure", "Persist local CLI authentication settings.");
        configure.SetHandler(async ctx =>
        {
            var config = LoadConfig(ctx, globals, requireAuth: false);
            var store = new CliConfigStore(ctx.ParseResult.GetValueForOption(globals.ConfigPath));
            store.Save(config.File.Merge(config.Overrides));
            await WriteJsonAsync(runtime, new { saved = true, path = store.Path }, ctx, globals).ConfigureAwait(false);
        });
        auth.AddCommand(configure);

        var status = new Command("status", "Call the Helpdesk API AI-agent status endpoint.");
        status.SetHandler(ctx => SendAsync(runtime, globals, ctx, HttpMethod.Get, "/api/v1/auth/ai-agent/status"));
        auth.AddCommand(status);

        var token = new Command("token", "Mint and print an Authentik AI-agent bearer token.");
        token.SetHandler(async ctx =>
        {
            var resolved = LoadConfig(ctx, globals).Resolved;
            var accessToken = await runtime.GetAccessTokenAsync(resolved!, ctx.GetCancellationToken()).ConfigureAwait(false);
            if (!ctx.ParseResult.GetValueForOption(globals.Quiet))
            {
                await runtime.Out.WriteLineAsync(accessToken).ConfigureAwait(false);
            }
        });
        auth.AddCommand(token);

        return auth;
    }

    private static Command BuildConfigCommand(CliRuntime runtime, GlobalOptions globals)
    {
        var config = new Command("config", "Read and edit local Helpdesk CLI config.");
        var keyArg = new Argument<string>("key");
        var valueArg = new Argument<string>("value");

        var show = new Command("show", "Show merged CLI config with secrets redacted.");
        show.SetHandler(ctx =>
        {
            var loaded = LoadConfig(ctx, globals, requireAuth: false);
            return WriteJsonAsync(runtime, Redact(loaded.File.Merge(loaded.Overrides)), ctx, globals);
        });
        config.AddCommand(show);

        var get = new Command("get", "Read one persisted config value.") { keyArg };
        get.SetHandler(async ctx =>
        {
            var store = new CliConfigStore(ctx.ParseResult.GetValueForOption(globals.ConfigPath));
            var value = GetConfigValue(store.Load(), ctx.ParseResult.GetValueForArgument(keyArg), redact: true);
            await WriteJsonAsync(runtime, new { key = ctx.ParseResult.GetValueForArgument(keyArg), value }, ctx, globals).ConfigureAwait(false);
        });
        config.AddCommand(get);

        var set = new Command("set", "Set one persisted config value.") { keyArg, valueArg };
        set.SetHandler(async ctx =>
        {
            var store = new CliConfigStore(ctx.ParseResult.GetValueForOption(globals.ConfigPath));
            var current = store.Load();
            var updated = SetConfigValue(current, ctx.ParseResult.GetValueForArgument(keyArg), ctx.ParseResult.GetValueForArgument(valueArg));
            store.Save(updated);
            await WriteJsonAsync(runtime, new { saved = true, path = store.Path }, ctx, globals).ConfigureAwait(false);
        });
        config.AddCommand(set);

        var unset = new Command("unset", "Unset one persisted config value.") { keyArg };
        unset.SetHandler(async ctx =>
        {
            var store = new CliConfigStore(ctx.ParseResult.GetValueForOption(globals.ConfigPath));
            var updated = SetConfigValue(store.Load(), ctx.ParseResult.GetValueForArgument(keyArg), null);
            store.Save(updated);
            await WriteJsonAsync(runtime, new { saved = true, path = store.Path }, ctx, globals).ConfigureAwait(false);
        });
        config.AddCommand(unset);

        return config;
    }

    private static Command BuildHealthCommand(CliRuntime runtime, GlobalOptions globals)
    {
        var command = new Command("health", "Check live, ready, and authenticated AI-agent status.");
        command.SetHandler(async ctx =>
        {
            var loaded = LoadConfig(ctx, globals);
            var live = await SendRawAsync(runtime, loaded.Resolved!, HttpMethod.Get, "/health/live", authenticated: false, null, ctx.GetCancellationToken()).ConfigureAwait(false);
            var ready = await SendRawAsync(runtime, loaded.Resolved!, HttpMethod.Get, "/health/ready", authenticated: false, null, ctx.GetCancellationToken()).ConfigureAwait(false);
            var auth = await SendRawAsync(runtime, loaded.Resolved!, HttpMethod.Get, "/api/v1/auth/ai-agent/status", authenticated: true, null, ctx.GetCancellationToken()).ConfigureAwait(false);
            await WriteJsonAsync(runtime, new[] { live.ToHealth("live"), ready.ToHealth("ready"), auth.ToHealth("auth") }, ctx, globals).ConfigureAwait(false);
        });
        return command;
    }

    private static Command BuildLogsCommand(CliRuntime runtime, GlobalOptions globals)
    {
        var logs = new Command("logs", "Search Helpdesk API AI-agent operation logs.");
        var search = new Command("search", "Search log entries.");
        var tail = new Command("tail", "Poll log entries repeatedly.");
        var since = new Option<long?>("--since") { Description = "Return logs after this sequence." };
        var level = new Option<string?>("--level") { Description = "Filter by log level." };
        var contains = new Option<string?>("--contains") { Description = "Filter by message text." };
        var correlationId = new Option<string?>("--correlation-id") { Description = "Filter by correlation id." };
        var limit = new Option<int?>("--limit") { Description = "Maximum entries." };
        foreach (var option in new Option[] { since, level, contains, correlationId, limit }) search.AddOption(option);
        search.SetHandler(ctx => SendAsync(runtime, globals, ctx, HttpMethod.Get, "/api/v1/ops/ai-agent/logs" + Query(ctx, (since, "since"), (level, "level"), (contains, "contains"), (correlationId, "correlationId"), (limit, "limit"))));
        logs.AddCommand(search);

        var iterations = new Option<int>("--iterations") { Description = "Poll count.", DefaultValueFactory = _ => 20 };
        var delay = new Option<int>("--delay-seconds") { Description = "Delay between polls.", DefaultValueFactory = _ => 3 };
        foreach (var option in new Option[] { since, iterations, delay }) tail.AddOption(option);
        tail.SetHandler(async ctx =>
        {
            long? cursor = ctx.ParseResult.GetValueForOption(since);
            var count = Math.Max(1, ctx.ParseResult.GetValueForOption(iterations));
            var pause = Math.Max(1, ctx.ParseResult.GetValueForOption(delay));
            for (var i = 0; i < count; i++)
            {
                var response = await SendStringAsync(runtime, globals, ctx, HttpMethod.Get, "/api/v1/ops/ai-agent/logs" + Query(("since", cursor?.ToString())), null).ConfigureAwait(false);
                if (!ctx.ParseResult.GetValueForOption(globals.Quiet))
                {
                    await runtime.Out.WriteLineAsync(response.Body).ConfigureAwait(false);
                }
                cursor = ExtractNextSince(response.Body) ?? cursor;
                if (i + 1 < count)
                {
                    await Task.Delay(TimeSpan.FromSeconds(pause), ctx.GetCancellationToken()).ConfigureAwait(false);
                }
            }
        });
        logs.AddCommand(tail);

        return logs;
    }

}
