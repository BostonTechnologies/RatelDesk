using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Helpdesk.API.Ops;

public sealed class AiAgentOpsLogBuffer
{
    private const int Capacity = 1000;
    private long _nextId;
    private readonly ConcurrentQueue<AiAgentOpsLogRecord> _records = new();

    public void Add(LogLevel level, string category, EventId eventId, string message, Exception? exception)
    {
        var id = Interlocked.Increment(ref _nextId);
        _records.Enqueue(new AiAgentOpsLogRecord(
            id,
            DateTimeOffset.UtcNow,
            level.ToString(),
            category,
            eventId.Id,
            eventId.Name,
            AiAgentOpsRedactor.Redact(message),
            exception is null ? null : AiAgentOpsRedactor.Redact(exception.GetType().Name),
            exception is null ? null : AiAgentOpsRedactor.Redact(exception.Message),
            Activity.Current?.TraceId.ToString(),
            Activity.Current?.SpanId.ToString()));

        while (_records.Count > Capacity && _records.TryDequeue(out _))
        {
        }
    }

    public AiAgentOpsLogResponse Query(long? since, string? level, string? contains, string? correlationId, int? limit)
    {
        var effectiveLimit = Math.Clamp(limit ?? 100, 1, 500);
        var query = _records.ToArray().AsEnumerable();

        if (since is > 0)
        {
            query = query.Where(record => record.Id > since.Value);
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(record => string.Equals(record.Level, level, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(contains))
        {
            query = query.Where(record =>
                record.Message.Contains(contains, StringComparison.OrdinalIgnoreCase) ||
                (record.Category?.Contains(contains, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(record =>
                string.Equals(record.TraceId, correlationId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(record.SpanId, correlationId, StringComparison.OrdinalIgnoreCase));
        }

        var items = query
            .OrderBy(record => record.Id)
            .Take(effectiveLimit)
            .ToArray();

        return new AiAgentOpsLogResponse(
            items,
            items.LastOrDefault()?.Id,
            DateTimeOffset.UtcNow,
            true);
    }
}

public sealed class AiAgentOpsLoggerProvider(AiAgentOpsLogBuffer buffer) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new AiAgentOpsLogger(buffer, categoryName);

    public void Dispose()
    {
    }
}

internal sealed class AiAgentOpsLogger(AiAgentOpsLogBuffer buffer, string categoryName) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        buffer.Add(logLevel, categoryName, eventId, formatter(state, exception), exception);
    }
}

public static partial class AiAgentOpsRedactor
{
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = BearerRegex().Replace(value, "$1[REDACTED]");
        redacted = CookieRegex().Replace(redacted, "$1[REDACTED]");
        redacted = SecretRegex().Replace(redacted, "$1=[REDACTED]");
        redacted = ConnectionStringRegex().Replace(redacted, "$1=[REDACTED]");
        return redacted;
    }

    [GeneratedRegex(@"(?i)(bearer\s+)[A-Za-z0-9._~+/\-]+=*")]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"(?i)(cookie:\s*)[^\r\n]+")]
    private static partial Regex CookieRegex();

    [GeneratedRegex(@"(?i)\b(password|passwd|pwd|secret|client_secret|app_password|token|access_token|refresh_token|api[_-]?key)\s*=\s*[^;\s,}]+")]
    private static partial Regex SecretRegex();

    [GeneratedRegex(@"(?i)\b(Host|Username|User\s+Id|Password|Database)\s*=\s*[^;]+")]
    private static partial Regex ConnectionStringRegex();
}

public sealed record AiAgentOpsLogRecord(
    long Id,
    DateTimeOffset TimestampUtc,
    string Level,
    string Category,
    int EventId,
    string? EventName,
    string Message,
    string? ExceptionType,
    string? ExceptionMessage,
    string? TraceId,
    string? SpanId);

public sealed record AiAgentOpsLogResponse(
    IReadOnlyList<AiAgentOpsLogRecord> Items,
    long? NextSince,
    DateTimeOffset ServerTimeUtc,
    bool Redacted);
