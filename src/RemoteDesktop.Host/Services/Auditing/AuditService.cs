using System.Text;
using System.Text.Json;
using RemoteDesktop.Host.Models;

namespace RemoteDesktop.Host.Services.Auditing;

public interface IAuditLogStore
{
    Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken);
}

public sealed class JsonAuditLogStore : IAuditLogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly string _auditFilePath;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public JsonAuditLogStore(IHostEnvironment environment)
    {
        _auditFilePath = Path.Combine(environment.ContentRootPath, "audit-log.ndjson");
    }

    public async Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_auditFilePath)!);
            await using var stream = new FileStream(_auditFilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await writer.WriteAsync(line.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return Array.Empty<AuditLogEntry>();
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_auditFilePath))
            {
                return Array.Empty<AuditLogEntry>();
            }

            var lines = await File.ReadAllLinesAsync(_auditFilePath, cancellationToken);
            var items = new List<AuditLogEntry>(Math.Min(lines.Length, take));
            for (var index = lines.Length - 1; index >= 0 && items.Count < take; index--)
            {
                var line = lines[index].Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize<AuditLogEntry>(line, JsonOptions);
                if (entry is not null)
                {
                    items.Add(entry);
                }
            }

            return items;
        }
        finally
        {
            _mutex.Release();
        }
    }
}

public interface IAuditService
{
    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken);

    Task WriteAsync(
        string action,
        string actorUserName,
        string actorDisplayName,
        string targetType,
        string? targetId,
        bool succeeded,
        string details,
        CancellationToken cancellationToken);

    Task WriteAsync(
        string action,
        AuthenticatedUserSession actor,
        string targetType,
        string? targetId,
        bool succeeded,
        string details,
        CancellationToken cancellationToken);
}

public sealed class AuditService : IAuditService
{
    private readonly IAuditLogStore _auditLogStore;

    public AuditService(IAuditLogStore auditLogStore)
    {
        _auditLogStore = auditLogStore;
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        return _auditLogStore.GetRecentAsync(take, cancellationToken);
    }

    public Task WriteAsync(
        string action,
        string actorUserName,
        string actorDisplayName,
        string targetType,
        string? targetId,
        bool succeeded,
        string details,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLogEntry
        {
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserName = Normalize(actorUserName, "system"),
            ActorDisplayName = Normalize(actorDisplayName, Normalize(actorUserName, "system")),
            Action = Normalize(action, "unknown"),
            TargetType = Normalize(targetType, "unknown"),
            TargetId = targetId?.Trim() ?? string.Empty,
            Succeeded = succeeded,
            Details = details?.Trim() ?? string.Empty
        };

        return _auditLogStore.AppendAsync(entry, cancellationToken);
    }

    public Task WriteAsync(
        string action,
        AuthenticatedUserSession actor,
        string targetType,
        string? targetId,
        bool succeeded,
        string details,
        CancellationToken cancellationToken)
    {
        return WriteAsync(action, actor.UserName, actor.DisplayName, targetType, targetId, succeeded, details, cancellationToken);
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
