
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RemoteDesktop.Shared.Models;

var configuration = new LoadTestConfiguration(
    AgentCount: 300,
    ViewerCount: 5,
    ViewerFramePayloadBytes: 96 * 1024,
    HeartbeatInterval: TimeSpan.FromSeconds(5),
    ViewerCommandInterval: TimeSpan.FromSeconds(2),
    StreamingFramesPerSecond: 8,
    WarmupDuration: TimeSpan.FromSeconds(15),
    MeasurementDuration: TimeSpan.FromSeconds(30),
    HeartbeatTimeoutProbeAgents: 10,
    HeartbeatTimeoutSeconds: 45,
    DashboardEventTimeout: TimeSpan.FromSeconds(120));

var repositoryRoot = ResolveRepositoryRoot();
var artifactsRoot = Path.Combine(repositoryRoot, "artifacts", "load-tests");
Directory.CreateDirectory(artifactsRoot);
var runStamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
var runDirectory = Path.Combine(artifactsRoot, $"central_300agents_5viewers_{runStamp}");
Directory.CreateDirectory(runDirectory);

var serverPort = GetFreeTcpPort();
var serverUrl = $"http://127.0.0.1:{serverPort}";
var accessKey = "LoadTest-Agent-Key-2026";
var adminPassword = "ChangeMe!2026";
var serverProcess = StartServerProcess(repositoryRoot, serverUrl, accessKey, adminPassword, configuration.HeartbeatTimeoutSeconds);

