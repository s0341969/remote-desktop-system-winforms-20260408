using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteDesktop.Agent.Options;
using RemoteDesktop.Agent.Services;
using RemoteDesktop.Host.Hosting;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;
using RemoteDesktop.Host.Services.Auditing;

await RunHostRelaySmokeTestAsync();
await RunFileTransferSmokeTestAsync();
await RunClipboardSmokeTestAsync();
Console.WriteLine("SMOKE_TEST_PASSED");

static async Task RunHostRelaySmokeTestAsync()
{
    var port = GetFreeTcpPort();
    var serverUrl = $"http://127.0.0.1:{port}";
    var accessKey = "SmokeTest-Agent-Key-2026";
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseSetting("urls", serverUrl);

    builder.Services.AddSingleton<IOptions<ControlServerOptions>>(Options.Create(new ControlServerOptions
    {
        ServerUrl = serverUrl,
        ConsoleName = "Smoke Test Console",
        AdminUserName = "admin",
        AdminPassword = "ChangeMe!2026",
        SharedAccessKey = accessKey,
        AgentHeartbeatTimeoutSeconds = 45
    }));

    builder.Services.AddSingleton<IDeviceRepository, InMemoryDeviceRepository>();
    builder.Services.AddRemoteDesktopHostCore();

    await using var app = builder.Build();
    app.MapRemoteDesktopHostEndpoints();
    await app.StartAsync();

    try
    {
        var agentUri = new UriBuilder(serverUrl) { Scheme = "ws", Path = "/ws/agent" }.Uri;
        using var agentSocket = new ClientWebSocket();
        await agentSocket.ConnectAsync(agentUri, CancellationToken.None);

        var helloPayload = JsonSerializer.Serialize(new RemoteDesktop.Host.Models.AgentHelloMessage
        {
            Type = "hello",
            DeviceId = "smoke-device-001",
            DeviceName = "Smoke Device",
            HostName = Environment.MachineName,
            AgentVersion = "smoke",
            AccessKey = accessKey,
            ScreenWidth = 1920,
            ScreenHeight = 1080
        });

        await SendTextAsync(agentSocket, helloPayload, CancellationToken.None);
        var ack = await ReadMessageAsync(agentSocket, CancellationToken.None);
        var ackJson = Encoding.UTF8.GetString(ack.Payload);
        if (!ackJson.Contains("\"hello-ack\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Host did not acknowledge the agent hello message.");
        }

        var broker = app.Services.GetRequiredService<DeviceBroker>();
        var authorizationUpdated = await broker.SetDeviceAuthorizationAsync("smoke-device-001", true, "smoke-admin", CancellationToken.None);
        if (!authorizationUpdated)
        {
            throw new InvalidOperationException("Smoke device could not be approved for unattended access.");
        }

        var viewerFrameReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewerAttached = await broker.AttachViewerAsync(
            "smoke-device-001",
            "smoke-viewer",
            (payload, cancellationToken) =>
            {
                viewerFrameReceived.TrySetResult(payload);
                return Task.CompletedTask;
            },
            null,
            null,
            CancellationToken.None);

        if (!viewerAttached)
        {
            throw new InvalidOperationException("Viewer could not attach to the smoke agent.");
        }

        var sampleFrame = Encoding.UTF8.GetBytes("frame-payload");
        await agentSocket.SendAsync(sampleFrame, WebSocketMessageType.Binary, true, CancellationToken.None);
        var publishedFrame = await viewerFrameReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (!publishedFrame.SequenceEqual(sampleFrame))
        {
            throw new InvalidOperationException("Viewer did not receive the expected frame payload.");
        }

        var receiveCommandTask = ReadMessageAsync(agentSocket, CancellationToken.None);
        await broker.ForwardViewerCommandAsync(
            "smoke-device-001",
            new RemoteDesktop.Host.Models.ViewerCommandMessage
            {
                Type = "move",
                X = 0.25,
                Y = 0.75
            },
            CancellationToken.None);

        var commandMessage = await receiveCommandTask.WaitAsync(TimeSpan.FromSeconds(5));
        var commandJson = Encoding.UTF8.GetString(commandMessage.Payload);
        if (!commandJson.Contains("\"type\":\"move\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Agent did not receive the expected viewer command.");
        }

        if (agentSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await agentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "smoke-test-complete", CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
        }
    }
    finally
    {
        await app.StopAsync();
    }
}

