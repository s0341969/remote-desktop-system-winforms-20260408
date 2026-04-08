using RemoteDesktop.Host.Services.Settings;

namespace RemoteDesktop.Host.Forms.Settings;

public sealed class HostSettingsFormFactory
{
    private readonly IHostSettingsStore _settingsStore;

    public HostSettingsFormFactory(IHostSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public async Task<HostSettingsForm> CreateAsync(bool showResultDialogs = true, CancellationToken cancellationToken = default)
    {
        var form = new HostSettingsForm(showResultDialogs);
        var settings = await _settingsStore.LoadAsync(cancellationToken);
        form.Bind(_settingsStore, settings, showResultDialogs);
        return form;
    }
}
