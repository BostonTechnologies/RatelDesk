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
    private static Command BuildRawCommand(CliRuntime runtime, GlobalOptions globals)
    {
        var raw = new Command("raw", "Call an operator-safe API path directly.");
        var method = new Argument<string>("method") { Description = "HTTP method: get, post, put, delete." };
        var path = new Option<string>("--path") { Description = "Operator-safe /api/v1 path.", Required = true };
        var body = new Option<string?>("--body") { Description = "JSON request body." };
        var bodyFile = new Option<string?>("--body-file") { Description = "Path to JSON request body file." };
        raw.AddArgument(method);
        raw.AddOption(path);
        raw.AddOption(body);
        raw.AddOption(bodyFile);
        raw.SetHandler(ctx =>
        {
            var rawPath = ctx.ParseResult.GetValueForOption(path);
            EnsureAllowedRawPath(rawPath!);
            return SendAsync(
                runtime,
                globals,
                ctx,
                ResolveMethod(RequiredArgument(ctx.ParseResult, method)),
                rawPath!,
                ReadBody(ctx.ParseResult.GetValueForOption(body), ctx.ParseResult.GetValueForOption(bodyFile), "{}"));
        });
        return raw;
    }

    private static Command UnavailableCommand(string name, string message)
    {
        var command = new Command(name, message);
        command.SetHandler(() => throw new CliValidationException(message));
        return command;
    }

    private static Command GetListCommand(CliRuntime runtime, GlobalOptions globals, string name, string path, params (Option option, string queryName)[] query)
    {
        var command = new Command(name, $"GET {path}");
        foreach (var (option, _) in query) command.AddOption(option);
        command.SetHandler(ctx => SendAsync(runtime, globals, ctx, HttpMethod.Get, path + Query(ctx, query)));
        return command;
    }

    private static Command GetByIdCommand(CliRuntime runtime, GlobalOptions globals, string name, string template, bool intId = false)
    {
        var id = new Argument<string>("id");
        var command = new Command(name, $"GET {template}") { id };
        command.SetHandler(ctx => SendAsync(runtime, globals, ctx, HttpMethod.Get, Fill(template, ctx.ParseResult.GetValueForArgument(id))));
        return command;
    }

    private static Command GetByStringIdCommand(CliRuntime runtime, GlobalOptions globals, string name, string template, string argumentName)
    {
        var id = new Argument<string>(argumentName);
        var command = new Command(name, $"GET {template}") { id };
        command.SetHandler(ctx => SendAsync(runtime, globals, ctx, HttpMethod.Get, Fill(template, ctx.ParseResult.GetValueForArgument(id))));
        return command;
    }

    private static Command GetByIdentityCommand(CliRuntime runtime, GlobalOptions globals, string name, string template)
    {
        var identity = new Argument<string>("identity");
        var command = new Command(name, $"GET {template}") { identity };
        command.SetHandler(ctx => SendAsync(runtime, globals, ctx, HttpMethod.Get, template.Replace("{identity}", Escape(RequiredArgument(ctx.ParseResult, identity)), StringComparison.Ordinal)));
        return command;
    }

    private static Command DeleteByIdCommand(CliRuntime runtime, GlobalOptions globals, string name, string template, bool intId = false)
    {
        var id = new Argument<string>("id");
        var command = new Command(name, $"DELETE {template}") { id };
        command.SetHandler(ctx => SendAsync(runtime, globals, ctx, HttpMethod.Delete, Fill(template, ctx.ParseResult.GetValueForArgument(id))));
        return command;
    }

    private static Command DeleteByIdentityCommand(CliRuntime runtime, GlobalOptions globals, string name, string template)
    {
        var identity = new Argument<string>("identity");
        var command = new Command(name, $"DELETE {template}") { identity };
        command.SetHandler(ctx => SendAsync(runtime, globals, ctx, HttpMethod.Delete, template.Replace("{identity}", Escape(RequiredArgument(ctx.ParseResult, identity)), StringComparison.Ordinal)));
        return command;
    }

    private static Command BodyCommand(CliRuntime runtime, GlobalOptions globals, string name, HttpMethod method, string template, params (string optionName, string jsonName)[] bodyOptions)
    {
        var command = new Command(name, $"{method.Method} {template}");
        var idArg = template.Contains("{id}", StringComparison.Ordinal) ? new Argument<string>("id") : null;
        var identityArg = template.Contains("{identity}", StringComparison.Ordinal) ? new Argument<string>("identity") : null;
        if (idArg is not null) command.AddArgument(idArg);
        if (identityArg is not null) command.AddArgument(identityArg);

        var body = new Option<string?>("--body") { Description = "JSON request body. Overrides field flags." };
        var bodyFile = new Option<string?>("--body-file") { Description = "Path to JSON request body file. Overrides field flags." };
        command.AddOption(body);
        command.AddOption(bodyFile);
        var fields = new List<(Option<string?> option, string jsonName)>();
        foreach (var (optionName, jsonName) in bodyOptions)
        {
            var option = new Option<string?>(optionName) { Description = $"JSON field '{jsonName}'." };
            command.AddOption(option);
            fields.Add((option, jsonName));
        }

        command.SetHandler(ctx =>
        {
            var path = template;
            if (idArg is not null) path = path.Replace("{id}", Escape(RequiredArgument(ctx.ParseResult, idArg)), StringComparison.Ordinal);
            if (identityArg is not null) path = path.Replace("{identity}", Escape(RequiredArgument(ctx.ParseResult, identityArg)), StringComparison.Ordinal);
            var content = ReadBody(ctx.ParseResult.GetValueForOption(body), ctx.ParseResult.GetValueForOption(bodyFile), null)
                ?? BuildBodyFromOptions(ctx, fields);
            content = NormalizeKnownBodyEnums(content);
            return SendAsync(runtime, globals, ctx, method, path, content);
        });

        return command;
    }

    private static string RequiredArgument(CliParseResult parseResult, Argument<string> argument)
        => parseResult.GetValueForArgument(argument)
            ?? throw new CliValidationException($"Argument '{argument.Name}' is required.");

}
