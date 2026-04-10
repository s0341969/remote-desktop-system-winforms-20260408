namespace RemoteDesktop.Shared.Models;

public sealed class AgentClipboardMessage
{
    public string Type { get; init; } = "clipboard-sync";

    public string Operation { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public bool Truncated { get; init; }

    public string Message { get; init; } = string.Empty;
}

