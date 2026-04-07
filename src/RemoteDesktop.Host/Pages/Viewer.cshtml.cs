using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Pages;

[Authorize]
public sealed class ViewerModel : PageModel
{
    private readonly IDeviceRepository _repository;

    public ViewerModel(IDeviceRepository repository)
    {
        _repository = repository;
    }

    public string DeviceId { get; private set; } = string.Empty;

    public string DeviceName { get; private set; } = string.Empty;

    public string HostName { get; private set; } = string.Empty;

    public string AgentVersion { get; private set; } = string.Empty;

    public int ScreenWidth { get; private set; }

    public int ScreenHeight { get; private set; }

    public string ViewerWebSocketUrl { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string deviceId, CancellationToken cancellationToken)
    {
        var device = await _repository.GetDeviceAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return RedirectToPage("/Index");
        }

        DeviceId = device.DeviceId;
        DeviceName = device.DeviceName;
        HostName = device.HostName;
        AgentVersion = device.AgentVersion;
        ScreenWidth = device.ScreenWidth;
        ScreenHeight = device.ScreenHeight;

        var scheme = Request.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        ViewerWebSocketUrl = $"{scheme}://{Request.Host}/ws/viewer?deviceId={Uri.EscapeDataString(deviceId)}";
        return Page();
    }
}
