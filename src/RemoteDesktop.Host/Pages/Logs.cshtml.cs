using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Pages;

[Authorize]
public sealed class LogsModel : PageModel
{
    private readonly IDeviceRepository _repository;

    public LogsModel(IDeviceRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<AgentPresenceLogRecord> Records { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Records = await _repository.GetPresenceLogsAsync(100, cancellationToken);
    }
}
