namespace RemoteDesktop.Shared.Models;

public sealed class ViewerTransportEnvelope
{
    public string Type { get; init; } = string.Empty;

    public string? DeviceId { get; init; }

    public string? Message { get; init; }

    public AgentFileTransferStatusMessage? TransferStatus { get; init; }

    public AgentClipboardMessage? Clipboard { get; init; }
}
