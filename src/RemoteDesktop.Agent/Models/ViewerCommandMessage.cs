namespace RemoteDesktop.Agent.Models;

public sealed class ViewerCommandMessage
{
    public string Type { get; init; } = string.Empty;

    public double X { get; init; }

    public double Y { get; init; }

    public string? Button { get; init; }

    public string? Code { get; init; }

    public string? Key { get; init; }

    public int DeltaY { get; init; }
}
