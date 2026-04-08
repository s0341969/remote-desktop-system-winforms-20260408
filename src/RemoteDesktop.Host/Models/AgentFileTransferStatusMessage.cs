namespace RemoteDesktop.Host.Models;

public sealed class AgentFileTransferStatusMessage
{
    public string Type { get; init; } = "file-transfer-status";

    public string UploadId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string StoredFileName { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public long BytesTransferred { get; init; }

    public string Message { get; init; } = string.Empty;
}
