using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Options;

namespace RemoteDesktop.Server.Services;

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
        var sweepIntervalSeconds = Math.Clamp(_options.AgentHeartbeatTimeoutSeconds / 3, 1, 10);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(sweepIntervalSeconds));
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
                _logger.LogError(exception, "Agent monitor loop failed.");
            }
        }
    }
}

