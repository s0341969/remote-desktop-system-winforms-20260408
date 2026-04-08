using RemoteDesktop.Agent.Services.Settings;

namespace RemoteDesktop.Agent.Forms.Settings;

public sealed class AgentSettingsFormFactory
{
    private readonly IAgentSettingsStore _settingsStore;

    public AgentSettingsFormFactory(IAgentSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public async Task<AgentSettingsForm> CreateAsync(bool showResultDialogs = true, CancellationToken cancellationToken = default)
    {
        var form = new AgentSettingsForm(showResultDialogs);
        var settings = await _settingsStore.LoadAsync(cancellationToken);
        form.Bind(_settingsStore, settings, showResultDialogs);
        return form;
    }
}
