using System.Threading;
using Archive.Core.Domain.Entities;
using Archive.Core.Jobs;
using Archive.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace Archive.Desktop.Logging;

public sealed class DatabaseLogSink : ILogEventSink
{
    private const int PruneEveryEvents = 100;
    private static readonly AsyncLocal<bool> IsEmitting = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private int _eventsSincePrune;
    private int _pruneInProgress;

    public DatabaseLogSink(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void Emit(LogEvent logEvent)
    {
        if (ShouldSkip(logEvent))
        {
            return;
        }

        if (IsEmitting.Value)
        {
            return;
        }

        try
        {
            IsEmitting.Value = true;

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();

            dbContext.ApplicationLogs.Add(new ApplicationLog
            {
                TimestampUtc = logEvent.Timestamp.UtcDateTime,
                Level = logEvent.Level.ToString(),
                Message = logEvent.RenderMessage(),
                Exception = logEvent.Exception?.ToString(),
                SourceContext = TryGetSourceContext(logEvent)
            });

            dbContext.SaveChanges();

            var shouldPrune = Interlocked.Increment(ref _eventsSincePrune) >= PruneEveryEvents;
            if (!shouldPrune)
            {
                return;
            }

            if (Interlocked.Exchange(ref _pruneInProgress, 1) == 1)
            {
                return;
            }

            try
            {
                var retentionService = scope.ServiceProvider.GetRequiredService<IExecutionLogRetentionService>();
                retentionService.PruneAsync().GetAwaiter().GetResult();
                Interlocked.Exchange(ref _eventsSincePrune, 0);
            }
            finally
            {
                Interlocked.Exchange(ref _pruneInProgress, 0);
            }
        }
        catch
        {
        }
        finally
        {
            IsEmitting.Value = false;
        }
    }

    private static bool ShouldSkip(LogEvent logEvent)
    {
        var sourceContext = TryGetSourceContext(logEvent);
        if (string.IsNullOrWhiteSpace(sourceContext))
        {
            return false;
        }

        return sourceContext.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
               || sourceContext.StartsWith("Archive.Desktop.Logging.DatabaseLogSink", StringComparison.Ordinal)
               || sourceContext.StartsWith("Serilog", StringComparison.Ordinal);
    }

    private static string? TryGetSourceContext(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var value))
        {
            return null;
        }

        if (value is ScalarValue scalar && scalar.Value is string text)
        {
            return text;
        }

        return value.ToString().Trim('"');
    }
}