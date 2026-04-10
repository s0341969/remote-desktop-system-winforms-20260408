using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services.Auditing;
using RemoteDesktop.Host.Services.Settings;

namespace RemoteDesktop.Host.Forms.Settings;

public sealed class HostSettingsFormFactory
{
    private readonly IHostSettingsStore _settingsStore;
    private readonly IAuditService _auditService;

    public HostSettingsFormFactory(IHostSettingsStore settingsStore, IAuditService auditService)
    {
        _settingsStore = settingsStore;
        _auditService = auditService;
    }

    public async Task<HostSettingsForm> CreateAsync(AuthenticatedUserSession currentUser, bool showResultDialogs = true, CancellationToken cancellationToken = default)
    {
        var form = new HostSettingsForm(showResultDialogs);
        var settings = await _settingsStore.LoadAsync(cancellationToken);
        form.Bind(_settingsStore, _auditService, currentUser, settings, showResultDialogs);
        return form;
    }
}
