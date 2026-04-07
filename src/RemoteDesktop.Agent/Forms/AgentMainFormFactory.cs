using RemoteDesktop.Agent.Services;

namespace RemoteDesktop.Agent.Forms;

public sealed class AgentMainFormFactory
{
    private readonly AgentRuntimeState _runtimeState;

    public AgentMainFormFactory(AgentRuntimeState runtimeState)
    {
        _runtimeState = runtimeState;
    }

    public AgentMainForm Create()
    {
        var form = new AgentMainForm();
        form.Bind(_runtimeState);
        return form;
    }
}
