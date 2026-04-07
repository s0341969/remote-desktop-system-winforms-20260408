using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Pages;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly IDeviceRepository _repository;
    private readonly ControlServerOptions _options;

    public IndexModel(IDeviceRepository repository, IOptions<ControlServerOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public string ConsoleName { get; private set; } = string.Empty;

    public int OnlineDeviceCount { get; private set; }

    public int TotalDeviceCount { get; private set; }

    public IReadOnlyList<DeviceRecord> Devices { get; private set; } = [];

    public IReadOnlyList<AgentPresenceLogRecord> RecentPresenceLogs { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ConsoleName = _options.ConsoleName;
        Devices = await _repository.GetDevicesAsync(50, cancellationToken);
        RecentPresenceLogs = await _repository.GetPresenceLogsAsync(10, cancellationToken);
        OnlineDeviceCount = Devices.Count(static item => item.IsOnline);
        TotalDeviceCount = Devices.Count;
    }
}
