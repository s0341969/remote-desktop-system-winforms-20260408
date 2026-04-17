using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RemoteDesktop.Agent.Options;

namespace RemoteDesktop.Agent.Services;

public sealed class AgentRuntimeState
{
    private readonly object _sync = new();
    private readonly ConcurrentQueue<string> _recentEvents = new();
    private readonly AgentOptions _options;

    public AgentRuntimeState(IOptions<AgentOptions> options)
    {
        _options = options.Value;
        var machineIdentity = AgentIdentity.GetMachineIdentity();
        CurrentStatus = AgentUiText.Bi("閒置", "Idle");
        ServerUrl = _options.ServerUrl;
        DeviceId = machineIdentity;
        DeviceName = machineIdentity;
    }

    public string CurrentStatus { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? LastConnectedAt { get; private set; }

    public DateTimeOffset? LastFrameSentAt { get; private set; }

    public string ServerUrl { get; }

    public string DeviceId { get; }

    public string DeviceName { get; }

    public AgentRuntimeSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new AgentRuntimeSnapshot(
                CurrentStatus,
                LastError,
                LastConnectedAt,
                LastFrameSentAt,
                ServerUrl,
                DeviceId,
                DeviceName,
                _recentEvents.ToArray());
        }
    }

    public void MarkStarting()
    {
        lock (_sync)
        {
            CurrentStatus = AgentUiText.Bi("啟動中", "Starting");
            Enqueue(AgentUiText.Bi("Agent 正在啟動。", "Agent is starting."));
        }
    }

    public void MarkConnecting(Uri serverUri)
    {
        lock (_sync)
        {
            CurrentStatus = AgentUiText.Bi("連線中", "Connecting");
            Enqueue(AgentUiText.Bi($"正在連線到 {serverUri}", $"Connecting to {serverUri}"));
        }
    }

    public void MarkConnected()
    {
        lock (_sync)
        {
            CurrentStatus = AgentUiText.Bi("已連線", "Connected");
            LastConnectedAt = DateTimeOffset.Now;
            LastError = null;
            Enqueue(AgentUiText.Bi("已連線到控制伺服器。", "Connected to Control Server."));
        }
    }

    public void MarkInfo(string message)
    {
        lock (_sync)
        {
            Enqueue(message);
        }
    }

    public void MarkWarning(string message)
    {
        lock (_sync)
        {
            LastError = message;
            Enqueue(message);
        }
    }

    public void MarkFrameSent()
    {
        lock (_sync)
        {
            LastFrameSentAt = DateTimeOffset.Now;
        }
    }

    public void MarkDisconnected(string reason)
    {
        lock (_sync)
        {
            CurrentStatus = AgentUiText.Bi("已中斷", "Disconnected");
            Enqueue(AgentUiText.Bi($"連線已中斷：{reason}", $"Disconnected: {reason}"));
        }
    }

    public void MarkError(Exception exception)
    {
        lock (_sync)
        {
            CurrentStatus = AgentUiText.Bi("錯誤", "Error");
            LastError = AgentUiText.Bi($"發生錯誤：{exception.Message}", $"Error: {exception.Message}");
            Enqueue(LastError);
        }
    }

    private void Enqueue(string message)
    {
        _recentEvents.Enqueue($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
        while (_recentEvents.Count > 20 && _recentEvents.TryDequeue(out _))
        {
        }
    }
}

public sealed record AgentRuntimeSnapshot(
    string CurrentStatus,
    string? LastError,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastFrameSentAt,
    string ServerUrl,
    string DeviceId,
    string DeviceName,
    IReadOnlyList<string> RecentEvents);
