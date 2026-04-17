namespace RemoteDesktop.Agent.Services;

public static class AgentIdentity
{
    public static string GetMachineIdentity()
    {
        var machineName = Environment.MachineName?.Trim();
        return string.IsNullOrWhiteSpace(machineName) ? "UNKNOWN-HOST" : machineName;
    }
}
