using System.Text.Json;

namespace RemoteDesktop.Host.Services;

public sealed class FileTransferTraceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _logPath;

    public FileTransferTraceService()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        _logPath = Path.Combine(logDirectory, "host-file-transfer.ndjson");
    }

    public string LogPath => _logPath;

    public async Task WriteAsync(string eventName, string message, object? data = null, CancellationToken cancellationToken = default)
    {
        var entry = new
        {
            occurredAt = DateTimeOffset.Now,
            eventName,
            message,
            data
        };

        var json = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_logPath, json, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