static async Task RunFileTransferSmokeTestAsync()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "RemoteDesktopSmoke", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var loggerFactory = LoggerFactory.Create(static builder => { });
        var options = Options.Create(new AgentOptions
        {
            ServerUrl = "http://localhost:5106",
            DeviceId = "smoke-agent",
            DeviceName = "Smoke Agent",
            SharedAccessKey = "SmokeTest-Agent-Key-2026",
            FileTransferDirectory = tempRoot,
            CaptureFramesPerSecond = 8,
            JpegQuality = 55,
            MaxFrameWidth = 1600,
            ReconnectDelaySeconds = 5
        });

        var fileTransferTraceService = new RemoteDesktop.Agent.Services.FileTransferTraceService();
        var fileTransferService = new FileTransferService(options, loggerFactory.CreateLogger<FileTransferService>(), fileTransferTraceService);
        var statuses = new List<RemoteDesktop.Agent.Models.AgentFileTransferStatusMessage>();
        Func<RemoteDesktop.Agent.Models.AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync = (status, cancellationToken) =>
        {
            statuses.Add(status);
            return Task.CompletedTask;
        };

        var repeatedContent = Enumerable.Repeat("smoke-file-transfer-payload-", 24_000);
        var content = Encoding.UTF8.GetBytes(string.Concat(repeatedContent));
        var uploadId = Guid.NewGuid().ToString("N");

        await fileTransferService.TryHandleAsync(new RemoteDesktop.Agent.Models.ViewerCommandMessage
        {
            Type = "file-upload-start",
            UploadId = uploadId,
            FileName = "smoke.txt",
            FileSize = content.Length
        }, publishStatusAsync, CancellationToken.None);

        const int chunkBytes = 16 * 1024;
        var offset = 0;
        var sequenceNumber = 0;
        while (offset < content.Length)
        {
            var bytesToSend = Math.Min(chunkBytes, content.Length - offset);
            var chunk = new byte[bytesToSend];
            Buffer.BlockCopy(content, offset, chunk, 0, bytesToSend);
            offset += bytesToSend;

            await fileTransferService.TryHandleAsync(new RemoteDesktop.Agent.Models.ViewerCommandMessage
            {
                Type = "file-upload-chunk",
                UploadId = uploadId,
                FileName = "smoke.txt",
                FileSize = content.Length,
                SequenceNumber = sequenceNumber++,
                ChunkBase64 = Convert.ToBase64String(chunk)
            }, publishStatusAsync, CancellationToken.None);
        }

        await fileTransferService.TryHandleAsync(new RemoteDesktop.Agent.Models.ViewerCommandMessage
        {
            Type = "file-upload-complete",
            UploadId = uploadId,
            FileName = "smoke.txt",
            FileSize = content.Length
        }, publishStatusAsync, CancellationToken.None);

        var completed = statuses.LastOrDefault(static item => string.Equals(item.Status, "completed", StringComparison.Ordinal));
        if (completed is null)
        {
            throw new InvalidOperationException("File transfer service did not emit a completion status.");
        }

        var storedPath = Path.Combine(tempRoot, completed.StoredFileName);
        if (!File.Exists(storedPath))
        {
            throw new InvalidOperationException("File transfer service did not create the expected output file.");
        }

        var savedBytes = await File.ReadAllBytesAsync(storedPath);
        if (!savedBytes.SequenceEqual(content))
        {
            throw new InvalidOperationException("File transfer service saved unexpected file contents.");
        }

        var progressStatuses = statuses.Count(static item => string.Equals(item.Status, "progress", StringComparison.Ordinal));
        if (progressStatuses <= 0)
        {
            throw new InvalidOperationException("File transfer service did not emit any throttled progress status.");
        }

        if (!File.Exists(fileTransferTraceService.LogPath))
        {
            throw new InvalidOperationException("File transfer trace log was not created.");
        }
    }
    finally
    {
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
        catch
        {
        }
    }
}

