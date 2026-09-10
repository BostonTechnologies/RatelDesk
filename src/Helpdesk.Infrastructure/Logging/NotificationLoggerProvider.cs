using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading;
using Helpdesk.Application.Notifications;
using Helpdesk.Shared.DTOs.Notification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.Logging;

public sealed class NotificationLoggerProvider : ILoggerProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _environmentName;
    private readonly string _serviceName;
    private readonly string _assemblyName;
    private readonly ConcurrentDictionary<string, NotificationLogger> _loggers = new();
    private readonly Channel<NotificationLogEntry> _queue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private int _disposed;

    public NotificationLoggerProvider(
        IServiceScopeFactory scopeFactory,
        string environmentName,
        string serviceName,
        string assemblyName)
    {
        _scopeFactory = scopeFactory;
        _environmentName = environmentName;
        _serviceName = serviceName;
        _assemblyName = assemblyName;

        _queue = Channel.CreateBounded<NotificationLogEntry>(new BoundedChannelOptions(2048)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        Console.WriteLine("NotificationLoggerProvider background worker started");
        _worker = Task.Run(ProcessQueueAsync);
    }

    public ILogger CreateLogger(string categoryName)
    {
        Console.WriteLine($"NotificationLoggerProvider.CreateLogger: {categoryName}");
        return _loggers.GetOrAdd(categoryName, name => new NotificationLogger(name, Enqueue));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        try
        {
            _cts.Cancel();
            _queue.Writer.TryComplete();
            _worker.GetAwaiter().GetResult();
        }
        catch
        {
            // Never throw during logger provider disposal.
        }
        finally
        {
            _cts.Dispose();
        }
    }

    private void Enqueue(NotificationLogEntry entry)
    {
        if (!ShouldCreateNotification(entry))
            return;

        try
        {
            _queue.Writer.TryWrite(entry);
        }
        catch
        {
            // Swallow to keep logging pipeline safe.
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var entry in _queue.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    var request = new CreateNotificationRequest
                    {
                        Title = BuildTitle(entry.Category),
                        Message = $"[{entry.TimestampUtc:O}] {entry.Message}",
                        Severity = MapSeverity(entry.Level),
                        Source = BuildSource(entry.Category),
                        Category = "System"
                    };

                    await notificationService.CreateNotificationAsync(request, _cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"NotificationLoggerProvider worker error: {ex}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"NotificationLoggerProvider loop terminated: {ex}");
        }
    }

    private string BuildSource(string category)
    {
        return $"{category} | Assembly={_assemblyName} | Env={_environmentName} | Service={_serviceName}";
    }

    private static string BuildTitle(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "System";

        var trimmed = category.StartsWith("Helpdesk.", StringComparison.Ordinal)
            ? category["Helpdesk.".Length..]
            : category;

        var idx = trimmed.LastIndexOf('.');
        return idx >= 0 ? trimmed[(idx + 1)..] : trimmed;
    }

    private static NotificationSeverity MapSeverity(LogLevel level) => level switch
    {
        LogLevel.Warning => NotificationSeverity.Warning,
        LogLevel.Error => NotificationSeverity.Error,
        LogLevel.Critical => NotificationSeverity.Critical,
        _ => NotificationSeverity.Info
    };

    private static bool ShouldCreateNotification(NotificationLogEntry entry)
    {
        if (entry.Level < LogLevel.Warning)
            return false;

        if (entry.Level == LogLevel.Warning &&
            string.Equals(entry.Category, "IncidentPostProcess", StringComparison.Ordinal) &&
            entry.Message.Contains("Optional incident post-processing skipped", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    internal readonly record struct NotificationLogEntry(LogLevel Level, string Category, string Message, DateTime TimestampUtc);
}