try
{
    await WaitForHealthAsync(serverUrl, configuration.DashboardEventTimeout);

    using var httpClient = new HttpClient { BaseAddress = new Uri(serverUrl, UriKind.Absolute), Timeout = TimeSpan.FromSeconds(30) };
    var session = await LoginAsync(httpClient, adminPassword);
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

    await using var dashboardTracker = new DashboardTracker(serverUrl, session.AccessToken);
    await dashboardTracker.StartAsync(configuration.DashboardEventTimeout);

    var metricsSampler = new ProcessMetricsSampler(serverProcess, TimeSpan.FromSeconds(1));
    using var metricsCancellationTokenSource = new CancellationTokenSource();
    var metricsTask = metricsSampler.RunAsync(metricsCancellationTokenSource.Token);

    var agents = new List<AgentSimulator>(configuration.AgentCount);
    var viewers = new List<ViewerSimulator>(configuration.ViewerCount);

    try
    {
        var connectTasks = Enumerable.Range(0, configuration.AgentCount)
            .Select(index => ConnectAgentAsync(index, serverUrl, accessKey, configuration, dashboardTracker))
            .ToArray();
        var connectedAgents = await Task.WhenAll(connectTasks);
        agents.AddRange(connectedAgents);

        var devices = await httpClient.GetFromJsonAsync<List<DeviceRecordDto>>($"/api/devices?take={configuration.AgentCount + 50}")
            ?? throw new InvalidOperationException("中央 Server 未回傳裝置清單。");
        var onlineCount = devices.Count(static device => device.IsOnline);
        if (onlineCount < configuration.AgentCount)
        {
            throw new InvalidOperationException($"裝置在線數不足。預期 {configuration.AgentCount}，實際 {onlineCount}。");
        }

        var viewerAgents = agents.Take(configuration.ViewerCount).ToArray();
        foreach (var viewerAgent in viewerAgents)
        {
            using var authorizationResponse = await httpClient.PostAsync($"/api/devices/{Uri.EscapeDataString(viewerAgent.DeviceId)}/authorization?isAuthorized=true&changedBy=load-test", null);
            authorizationResponse.EnsureSuccessStatusCode();
        }

        foreach (var viewerAgent in viewerAgents)
        {
            var viewer = new ViewerSimulator(viewerAgent.DeviceId, serverUrl, session.AccessToken, configuration.ViewerCommandInterval);
            await viewer.StartAsync(configuration.DashboardEventTimeout);
            viewers.Add(viewer);
        }

        var framePayload = BuildFramePayload(configuration.ViewerFramePayloadBytes);
        var frameInterval = TimeSpan.FromMilliseconds(1000d / configuration.StreamingFramesPerSecond);
        foreach (var viewerAgent in viewerAgents)
        {
            viewerAgent.StartStreaming(framePayload, frameInterval);
        }

        await Task.Delay(configuration.WarmupDuration);
        var steadyStateStartedAt = DateTimeOffset.UtcNow;
        var networkBefore = CaptureNetworkCounters(agents, viewers);
        await Task.Delay(configuration.MeasurementDuration);
        var steadyStateEndedAt = DateTimeOffset.UtcNow;
        var networkAfter = CaptureNetworkCounters(agents, viewers);

        var timeoutProbeAgents = agents
            .Skip(configuration.ViewerCount)
            .Take(configuration.HeartbeatTimeoutProbeAgents)
            .ToArray();

        foreach (var timeoutProbeAgent in timeoutProbeAgents)
        {
            timeoutProbeAgent.PauseHeartbeats();
        }

        var timeoutLatenciesByDevice = await WaitForAgentsOfflineAsync(timeoutProbeAgents, configuration.DashboardEventTimeout);

        var timeoutLatencies = timeoutLatenciesByDevice.Values.ToArray();

        metricsCancellationTokenSource.Cancel();
        try
        {
            await metricsTask;
        }
        catch (OperationCanceledException)
        {
        }
        var cpuMemoryOverall = metricsSampler.CreateSummary();
        var cpuMemorySteadyState = metricsSampler.CreateSummary(steadyStateStartedAt, steadyStateEndedAt);
        var activeFrameSentCount = viewerAgents.Sum(static agent => agent.StreamedFrameCount);
        var activeFrameReceivedCount = viewers.Sum(static viewer => viewer.BinaryFrameCount);
        var heartbeatSentCount = agents.Sum(static agent => agent.HeartbeatCount);
        var unexpectedAgentCloseCount = agents.Count(static agent => agent.UnexpectedClose);
        var unexpectedViewerCloseCount = viewers.Count(static viewer => viewer.UnexpectedClose);
        var viewerReadyCount = viewers.Count(static viewer => viewer.ReadyReceived);
        var commandRelayCount = agents.Sum(static agent => agent.RelayedCommandCount);
        var connectLatencies = agents
            .Select(static agent => agent.ConnectDashboardLatency)
            .Where(static latency => latency.HasValue)
            .Select(static latency => latency.GetValueOrDefault())
            .ToArray();

        var dashboardSummary = new DashboardLatencySummary(
            OnlineEventCount: connectLatencies.Length,
            AverageMilliseconds: CalculateAverageMilliseconds(connectLatencies),
            P95Milliseconds: CalculatePercentileMilliseconds(connectLatencies, 0.95),
            MaxMilliseconds: CalculateMaxMilliseconds(connectLatencies));

        var heartbeatTimeoutSummary = new HeartbeatTimeoutSummary(
            ProbeAgentCount: timeoutProbeAgents.Length,
            OfflineEventCount: timeoutLatencies.Length,
            AverageMilliseconds: CalculateAverageMilliseconds(timeoutLatencies),
            P95Milliseconds: CalculatePercentileMilliseconds(timeoutLatencies, 0.95),
            MaxMilliseconds: CalculateMaxMilliseconds(timeoutLatencies));

        var networkSummary = new NetworkSummary(
            ApproxServerIngressBytesSteadyState: networkAfter.ServerIngressBytes - networkBefore.ServerIngressBytes,
            ApproxServerEgressBytesSteadyState: networkAfter.ServerEgressBytes - networkBefore.ServerEgressBytes,
            ApproxServerIngressMbpsSteadyState: CalculateMegabitsPerSecond(networkAfter.ServerIngressBytes - networkBefore.ServerIngressBytes, configuration.MeasurementDuration),
            ApproxServerEgressMbpsSteadyState: CalculateMegabitsPerSecond(networkAfter.ServerEgressBytes - networkBefore.ServerEgressBytes, configuration.MeasurementDuration),
            AgentSentBytesTotal: agents.Sum(static agent => agent.BytesSent),
            AgentReceivedBytesTotal: agents.Sum(static agent => agent.BytesReceived),
            ViewerSentBytesTotal: viewers.Sum(static viewer => viewer.BytesSent),
            ViewerReceivedBytesTotal: viewers.Sum(static viewer => viewer.BytesReceived));

        var stabilitySummary = new WebSocketStabilitySummary(
            ConnectedAgents: agents.Count(static agent => agent.Acknowledged),
            ConnectedViewers: viewers.Count,
            ViewerReadyCount: viewerReadyCount,
            UnexpectedAgentCloseCount: unexpectedAgentCloseCount,
            UnexpectedViewerCloseCount: unexpectedViewerCloseCount,
            HeartbeatsSent: heartbeatSentCount,
            ViewerCommandsRelayedToAgents: commandRelayCount,
            StreamedFramesSent: activeFrameSentCount,
            StreamedFramesReceived: activeFrameReceivedCount,
            FrameRelaySuccessRate: activeFrameSentCount == 0 ? 0 : (double)activeFrameReceivedCount / activeFrameSentCount);

        var devicesAfterTimeout = await httpClient.GetFromJsonAsync<List<DeviceRecordDto>>($"/api/devices?take={configuration.AgentCount + 50}")
            ?? [];

        var report = new LoadTestReport(
            Scenario: new ScenarioSummary(
                AgentCount: configuration.AgentCount,
                ViewerCount: configuration.ViewerCount,
                ActiveStreamingAgents: configuration.ViewerCount,
                AgentHeartbeatIntervalSeconds: configuration.HeartbeatInterval.TotalSeconds,
                HeartbeatTimeoutSeconds: configuration.HeartbeatTimeoutSeconds,
                MeasurementDurationSeconds: configuration.MeasurementDuration.TotalSeconds,
                WarmupDurationSeconds: configuration.WarmupDuration.TotalSeconds,
                StreamingFramesPerSecond: configuration.StreamingFramesPerSecond,
                ViewerFramePayloadBytes: configuration.ViewerFramePayloadBytes,
                ViewerFramePayloadDescription: "使用 96 KiB 固定二進位 frame 模擬 1600px / JPEG quality 55 的中等壓縮畫面負載；Agent 預設截圖參數未變更。"),
            CpuRamSteadyState: cpuMemorySteadyState,
            CpuRamOverall: cpuMemoryOverall,
            Network: networkSummary,
            WebSocketStability: stabilitySummary,
            HeartbeatTimeout: heartbeatTimeoutSummary,
            DashboardLatency: dashboardSummary,
            DeviceCounts: new DeviceCountSummary(
                ExpectedOnlineAgents: configuration.AgentCount,
                InitialOnlineAgents: onlineCount,
                OnlineAgentsAfterTimeoutProbe: devicesAfterTimeout.Count(static device => device.IsOnline)),
            RunAt: DateTimeOffset.Now,
            ServerUrl: serverUrl,
            ArtifactDirectory: runDirectory);

        var jsonPath = Path.Combine(runDirectory, "load-test-report.json");
        var markdownPath = Path.Combine(runDirectory, "load-test-report.md");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdownReport(report), Encoding.UTF8);

        Console.WriteLine($"LOAD_TEST_REPORT={jsonPath}");
        Console.WriteLine($"LOAD_TEST_MARKDOWN={markdownPath}");
        Console.WriteLine($"CONNECTED_AGENTS={stabilitySummary.ConnectedAgents}");
        Console.WriteLine($"CONNECTED_VIEWERS={stabilitySummary.ConnectedViewers}");
        Console.WriteLine($"STEADY_CPU_AVG={cpuMemorySteadyState.AverageCpuPercent:F2}");
        Console.WriteLine($"STEADY_RAM_MAX_MB={cpuMemorySteadyState.MaxWorkingSetMegabytes:F2}");
        Console.WriteLine($"STEADY_INGRESS_MBPS={networkSummary.ApproxServerIngressMbpsSteadyState:F2}");
        Console.WriteLine($"STEADY_EGRESS_MBPS={networkSummary.ApproxServerEgressMbpsSteadyState:F2}");
        Console.WriteLine($"DASHBOARD_P95_MS={dashboardSummary.P95Milliseconds:F2}");
        Console.WriteLine($"HEARTBEAT_TIMEOUT_P95_MS={heartbeatTimeoutSummary.P95Milliseconds:F2}");
        Console.WriteLine("LOAD_TEST_PASSED");
    }
    finally
    {
        foreach (var viewer in viewers)
        {
            await viewer.DisposeAsync();
        }

        foreach (var agent in agents)
        {
            agent.StopStreaming();
            await agent.DisposeAsync();
        }
    }
}
finally
{
    await StopServerProcessAsync(serverProcess);
}

