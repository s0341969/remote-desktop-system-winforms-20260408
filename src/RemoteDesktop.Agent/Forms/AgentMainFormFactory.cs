using RemoteDesktop.Agent.Forms.Settings;
using RemoteDesktop.Agent.Services;

namespace RemoteDesktop.Agent.Forms;

public sealed class AgentMainFormFactory
{
    private readonly AgentRuntimeState _runtimeState;
    private readonly AgentSettingsFormFactory _agentSettingsFormFactory;

    public AgentMainFormFactory(AgentRuntimeState runtimeState, AgentSettingsFormFactory agentSettingsFormFactory)
    {
        _runtimeState = runtimeState;
        _agentSettingsFormFactory = agentSettingsFormFactory;
    }

    public AgentMainForm Create()
    {
        var form = new AgentMainForm();
        form.Bind(_runtimeState, _agentSettingsFormFactory);
        return form;
    }
}
