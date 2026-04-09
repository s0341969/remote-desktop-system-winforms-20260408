namespace RemoteDesktop.Host.Models;

public sealed class RemoteMoveResult
{
    public string DestinationPath { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