static async Task<AgentSimulator> ConnectAgentAsync(int index, string serverUrl, string accessKey, LoadTestConfiguration configuration, DashboardTracker dashboardTracker)
{
    var agent = new AgentSimulator(
        deviceId: $"load-agent-{index + 1:000}",
        deviceName: $"Load Agent {index + 1:000}",
        serverUrl,
        accessKey,
        configuration.HeartbeatInterval,
        dashboardTracker);
    await agent.StartAsync(configuration.DashboardEventTimeout);
    return agent;
}

static string ResolveRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "RemoteDesktopSystem.sln")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("無法定位專案根目錄。");
}

static Process StartServerProcess(string repositoryRoot, string serverUrl, string accessKey, string adminPassword, int heartbeatTimeoutSeconds)
{
    var serverDllPath = Path.Combine(repositoryRoot, "src", "RemoteDesktop.Server", "bin", "Debug", "net8.0", "RemoteDesktop.Server.dll");
    if (!File.Exists(serverDllPath))
    {
        throw new FileNotFoundException($"找不到中央 Server 輸出：{serverDllPath}");
    }

    var tempRoot = Path.Combine(Path.GetTempPath(), "RemoteDesktopLoadTestServer", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var startInfo = new ProcessStartInfo
    {
        FileName = FindDotnetExecutable(),
        Arguments = $"\"{serverDllPath}\"",
        WorkingDirectory = tempRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    startInfo.Environment["ControlServer__ServerUrl"] = serverUrl;
    startInfo.Environment["ControlServer__ConsoleName"] = "Central Load Test Server";
    startInfo.Environment["ControlServer__AdminUserName"] = "admin";
    startInfo.Environment["ControlServer__AdminPassword"] = adminPassword;
    startInfo.Environment["ControlServer__SharedAccessKey"] = accessKey;
    startInfo.Environment["ControlServer__PersistenceMode"] = "Memory";
    startInfo.Environment["ControlServer__AgentHeartbeatTimeoutSeconds"] = heartbeatTimeoutSeconds.ToString();
    startInfo.Environment["ASPNETCORE_URLS"] = serverUrl;
    startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";

    var process = new Process { StartInfo = startInfo };
    if (!process.Start())
    {
        throw new InvalidOperationException("中央 Server 啟動失敗。");
    }

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

static string FindDotnetExecutable()
{
    var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
    if (!string.IsNullOrWhiteSpace(dotnetRoot))
    {
        var candidate = Path.Combine(dotnetRoot, "dotnet.exe");
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    const string defaultPath = @"C:\Program Files\dotnet\dotnet.exe";
    if (File.Exists(defaultPath))
    {
        return defaultPath;
    }

    return "dotnet";
}
static async Task WaitForHealthAsync(string serverUrl, TimeSpan timeout)
{
    using var httpClient = new HttpClient { BaseAddress = new Uri(serverUrl, UriKind.Absolute), Timeout = TimeSpan.FromSeconds(5) };
    var startedAt = DateTimeOffset.UtcNow;
    while (DateTimeOffset.UtcNow - startedAt < timeout)
    {
        try
        {
            using var response = await httpClient.GetAsync("/healthz");
            if (response.IsSuccessStatusCode)
            {
                return;
            }
        }
        catch
        {
        }

        await Task.Delay(500);
    }

    throw new TimeoutException("中央 Server 健康檢查未在預期時間內就緒。");
}

static async Task<UserSessionDto> LoginAsync(HttpClient httpClient, string adminPassword)
{
    using var response = await httpClient.PostAsJsonAsync("/api/auth/login", new { userName = "admin", password = adminPassword });
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<UserSessionDto>()
        ?? throw new InvalidOperationException("中央登入未回傳 session。");
}

static async Task<Dictionary<string, TimeSpan>> WaitForAgentsOfflineAsync(IReadOnlyCollection<AgentSimulator> agents, TimeSpan timeout)
{
    var results = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
    using var timeoutSource = new CancellationTokenSource(timeout);
    var waitTasks = agents.ToDictionary(
        static agent => agent.DeviceId,
        static agent => agent.WaitForOfflineEventAsync(),
        StringComparer.OrdinalIgnoreCase);

    while (waitTasks.Count > 0)
    {
        var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutSource.Token);
        var completedTask = await Task.WhenAny(waitTasks.Values.Cast<Task>().Append(timeoutTask));
        if (ReferenceEquals(completedTask, timeoutTask))
        {
            break;
        }

        var completedAgent = waitTasks.First(pair => ReferenceEquals(pair.Value, completedTask));
        try
        {
            results[completedAgent.Key] = await completedAgent.Value;
        }
        catch
        {
        }

        waitTasks.Remove(completedAgent.Key);
    }

    return results;
}
static byte[] BuildFramePayload(int payloadBytes)
{
    var buffer = new byte[payloadBytes];
    for (var index = 0; index < buffer.Length; index++)
    {
        buffer[index] = (byte)(index % 251);
    }

    return buffer;
}

static NetworkCounters CaptureNetworkCounters(IEnumerable<AgentSimulator> agents, IEnumerable<ViewerSimulator> viewers)
{
    var agentSent = agents.Sum(static item => item.BytesSent);
    var agentReceived = agents.Sum(static item => item.BytesReceived);
    var viewerSent = viewers.Sum(static item => item.BytesSent);
    var viewerReceived = viewers.Sum(static item => item.BytesReceived);
    return new NetworkCounters(
        ServerIngressBytes: agentSent + viewerSent,
        ServerEgressBytes: agentReceived + viewerReceived);
}

static double CalculateAverageMilliseconds(IReadOnlyCollection<TimeSpan> latencies)
{
    return latencies.Count == 0 ? 0 : latencies.Average(static item => item.TotalMilliseconds);
}

static double CalculatePercentileMilliseconds(IReadOnlyCollection<TimeSpan> latencies, double percentile)
{
    if (latencies.Count == 0)
    {
        return 0;
    }

    var ordered = latencies.OrderBy(static item => item).ToArray();
    var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
    index = Math.Clamp(index, 0, ordered.Length - 1);
    return ordered[index].TotalMilliseconds;
}

static double CalculateMaxMilliseconds(IReadOnlyCollection<TimeSpan> latencies)
{
    return latencies.Count == 0 ? 0 : latencies.Max(static item => item.TotalMilliseconds);
}

static double CalculateMegabitsPerSecond(long bytes, TimeSpan duration)
{
    if (duration <= TimeSpan.Zero)
    {
        return 0;
    }

    return bytes * 8d / duration.TotalSeconds / 1_000_000d;
}

static int GetFreeTcpPort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static async Task StopServerProcessAsync(Process process)
{
    try
    {
        if (process.HasExited)
        {
            return;
        }

        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
    }
    catch
    {
    }
}

static string BuildMarkdownReport(LoadTestReport report)
{
    var builder = new StringBuilder();
    builder.AppendLine("# 中央 Server 壓測報告");
    builder.AppendLine();
    builder.AppendLine($"- 執行時間：{report.RunAt:yyyy-MM-dd HH:mm:ss zzz}");
    builder.AppendLine($"- Server URL：`{report.ServerUrl}`");
    builder.AppendLine($"- 報告目錄：`{report.ArtifactDirectory}`");
    builder.AppendLine();
    builder.AppendLine("## 情境");
    builder.AppendLine();
    builder.AppendLine($"- Agent 在線數：`{report.Scenario.AgentCount}`");
    builder.AppendLine($"- 同時 Viewer：`{report.Scenario.ViewerCount}`");
    builder.AppendLine($"- 同時送圖 Agent：`{report.Scenario.ActiveStreamingAgents}`");
    builder.AppendLine($"- Viewer 串流 FPS：`{report.Scenario.StreamingFramesPerSecond}`");
    builder.AppendLine($"- 單 frame 模擬大小：`{report.Scenario.ViewerFramePayloadBytes}` bytes");
    builder.AppendLine($"- 說明：{report.Scenario.ViewerFramePayloadDescription}");
    builder.AppendLine();
    builder.AppendLine("## CPU / RAM（穩態 30 秒）");
    builder.AppendLine();
    builder.AppendLine($"- 平均 CPU：`{report.CpuRamSteadyState.AverageCpuPercent:F2}%`");
    builder.AppendLine($"- 尖峰 CPU：`{report.CpuRamSteadyState.MaxCpuPercent:F2}%`");
    builder.AppendLine($"- 平均 Working Set：`{report.CpuRamSteadyState.AverageWorkingSetMegabytes:F2} MB`");
    builder.AppendLine($"- 尖峰 Working Set：`{report.CpuRamSteadyState.MaxWorkingSetMegabytes:F2} MB`");
    builder.AppendLine();
    builder.AppendLine("## 網路（穩態 30 秒）");
    builder.AppendLine();
    builder.AppendLine($"- 估算 Server Ingress：`{report.Network.ApproxServerIngressMbpsSteadyState:F2} Mbps`");
    builder.AppendLine($"- 估算 Server Egress：`{report.Network.ApproxServerEgressMbpsSteadyState:F2} Mbps`");
    builder.AppendLine();
    builder.AppendLine("## WebSocket 穩定性");
    builder.AppendLine();
    builder.AppendLine($"- 成功 Agent 連線：`{report.WebSocketStability.ConnectedAgents}`");
    builder.AppendLine($"- 成功 Viewer 連線：`{report.WebSocketStability.ConnectedViewers}`");
    builder.AppendLine($"- Viewer Ready：`{report.WebSocketStability.ViewerReadyCount}`");
    builder.AppendLine($"- 非預期 Agent 斷線：`{report.WebSocketStability.UnexpectedAgentCloseCount}`");
    builder.AppendLine($"- 非預期 Viewer 斷線：`{report.WebSocketStability.UnexpectedViewerCloseCount}`");
    builder.AppendLine($"- 送出 heartbeat：`{report.WebSocketStability.HeartbeatsSent}`");
    builder.AppendLine($"- 轉送控制指令：`{report.WebSocketStability.ViewerCommandsRelayedToAgents}`");
    builder.AppendLine($"- 送出 frame：`{report.WebSocketStability.StreamedFramesSent}`");
    builder.AppendLine($"- Viewer 收到 frame：`{report.WebSocketStability.StreamedFramesReceived}`");
    builder.AppendLine($"- frame relay 成功率：`{report.WebSocketStability.FrameRelaySuccessRate:P2}`");
    builder.AppendLine();
    builder.AppendLine("## heartbeat timeout");
    builder.AppendLine();
    builder.AppendLine($"- Probe Agent：`{report.HeartbeatTimeout.ProbeAgentCount}`");
    builder.AppendLine($"- 收到離線事件：`{report.HeartbeatTimeout.OfflineEventCount}`");
    builder.AppendLine($"- 平均 timeout 偵測：`{report.HeartbeatTimeout.AverageMilliseconds:F2} ms`");
    builder.AppendLine($"- P95 timeout 偵測：`{report.HeartbeatTimeout.P95Milliseconds:F2} ms`");
    builder.AppendLine($"- Max timeout 偵測：`{report.HeartbeatTimeout.MaxMilliseconds:F2} ms`");
    builder.AppendLine();
    builder.AppendLine("## dashboard 延遲");
    builder.AppendLine();
    builder.AppendLine($"- dashboard online 事件數：`{report.DashboardLatency.OnlineEventCount}`");
    builder.AppendLine($"- 平均延遲：`{report.DashboardLatency.AverageMilliseconds:F2} ms`");
    builder.AppendLine($"- P95 延遲：`{report.DashboardLatency.P95Milliseconds:F2} ms`");
    builder.AppendLine($"- Max 延遲：`{report.DashboardLatency.MaxMilliseconds:F2} ms`");
    builder.AppendLine();
    builder.AppendLine("## 裝置數量");
    builder.AppendLine();
    builder.AppendLine($"- 預期在線：`{report.DeviceCounts.ExpectedOnlineAgents}`");
    builder.AppendLine($"- 初始在線：`{report.DeviceCounts.InitialOnlineAgents}`");
    builder.AppendLine($"- timeout probe 後在線：`{report.DeviceCounts.OnlineAgentsAfterTimeoutProbe}`");
    return builder.ToString();
}
static class LoadTestSocketHelpers
{
    public static async Task<WebSocketPayload> ReadMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
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

    public static async Task<DashboardUpdateEnvelope> ReadDashboardEnvelopeAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var payload = await ReadMessageAsync(socket, cancellationToken);
        if (payload.MessageType != WebSocketMessageType.Text)
        {
            throw new InvalidOperationException("dashboard websocket 回傳了非文字訊息。");
        }

        return JsonSerializer.Deserialize<DashboardUpdateEnvelope>(Encoding.UTF8.GetString(payload.Payload), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("dashboard websocket 回傳了無效 envelope。");
    }

    public static async Task<ViewerTransportEnvelope> ReadViewerEnvelopeAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var payload = await ReadMessageAsync(socket, cancellationToken);
        if (payload.MessageType != WebSocketMessageType.Text)
        {
            throw new InvalidOperationException("viewer websocket 回傳了非文字訊息。");
        }

        return JsonSerializer.Deserialize<ViewerTransportEnvelope>(Encoding.UTF8.GetString(payload.Payload), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("viewer websocket 回傳了無效 envelope。");
    }
}

sealed class DashboardTracker : IAsyncDisposable
{
    private readonly Uri _dashboardUri;
    private readonly string _accessToken;
    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _onlineSentAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TimeSpan>> _onlineEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _offlineSentAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TimeSpan>> _offlineEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TimeSpan> _onlineLatencies = [];
    private readonly object _sync = new();
    private CancellationTokenSource? _receiveCancellationTokenSource;
    private Task? _receiveTask;

    public DashboardTracker(string serverUrl, string accessToken)
    {
        _accessToken = accessToken;
        _dashboardUri = new UriBuilder(serverUrl) { Scheme = "ws", Path = "/ws/dashboard" }.Uri;
    }

    public async Task StartAsync(TimeSpan timeout)
    {
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
        _receiveCancellationTokenSource = new CancellationTokenSource();
        await _socket.ConnectAsync(_dashboardUri, CancellationToken.None);
        var ready = await LoadTestSocketHelpers.ReadDashboardEnvelopeAsync(_socket, CancellationToken.None).WaitAsync(timeout);
        if (!string.Equals(ready.Type, "dashboard-ready", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("中央 dashboard websocket 未回傳 dashboard-ready。");
        }

        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCancellationTokenSource.Token));
    }

    public void MarkOnlineSent(string deviceId, DateTimeOffset sentAt)
    {
        _onlineSentAt[deviceId] = sentAt;
        _onlineEvents[deviceId] = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void MarkOfflineSent(string deviceId, DateTimeOffset sentAt)
    {
        _offlineSentAt[deviceId] = sentAt;
        _offlineEvents[deviceId] = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task<TimeSpan> WaitForOnlineAsync(string deviceId) => _onlineEvents[deviceId].Task;

    public Task<TimeSpan> WaitForOfflineAsync(string deviceId) => _offlineEvents[deviceId].Task;

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
        {
            var envelope = await LoadTestSocketHelpers.ReadDashboardEnvelopeAsync(_socket, cancellationToken);
            if (!string.Equals(envelope.Type, "dashboard-changed", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(envelope.DeviceId))
            {
                continue;
            }

            if (string.Equals(envelope.Reason, "device-online", StringComparison.Ordinal)
                && _onlineSentAt.TryRemove(envelope.DeviceId, out var onlineSentAt)
                && _onlineEvents.TryGetValue(envelope.DeviceId, out var onlineTaskSource))
            {
                onlineTaskSource.TrySetResult(DateTimeOffset.UtcNow - onlineSentAt);
            }

            if (string.Equals(envelope.Reason, "device-offline", StringComparison.Ordinal)
                && _offlineSentAt.TryRemove(envelope.DeviceId, out var offlineSentAt)
                && _offlineEvents.TryGetValue(envelope.DeviceId, out var offlineTaskSource))
            {
                offlineTaskSource.TrySetResult(DateTimeOffset.UtcNow - offlineSentAt);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_receiveCancellationTokenSource is not null)
        {
            _receiveCancellationTokenSource.Cancel();
        }

        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "load-test-complete", CancellationToken.None);
            }
            catch
            {
            }
        }

        _socket.Dispose();
        if (_receiveTask is not null)
        {
            try { await _receiveTask; } catch { }
        }
        _receiveCancellationTokenSource?.Dispose();
    }
}

sealed class AgentSimulator : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _serverUrl;
    private readonly string _accessKey;
    private readonly TimeSpan _heartbeatInterval;
    private readonly DashboardTracker _dashboardTracker;
    private readonly ClientWebSocket _socket = new();
    private readonly CancellationTokenSource _disposeCancellationTokenSource = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Task? _heartbeatTask;
    private Task? _receiveTask;
    private Task? _streamingTask;
    private volatile bool _heartbeatEnabled = true;
    private volatile bool _serverDisconnectExpected;
    private volatile bool _streamingEnabled;
    private byte[]? _streamingPayload;
    private TimeSpan _streamingInterval;

    public AgentSimulator(string deviceId, string deviceName, string serverUrl, string accessKey, TimeSpan heartbeatInterval, DashboardTracker dashboardTracker)
    {
        DeviceId = deviceId;
        DeviceName = deviceName;
        _serverUrl = serverUrl;
        _accessKey = accessKey;
        _heartbeatInterval = heartbeatInterval;
        _dashboardTracker = dashboardTracker;
    }

    public string DeviceId { get; }
    public string DeviceName { get; }
    public bool Acknowledged { get; private set; }
    public long BytesSent { get; private set; }
    public long BytesReceived { get; private set; }
    public int HeartbeatCount { get; private set; }
    public int RelayedCommandCount { get; private set; }
    public int StreamedFrameCount { get; private set; }
    public bool UnexpectedClose { get; private set; }
    public TimeSpan? ConnectDashboardLatency { get; private set; }
    public TimeSpan? HeartbeatTimeoutLatency { get; private set; }

    public async Task StartAsync(TimeSpan timeout)
    {
        var agentUri = new UriBuilder(_serverUrl) { Scheme = "ws", Path = "/ws/agent" }.Uri;
        await _socket.ConnectAsync(agentUri, CancellationToken.None);

        var hello = new AgentHelloMessage
        {
            Type = "hello",
            DeviceId = DeviceId,
            DeviceName = DeviceName,
            HostName = Environment.MachineName,
            AgentVersion = "load-test",
            AccessKey = _accessKey,
            ScreenWidth = 1600,
            ScreenHeight = 900
        };

        _dashboardTracker.MarkOnlineSent(DeviceId, DateTimeOffset.UtcNow);
        await SendTextAsync(JsonSerializer.Serialize(hello, JsonOptions), CancellationToken.None);
        var ack = await LoadTestSocketHelpers.ReadMessageAsync(_socket, CancellationToken.None).WaitAsync(timeout);
        if (ack.MessageType != WebSocketMessageType.Text || !Encoding.UTF8.GetString(ack.Payload).Contains("\"hello-ack\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Agent {DeviceId} 未收到 hello-ack。");
        }

        Acknowledged = true;
        ConnectDashboardLatency = await _dashboardTracker.WaitForOnlineAsync(DeviceId).WaitAsync(timeout);
        _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_disposeCancellationTokenSource.Token));
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_disposeCancellationTokenSource.Token));
    }

    public void StartStreaming(byte[] payload, TimeSpan interval)
    {
        _streamingPayload = payload;
        _streamingInterval = interval;
        _streamingEnabled = true;
        _streamingTask ??= Task.Run(() => StreamingLoopAsync(_disposeCancellationTokenSource.Token));
    }

    public void StopStreaming() => _streamingEnabled = false;

    public void PauseHeartbeats()
    {
        if (!_heartbeatEnabled)
        {
            return;
        }

        _heartbeatEnabled = false;
        _serverDisconnectExpected = true;
        _dashboardTracker.MarkOfflineSent(DeviceId, DateTimeOffset.UtcNow);
    }

    public async Task<TimeSpan> WaitForOfflineEventAsync()
    {
        var latency = await _dashboardTracker.WaitForOfflineAsync(DeviceId);
        HeartbeatTimeoutLatency = latency;
        return latency;
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_heartbeatInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (!_heartbeatEnabled || _socket.State != WebSocketState.Open)
            {
                continue;
            }

            var heartbeat = new AgentHeartbeatMessage { Type = "heartbeat", ScreenWidth = 1600, ScreenHeight = 900 };
            await SendTextAsync(JsonSerializer.Serialize(heartbeat, JsonOptions), cancellationToken);
            HeartbeatCount++;
        }
    }

    private async Task StreamingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_streamingEnabled || _streamingPayload is null || _socket.State != WebSocketState.Open)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            await SendBinaryAsync(_streamingPayload, cancellationToken);
            StreamedFrameCount++;
            await Task.Delay(_streamingInterval, cancellationToken);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                var payload = await LoadTestSocketHelpers.ReadMessageAsync(_socket, cancellationToken);
                if (payload.MessageType == WebSocketMessageType.Close)
                {
                    if (!cancellationToken.IsCancellationRequested && !_serverDisconnectExpected)
                    {
                        UnexpectedClose = true;
                    }
                    break;
                }

                BytesReceived += payload.Payload.LongLength;
                if (payload.MessageType == WebSocketMessageType.Text)
                {
                    RelayedCommandCount++;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            if (!cancellationToken.IsCancellationRequested && !_serverDisconnectExpected)
                    {
                        UnexpectedClose = true;
                    }
        }
    }

    private async Task SendTextAsync(string json, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            BytesSent += bytes.LongLength;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task SendBinaryAsync(byte[] payload, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(payload, WebSocketMessageType.Binary, true, cancellationToken);
            BytesSent += payload.LongLength;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposeCancellationTokenSource.Cancel();
        StopStreaming();
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "load-test-complete", CancellationToken.None); } catch { }
        }

        _socket.Dispose();
        _sendLock.Dispose();
        _disposeCancellationTokenSource.Dispose();
        if (_heartbeatTask is not null) { try { await _heartbeatTask; } catch { } }
        if (_streamingTask is not null) { try { await _streamingTask; } catch { } }
        if (_receiveTask is not null) { try { await _receiveTask; } catch { } }
    }
}

