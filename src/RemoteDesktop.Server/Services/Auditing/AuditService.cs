using System.Text;
using System.Text.Json;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services.Auditing;

public interface IAuditLogStore
{
    Task AppendAsync(AuditLogEntryDto entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditLogEntryDto>> GetRecentAsync(int take, CancellationToken cancellationToken);
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

    public async Task AppendAsync(AuditLogEntryDto entry, CancellationToken cancellationToken)
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

    public async Task<IReadOnlyList<AuditLogEntryDto>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return Array.Empty<AuditLogEntryDto>();
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_auditFilePath))
            {
                return Array.Empty<AuditLogEntryDto>();
            }

            var lines = await File.ReadAllLinesAsync(_auditFilePath, cancellationToken);
            var items = new List<AuditLogEntryDto>(Math.Min(lines.Length, take));
            for (var index = lines.Length - 1; index >= 0 && items.Count < take; index--)
            {
                var line = lines[index].Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize<AuditLogEntryDto>(line, JsonOptions);
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

public sealed class AuditService
{
    private readonly IAuditLogStore _auditLogStore;

    public AuditService(IAuditLogStore auditLogStore)
    {
        _auditLogStore = auditLogStore;
    }

    public Task<IReadOnlyList<AuditLogEntryDto>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        return _auditLogStore.GetRecentAsync(take, cancellationToken);
    }

    public Task WriteAsync(AuditLogEntryDto entry, CancellationToken cancellationToken)
    {
        var normalized = new AuditLogEntryDto
        {
            Id = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id,
            OccurredAt = entry.OccurredAt == default ? DateTimeOffset.UtcNow : entry.OccurredAt,
            ActorUserName = Normalize(entry.ActorUserName, "system"),
            ActorDisplayName = Normalize(entry.ActorDisplayName, Normalize(entry.ActorUserName, "system")),
            Action = Normalize(entry.Action, "unknown"),
            TargetType = Normalize(entry.TargetType, "unknown"),
            TargetId = entry.TargetId?.Trim() ?? string.Empty,
            Succeeded = entry.Succeeded,
            Details = entry.Details?.Trim() ?? string.Empty
        };

        return _auditLogStore.AppendAsync(normalized, cancellationToken);
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
