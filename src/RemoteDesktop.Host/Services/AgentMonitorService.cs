using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Options;

namespace RemoteDesktop.Host.Services;

public sealed class AgentMonitorService : BackgroundService
{
    private readonly DeviceBroker _broker;
    private readonly ControlServerOptions _options;
    private readonly ILogger<AgentMonitorService> _logger;

    public AgentMonitorService(DeviceBroker broker, IOptions<ControlServerOptions> options, ILogger<AgentMonitorService> logger)
    {
        _broker = broker;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var staleBefore = DateTimeOffset.UtcNow.AddSeconds(-_options.AgentHeartbeatTimeoutSeconds);
                await _broker.DisconnectStaleAgentsAsync(staleBefore, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "清理逾時 Agent 時發生錯誤。");
            }
        }
    }
}
