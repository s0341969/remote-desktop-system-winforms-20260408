using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Net.Http.Json;
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
using RemoteDesktop.Server.Hosting;
using ServerIDeviceRepository = RemoteDesktop.Server.Services.IDeviceRepository;
using ServerInMemoryDeviceRepository = RemoteDesktop.Server.Services.InMemoryDeviceRepository;
using ServerControlServerOptions = RemoteDesktop.Server.Options.ControlServerOptions;



await RunHostRelaySmokeTestAsync();
await RunCentralViewerLockSmokeTestAsync();
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

    builder.Services.AddSingleton<RemoteDesktop.Host.Services.IDeviceRepository, InMemoryDeviceRepository>();
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
            true,
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

static async Task RunCentralViewerLockSmokeTestAsync()
{
    var port = GetFreeTcpPort();
    var serverUrl = $"http://127.0.0.1:{port}";
    var accessKey = "ChangeMe-Agent-Key";
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseSetting("urls", serverUrl);

    builder.Services.AddSingleton<IOptions<ServerControlServerOptions>>(Options.Create(new ServerControlServerOptions
    {
        ServerUrl = serverUrl,
        ConsoleName = "Central Smoke Console",
        AdminUserName = "admin",
        AdminPassword = "ChangeMe!2026",
        SharedAccessKey = accessKey,
        AgentHeartbeatTimeoutSeconds = 45,
        PersistenceMode = "Memory"
    }));

    builder.Services.AddSingleton<ServerIDeviceRepository, ServerInMemoryDeviceRepository>();
    builder.Services.AddRemoteDesktopServerCore();

    await using var app = builder.Build();
    app.MapRemoteDesktopServerEndpoints();
    await app.StartAsync();

    try
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(serverUrl, UriKind.Absolute) };
        var loginResponse = await httpClient.PostAsJsonAsync("/api/auth/login", new { userName = "admin", password = "ChangeMe!2026" });
        loginResponse.EnsureSuccessStatusCode();
        var session = await loginResponse.Content.ReadFromJsonAsync<RemoteDesktop.Shared.Models.UserSessionDto>()
            ?? throw new InvalidOperationException("Central login did not return a session.");

        if (string.IsNullOrWhiteSpace(session.AccessToken))
        {
            throw new InvalidOperationException("Central login did not return an access token.");
        }

        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);

        using var agentSocket = new ClientWebSocket();
        await agentSocket.ConnectAsync(new UriBuilder(serverUrl) { Scheme = "ws", Path = "/ws/agent" }.Uri, CancellationToken.None);
        await SendTextAsync(agentSocket, JsonSerializer.Serialize(new RemoteDesktop.Shared.Models.AgentHelloMessage
        {
            Type = "hello",
            DeviceId = "central-lock-device-001",
            DeviceName = "Central Lock Device",
            HostName = Environment.MachineName,
            AgentVersion = "smoke",
            AccessKey = accessKey,
            ScreenWidth = 1920,
            ScreenHeight = 1080
        }), CancellationToken.None);

        var agentAck = await ReadMessageAsync(agentSocket, CancellationToken.None);
        if (!Encoding.UTF8.GetString(agentAck.Payload).Contains("\"hello-ack\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Central server did not acknowledge the agent hello message.");
        }

        var authResponse = await httpClient.PostAsync("/api/devices/central-lock-device-001/authorization?isAuthorized=true&changedBy=ignored", null);
        authResponse.EnsureSuccessStatusCode();

        using var viewerOne = new ClientWebSocket();
        viewerOne.Options.SetRequestHeader("Authorization", $"Bearer {session.AccessToken}");
        await viewerOne.ConnectAsync(new UriBuilder(serverUrl) { Scheme = "ws", Path = "/ws/viewer", Query = "deviceId=central-lock-device-001" }.Uri, CancellationToken.None);
        var viewerOneReady = await ReadViewerEnvelopeAsync(viewerOne, CancellationToken.None);
        if (!string.Equals(viewerOneReady.Type, "viewer-ready", StringComparison.Ordinal) || viewerOneReady.CanControl != true)
        {
            throw new InvalidOperationException("First viewer was expected to become the controlling session.");
        }

        using var viewerTwo = new ClientWebSocket();
        viewerTwo.Options.SetRequestHeader("Authorization", $"Bearer {session.AccessToken}");
        await viewerTwo.ConnectAsync(new UriBuilder(serverUrl) { Scheme = "ws", Path = "/ws/viewer", Query = "deviceId=central-lock-device-001" }.Uri, CancellationToken.None);
        var viewerTwoReady = await ReadViewerEnvelopeAsync(viewerTwo, CancellationToken.None);
        if (!string.Equals(viewerTwoReady.Type, "viewer-ready", StringComparison.Ordinal) || viewerTwoReady.CanControl != false)
        {
            throw new InvalidOperationException("Second viewer should have been downgraded to observe-only mode.");
        }

        await SendTextAsync(viewerTwo, JsonSerializer.Serialize(new { type = "viewer-control-request", forceTakeover = true }), CancellationToken.None);
        var viewerTwoUpdated = await ReadViewerEnvelopeAsync(viewerTwo, CancellationToken.None);
        var viewerOneUpdated = await ReadViewerEnvelopeAsync(viewerOne, CancellationToken.None);
        if (!string.Equals(viewerTwoUpdated.Type, "viewer-mode-updated", StringComparison.Ordinal) || viewerTwoUpdated.CanControl != true)
        {
            throw new InvalidOperationException("Second viewer did not receive control after takeover.");
        }

        if (!string.Equals(viewerOneUpdated.Type, "viewer-mode-updated", StringComparison.Ordinal) || viewerOneUpdated.CanControl != false)
        {
            throw new InvalidOperationException("Original controller was not downgraded to observe-only after takeover.");
        }

        await SendTextAsync(viewerOne, JsonSerializer.Serialize(new RemoteDesktop.Shared.Models.ViewerCommandMessage { Type = "move", X = 0.1, Y = 0.1 }), CancellationToken.None);
        await Task.Delay(750);

        await SendTextAsync(viewerTwo, JsonSerializer.Serialize(new RemoteDesktop.Shared.Models.ViewerCommandMessage { Type = "move", X = 0.4, Y = 0.6 }), CancellationToken.None);
        var relayedCommand = await ReadMessageAsync(agentSocket, CancellationToken.None);
        var relayedJson = Encoding.UTF8.GetString(relayedCommand.Payload);
        var relayedViewerCommand = JsonSerializer.Deserialize<RemoteDesktop.Shared.Models.ViewerCommandMessage>(relayedJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (relayedViewerCommand is null
            || !string.Equals(relayedViewerCommand.Type, "move", StringComparison.Ordinal)
            || Math.Abs(relayedViewerCommand.X - 0.4) > 0.0001
            || Math.Abs(relayedViewerCommand.Y - 0.6) > 0.0001)
        {
            throw new InvalidOperationException($"Viewer lock did not preserve control ownership after takeover. Payload: {relayedJson}");
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

        var browseStatuses = new List<RemoteDesktop.Agent.Models.AgentFileTransferStatusMessage>();
        await fileTransferService.TryHandleAsync(new RemoteDesktop.Agent.Models.ViewerCommandMessage
        {
            Type = "file-browser-list",
            UploadId = Guid.NewGuid().ToString("N"),
            DirectoryPath = tempRoot
        }, (status, cancellationToken) =>
        {
            browseStatuses.Add(status);
            return Task.CompletedTask;
        }, CancellationToken.None);

        var directoryListing = browseStatuses.SingleOrDefault(static status =>
            string.Equals(status.Direction, "browse", StringComparison.Ordinal)
            && string.Equals(status.Status, "listed", StringComparison.Ordinal));
        if (directoryListing is null)
        {
            throw new InvalidOperationException("File transfer service did not return a remote directory listing.");
        }

        if (!directoryListing.Entries.Any(entry =>
                !entry.IsDirectory
                && string.Equals(entry.Name, completed.StoredFileName, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Remote directory listing did not include the uploaded file.");
        }

        var filePathBrowseStatuses = new List<RemoteDesktop.Agent.Models.AgentFileTransferStatusMessage>();
        await fileTransferService.TryHandleAsync(new RemoteDesktop.Agent.Models.ViewerCommandMessage
        {
            Type = "file-browser-list",
            UploadId = Guid.NewGuid().ToString("N"),
            DirectoryPath = storedPath
        }, (status, cancellationToken) =>
        {
            filePathBrowseStatuses.Add(status);
            return Task.CompletedTask;
        }, CancellationToken.None);

        var filePathListing = filePathBrowseStatuses.SingleOrDefault(static status =>
            string.Equals(status.Direction, "browse", StringComparison.Ordinal)
            && string.Equals(status.Status, "listed", StringComparison.Ordinal));
        if (filePathListing is null || !string.Equals(filePathListing.DirectoryPath, tempRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Remote directory listing did not normalize a file path to its parent directory.");
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
        var actual = await ReadClipboardEventuallyAsync(service, expectedText: "smoke-clipboard-text", expectEmpty: false, CancellationToken.None);
        if (!string.Equals(actual, "smoke-clipboard-text", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Clipboard sync service did not return the expected text.");
        }

        await service.SetTextAsync(string.Empty, CancellationToken.None);
        var cleared = await ReadClipboardEventuallyAsync(service, expectedText: string.Empty, expectEmpty: true, CancellationToken.None);
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

static async Task<string> ReadClipboardEventuallyAsync(ClipboardSyncService service, string expectedText, bool expectEmpty, CancellationToken cancellationToken)
{
    for (var attempt = 0; attempt < 10; attempt++)
    {
        var current = await service.GetTextAsync(cancellationToken);
        if ((expectEmpty && string.IsNullOrEmpty(current)) || string.Equals(current, expectedText, StringComparison.Ordinal))
        {
            return current;
        }

        await Task.Delay(100, cancellationToken);
    }

    return await service.GetTextAsync(cancellationToken);
}
static async Task SendTextAsync(ClientWebSocket socket, string payload, CancellationToken cancellationToken)
{
    var bytes = Encoding.UTF8.GetBytes(payload);
    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
}

static async Task<RemoteDesktop.Shared.Models.ViewerTransportEnvelope> ReadViewerEnvelopeAsync(ClientWebSocket socket, CancellationToken cancellationToken)
{
    var payload = await ReadMessageAsync(socket, cancellationToken);
    if (payload.MessageType != WebSocketMessageType.Text)
    {
        throw new InvalidOperationException("Viewer websocket returned an unexpected non-text payload.");
    }

    return JsonSerializer.Deserialize<RemoteDesktop.Shared.Models.ViewerTransportEnvelope>(Encoding.UTF8.GetString(payload.Payload), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException("Viewer websocket returned an invalid envelope.");
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





