sealed class ViewerSimulator : IAsyncDisposable
{
    private readonly string _deviceId;
    private readonly Uri _viewerUri;
    private readonly TimeSpan _commandInterval;
    private readonly ClientWebSocket _socket = new();
    private readonly CancellationTokenSource _disposeCancellationTokenSource = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Task? _receiveTask;
    private Task? _commandTask;
    private volatile bool _canControl;

    public ViewerSimulator(string deviceId, string serverUrl, string accessToken, TimeSpan commandInterval)
    {
        _deviceId = deviceId;
        _viewerUri = new UriBuilder(serverUrl) { Scheme = "ws", Path = "/ws/viewer", Query = $"deviceId={Uri.EscapeDataString(deviceId)}" }.Uri;
        _commandInterval = commandInterval;
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
    }

    public long BytesSent { get; private set; }
    public long BytesReceived { get; private set; }
    public int BinaryFrameCount { get; private set; }
    public bool ReadyReceived { get; private set; }
    public bool UnexpectedClose { get; private set; }

    public async Task StartAsync(TimeSpan timeout)
    {
        await _socket.ConnectAsync(_viewerUri, CancellationToken.None);
        var envelope = await LoadTestSocketHelpers.ReadViewerEnvelopeAsync(_socket, CancellationToken.None).WaitAsync(timeout);
        if (!string.Equals(envelope.Type, "viewer-ready", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Viewer {_deviceId} 未收到 viewer-ready。");
        }

        ReadyReceived = true;
        _canControl = envelope.CanControl == true;
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_disposeCancellationTokenSource.Token));
        _commandTask = Task.Run(() => CommandLoopAsync(_disposeCancellationTokenSource.Token));
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                var payload = await LoadTestSocketHelpers.ReadMessageAsync(_socket, cancellationToken);
                if (payload.MessageType == WebSocketMessageType.Close)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        UnexpectedClose = true;
                    }
                    break;
                }

                BytesReceived += payload.Payload.LongLength;
                if (payload.MessageType == WebSocketMessageType.Binary)
                {
                    BinaryFrameCount++;
                    continue;
                }

                var envelope = JsonSerializer.Deserialize<ViewerTransportEnvelope>(Encoding.UTF8.GetString(payload.Payload), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (envelope is not null && string.Equals(envelope.Type, "viewer-mode-updated", StringComparison.Ordinal))
                {
                    _canControl = envelope.CanControl == true;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                UnexpectedClose = true;
            }
        }
    }

    private async Task CommandLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_commandInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (!_canControl || _socket.State != WebSocketState.Open)
            {
                continue;
            }

            var command = JsonSerializer.Serialize(new ViewerCommandMessage { Type = "move", X = 0.5, Y = 0.5 }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var bytes = Encoding.UTF8.GetBytes(command);
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
                BytesSent += bytes.LongLength;
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposeCancellationTokenSource.Cancel();
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "load-test-complete", CancellationToken.None); } catch { }
        }

        _socket.Dispose();
        _sendLock.Dispose();
        _disposeCancellationTokenSource.Dispose();
        if (_commandTask is not null) { try { await _commandTask; } catch { } }
        if (_receiveTask is not null) { try { await _receiveTask; } catch { } }
    }
}

