using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;

internal sealed class InvocationContext
{
    internal InvocationContext(ParseResult parseResult, CancellationToken cancellationToken)
    {
        ParseResult = new CliParseResult(parseResult);
        CancellationToken = cancellationToken;
    }

    public CliParseResult ParseResult { get; }

    public int ExitCode { get; set; }

    public CancellationToken GetCancellationToken() => CancellationToken;

    private CancellationToken CancellationToken { get; }
}

internal sealed class CliParseResult
{
    private static readonly MethodInfo GetValueMethod = typeof(ParseResult)
        .GetMethods()
        .Single(method => method.Name == nameof(ParseResult.GetValue)
            && method.IsGenericMethodDefinition
            && method.GetParameters() is [{ ParameterType: var parameterType }]
            && parameterType.IsGenericType
            && parameterType.GetGenericTypeDefinition() == typeof(Option<>));

    private readonly ParseResult parseResult;

    internal CliParseResult(ParseResult parseResult) => this.parseResult = parseResult;

    public T? GetValueForArgument<T>(Argument<T> argument) => parseResult.GetValue(argument);

    public T? GetValueForOption<T>(Option<T> option) => parseResult.GetValue(option);

    public object? GetValueForOption(Option option)
    {
        var method = GetValueMethod.MakeGenericMethod(option.ValueType);
        return method.Invoke(parseResult, [option]);
    }

    public bool WasSpecified(Option option)
        => (parseResult.GetResult(option) as OptionResult)?.IdentifierTokenCount > 0;
}

internal static class CommandCompatibilityExtensions
{
    public static void AddCommand(this Command command, Command subcommand)
        => command.Subcommands.Add(subcommand);

    public static void AddArgument(this Command command, Argument argument)
        => command.Arguments.Add(argument);

    public static void AddOption(this Command command, Option option)
        => command.Options.Add(option);

    public static void AddGlobalOption(this Command command, Option option)
    {
        option.Recursive = true;
        command.Options.Add(option);
    }

    public static void SetHandler(this Command command, Func<InvocationContext, Task> handler)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var context = new InvocationContext(parseResult, cancellationToken);
            await handler(context).ConfigureAwait(false);
            return context.ExitCode;
        });
    }

    public static void SetHandler(this Command command, Action handler)
        => command.SetAction(_ => handler());
}
