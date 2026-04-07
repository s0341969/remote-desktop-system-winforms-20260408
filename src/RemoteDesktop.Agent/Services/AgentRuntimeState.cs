using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RemoteDesktop.Agent.Options;

namespace RemoteDesktop.Agent.Services;

public sealed class AgentRuntimeState
{
    private readonly ConcurrentQueue<string> _recentEvents = new();
    private readonly AgentOptions _options;

    public AgentRuntimeState(IOptions<AgentOptions> options)
    {
        _options = options.Value;
        CurrentStatus = "待命";
        ServerUrl = _options.ServerUrl;
        DeviceId = _options.DeviceId;
        DeviceName = _options.DeviceName;
    }

    public string CurrentStatus { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? LastConnectedAt { get; private set; }

    public DateTimeOffset? LastFrameSentAt { get; private set; }

    public string ServerUrl { get; }

    public string DeviceId { get; }

    public string DeviceName { get; }

    public IReadOnlyList<string> GetRecentEvents()
    {
        return _recentEvents.ToArray();
    }

    public void MarkStarting()
    {
        CurrentStatus = "啟動中";
        Enqueue("Agent 啟動中。");
    }

    public void MarkConnecting(Uri serverUri)
    {
        CurrentStatus = "連線中";
        Enqueue($"連線至 {serverUri}");
    }

    public void MarkConnected()
    {
        CurrentStatus = "已連線";
        LastConnectedAt = DateTimeOffset.Now;
        LastError = null;
        Enqueue("已成功連線到控制端。");
    }

    public void MarkFrameSent()
    {
        LastFrameSentAt = DateTimeOffset.Now;
    }

    public void MarkDisconnected(string reason)
    {
        CurrentStatus = "已中斷";
        Enqueue($"連線中斷：{reason}");
    }

    public void MarkError(Exception exception)
    {
        CurrentStatus = "錯誤";
        LastError = exception.Message;
        Enqueue($"錯誤：{exception.Message}");
    }

    private void Enqueue(string message)
    {
        _recentEvents.Enqueue($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
        while (_recentEvents.Count > 20 && _recentEvents.TryDequeue(out _))
        {
        }
    }
}
