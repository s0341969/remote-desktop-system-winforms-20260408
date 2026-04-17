using RemoteDesktop.Agent.Forms.Settings;
using RemoteDesktop.Agent.Options;
using RemoteDesktop.Agent.Services;
using Microsoft.Extensions.Options;

namespace RemoteDesktop.Agent.Forms;

public sealed class AgentMainFormFactory
{
    private readonly AgentRuntimeState _runtimeState;
    private readonly AgentSettingsFormFactory _agentSettingsFormFactory;
    private readonly AgentOptions _agentOptions;

    public AgentMainFormFactory(
        AgentRuntimeState runtimeState,
        AgentSettingsFormFactory agentSettingsFormFactory,
        IOptions<AgentOptions> agentOptions)
    {
        _runtimeState = runtimeState;
        _agentSettingsFormFactory = agentSettingsFormFactory;
        _agentOptions = agentOptions.Value;
    }

    public AgentMainForm Create()
    {
        var form = new AgentMainForm();
        form.Bind(_runtimeState, _agentSettingsFormFactory, _agentOptions);
        return form;
    }
}