sealed class ProcessMetricsSampler
{
    private readonly Process _process;
    private readonly TimeSpan _interval;
    private readonly List<ProcessSample> _samples = [];
    private readonly object _sync = new();

    public ProcessMetricsSampler(Process process, TimeSpan interval)
    {
        _process = process;
        _interval = interval;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _process.Refresh();
        var previousTimestamp = DateTimeOffset.UtcNow;
        var previousCpu = _process.TotalProcessorTime;

        while (!cancellationToken.IsCancellationRequested && !_process.HasExited)
        {
            await Task.Delay(_interval, cancellationToken);
            _process.Refresh();
            var now = DateTimeOffset.UtcNow;
            var currentCpu = _process.TotalProcessorTime;
            var cpuPercent = (currentCpu - previousCpu).TotalMilliseconds / ((now - previousTimestamp).TotalMilliseconds * Environment.ProcessorCount) * 100d;
            lock (_sync)
            {
                _samples.Add(new ProcessSample(now, cpuPercent, _process.WorkingSet64 / 1024d / 1024d));
            }

            previousTimestamp = now;
            previousCpu = currentCpu;
        }
    }

    public ProcessMetricSummary CreateSummary(DateTimeOffset? startedAt = null, DateTimeOffset? endedAt = null)
    {
        List<ProcessSample> selected;
        lock (_sync)
        {
            selected = _samples
                .Where(sample => (!startedAt.HasValue || sample.Timestamp >= startedAt.Value) && (!endedAt.HasValue || sample.Timestamp <= endedAt.Value))
                .ToList();
        }

        if (selected.Count == 0)
        {
            return new ProcessMetricSummary(0, 0, 0, 0);
        }

        return new ProcessMetricSummary(
            AverageCpuPercent: selected.Average(static sample => sample.CpuPercent),
            MaxCpuPercent: selected.Max(static sample => sample.CpuPercent),
            AverageWorkingSetMegabytes: selected.Average(static sample => sample.WorkingSetMegabytes),
            MaxWorkingSetMegabytes: selected.Max(static sample => sample.WorkingSetMegabytes));
    }
}
readonly record struct WebSocketPayload(WebSocketMessageType MessageType, byte[] Payload);
readonly record struct NetworkCounters(long ServerIngressBytes, long ServerEgressBytes);
readonly record struct ProcessSample(DateTimeOffset Timestamp, double CpuPercent, double WorkingSetMegabytes);
readonly record struct LoadTestConfiguration(int AgentCount, int ViewerCount, int ViewerFramePayloadBytes, TimeSpan HeartbeatInterval, TimeSpan ViewerCommandInterval, int StreamingFramesPerSecond, TimeSpan WarmupDuration, TimeSpan MeasurementDuration, int HeartbeatTimeoutProbeAgents, int HeartbeatTimeoutSeconds, TimeSpan DashboardEventTimeout);
readonly record struct ScenarioSummary(int AgentCount, int ViewerCount, int ActiveStreamingAgents, double AgentHeartbeatIntervalSeconds, int HeartbeatTimeoutSeconds, double MeasurementDurationSeconds, double WarmupDurationSeconds, int StreamingFramesPerSecond, int ViewerFramePayloadBytes, string ViewerFramePayloadDescription);
readonly record struct ProcessMetricSummary(double AverageCpuPercent, double MaxCpuPercent, double AverageWorkingSetMegabytes, double MaxWorkingSetMegabytes);
readonly record struct NetworkSummary(long ApproxServerIngressBytesSteadyState, long ApproxServerEgressBytesSteadyState, double ApproxServerIngressMbpsSteadyState, double ApproxServerEgressMbpsSteadyState, long AgentSentBytesTotal, long AgentReceivedBytesTotal, long ViewerSentBytesTotal, long ViewerReceivedBytesTotal);
readonly record struct WebSocketStabilitySummary(int ConnectedAgents, int ConnectedViewers, int ViewerReadyCount, int UnexpectedAgentCloseCount, int UnexpectedViewerCloseCount, int HeartbeatsSent, int ViewerCommandsRelayedToAgents, int StreamedFramesSent, int StreamedFramesReceived, double FrameRelaySuccessRate);
readonly record struct HeartbeatTimeoutSummary(int ProbeAgentCount, int OfflineEventCount, double AverageMilliseconds, double P95Milliseconds, double MaxMilliseconds);
readonly record struct DashboardLatencySummary(int OnlineEventCount, double AverageMilliseconds, double P95Milliseconds, double MaxMilliseconds);
readonly record struct DeviceCountSummary(int ExpectedOnlineAgents, int InitialOnlineAgents, int OnlineAgentsAfterTimeoutProbe);
readonly record struct LoadTestReport(ScenarioSummary Scenario, ProcessMetricSummary CpuRamSteadyState, ProcessMetricSummary CpuRamOverall, NetworkSummary Network, WebSocketStabilitySummary WebSocketStability, HeartbeatTimeoutSummary HeartbeatTimeout, DashboardLatencySummary DashboardLatency, DeviceCountSummary DeviceCounts, DateTimeOffset RunAt, string ServerUrl, string ArtifactDirectory);

sealed class DeviceRecordDto
{
    public string DeviceId { get; init; } = string.Empty;

    public bool IsOnline { get; init; }
}

