static async Task RunClipboardSmokeTestAsync()
{
    using var service = new ClipboardSyncService();
    var originalText = await service.GetTextAsync(CancellationToken.None);

    try
    {
        await service.SetTextAsync("smoke-clipboard-text", CancellationToken.None);
        var actual = await service.GetTextAsync(CancellationToken.None);
        if (!string.Equals(actual, "smoke-clipboard-text", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Clipboard sync service did not return the expected text.");
        }

        await service.SetTextAsync(string.Empty, CancellationToken.None);
        var cleared = await service.GetTextAsync(CancellationToken.None);
        if (!string.IsNullOrEmpty(cleared))
        {
            throw new InvalidOperationException("Clipboard sync service did not clear clipboard text.");
        }
    }
    finally
    {
        await service.SetTextAsync(originalText, CancellationToken.None);
    }
}

static async Task SendTextAsync(ClientWebSocket socket, string payload, CancellationToken cancellationToken)
{
    var bytes = Encoding.UTF8.GetBytes(payload);
    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
}

static async Task<WebSocketPayload> ReadMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
{
    var buffer = new byte[64 * 1024];
    using var stream = new MemoryStream();

    while (true)
    {
        var result = await socket.ReceiveAsync(buffer, cancellationToken);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            return new WebSocketPayload(WebSocketMessageType.Close, Array.Empty<byte>());
        }

        await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
        if (result.EndOfMessage)
        {
            return new WebSocketPayload(result.MessageType, stream.ToArray());
        }
    }
}

static int GetFreeTcpPort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    try
    {
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
    finally
    {
        listener.Stop();
    }
}

internal sealed record WebSocketPayload(WebSocketMessageType MessageType, byte[] Payload);

internal sealed class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DeviceRecord> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, AgentPresenceLogRecord> _presenceLogs = new();

    public Task InitializeSchemaAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task UpsertDeviceOnlineAsync(AgentDescriptor descriptor, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            var createdAt = _devices.TryGetValue(descriptor.DeviceId, out var existing)
                ? existing.CreatedAt
                : now;

            _devices[descriptor.DeviceId] = new DeviceRecord
            {
                DeviceId = descriptor.DeviceId,
                DeviceName = descriptor.DeviceName,
                HostName = descriptor.HostName,
                AgentVersion = descriptor.AgentVersion,
                ScreenWidth = descriptor.ScreenWidth,
                ScreenHeight = descriptor.ScreenHeight,
                IsOnline = true,
                IsAuthorized = existing?.IsAuthorized ?? false,
                AuthorizedAt = existing?.AuthorizedAt,
                AuthorizedBy = existing?.AuthorizedBy,
                CreatedAt = createdAt,
                LastSeenAt = now,
                LastConnectedAt = now,
                LastDisconnectedAt = null
            };
        }

        return Task.CompletedTask;
    }

    public Task<Guid> StartPresenceAsync(AgentDescriptor descriptor, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            _presenceLogs[id] = new AgentPresenceLogRecord
            {
                PresenceId = id,
                DeviceId = descriptor.DeviceId,
                DeviceName = descriptor.DeviceName,
                HostName = descriptor.HostName,
                AgentVersion = descriptor.AgentVersion,
                ConnectedAt = now,
                LastSeenAt = now,
                OnlineSeconds = 0
            };
        }

        return Task.FromResult(id);
    }

    public Task TouchPresenceAsync(Guid presenceId, string deviceId, int screenWidth, int screenHeight, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (_devices.TryGetValue(deviceId, out var device))
            {
                _devices[deviceId] = new DeviceRecord
                {
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName,
                    HostName = device.HostName,
                    AgentVersion = device.AgentVersion,
                    ScreenWidth = screenWidth,
                    ScreenHeight = screenHeight,
                    IsOnline = true,
                    IsAuthorized = device.IsAuthorized,
                    AuthorizedAt = device.AuthorizedAt,
                    AuthorizedBy = device.AuthorizedBy,
                    CreatedAt = device.CreatedAt,
                    LastSeenAt = now,
                    LastConnectedAt = device.LastConnectedAt,
                    LastDisconnectedAt = device.LastDisconnectedAt
                };
            }

            if (_presenceLogs.TryGetValue(presenceId, out var log))
            {
                _presenceLogs[presenceId] = new AgentPresenceLogRecord
                {
                    PresenceId = log.PresenceId,
                    DeviceId = log.DeviceId,
                    DeviceName = log.DeviceName,
                    HostName = log.HostName,
                    AgentVersion = log.AgentVersion,
                    ConnectedAt = log.ConnectedAt,
                    LastSeenAt = now,
                    DisconnectedAt = log.DisconnectedAt,
                    DisconnectReason = log.DisconnectReason,
                    OnlineSeconds = (long)Math.Max(0, (now - log.ConnectedAt).TotalSeconds)
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task ClosePresenceAsync(Guid presenceId, string deviceId, string reason, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (_devices.TryGetValue(deviceId, out var device))
            {
                _devices[deviceId] = new DeviceRecord
                {
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName,
                    HostName = device.HostName,
                    AgentVersion = device.AgentVersion,
                    ScreenWidth = device.ScreenWidth,
                    ScreenHeight = device.ScreenHeight,
                    IsOnline = false,
                    IsAuthorized = device.IsAuthorized,
                    AuthorizedAt = device.AuthorizedAt,
                    AuthorizedBy = device.AuthorizedBy,
                    CreatedAt = device.CreatedAt,
                    LastSeenAt = now,
                    LastConnectedAt = device.LastConnectedAt,
                    LastDisconnectedAt = now
                };
            }

            if (_presenceLogs.TryGetValue(presenceId, out var log))
            {
                _presenceLogs[presenceId] = new AgentPresenceLogRecord
                {
                    PresenceId = log.PresenceId,
                    DeviceId = log.DeviceId,
                    DeviceName = log.DeviceName,
                    HostName = log.HostName,
                    AgentVersion = log.AgentVersion,
                    ConnectedAt = log.ConnectedAt,
                    LastSeenAt = now,
                    DisconnectedAt = now,
                    DisconnectReason = reason,
                    OnlineSeconds = (long)Math.Max(0, (now - log.ConnectedAt).TotalSeconds)
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task SetDeviceAuthorizationAsync(string deviceId, bool isAuthorized, string changedByUserName, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!_devices.TryGetValue(deviceId, out var device))
            {
                return Task.CompletedTask;
            }

            _devices[deviceId] = new DeviceRecord
            {
                DeviceId = device.DeviceId,
                DeviceName = device.DeviceName,
                HostName = device.HostName,
                AgentVersion = device.AgentVersion,
                ScreenWidth = device.ScreenWidth,
                ScreenHeight = device.ScreenHeight,
                IsOnline = device.IsOnline,
                IsAuthorized = isAuthorized,
                AuthorizedAt = isAuthorized ? DateTimeOffset.UtcNow : (DateTimeOffset?)null,
                AuthorizedBy = isAuthorized ? changedByUserName : null,
                CreatedAt = device.CreatedAt,
                LastSeenAt = device.LastSeenAt,
                LastConnectedAt = device.LastConnectedAt,
                LastDisconnectedAt = device.LastDisconnectedAt
            };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            IReadOnlyList<DeviceRecord> result = _devices.Values
                .OrderByDescending(static item => item.IsOnline)
                .ThenByDescending(static item => item.LastSeenAt)
                .Take(take)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<DeviceRecord?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _devices.TryGetValue(deviceId, out var device);
            return Task.FromResult(device);
        }
    }

    public Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            IReadOnlyList<AgentPresenceLogRecord> result = _presenceLogs.Values
                .OrderByDescending(static item => item.ConnectedAt)
                .Take(take)
                .ToList();
            return Task.FromResult(result);
        }
    }
}

internal sealed class InMemoryAuditLogStore : IAuditLogStore
{
    public Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(Array.Empty<AuditLogEntry>());
    }
}






