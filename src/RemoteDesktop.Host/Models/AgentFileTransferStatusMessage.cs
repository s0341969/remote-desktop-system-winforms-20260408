namespace RemoteDesktop.Host.Models;

public sealed class AgentFileTransferStatusMessage
{
    public string Type { get; init; } = "file-transfer-status";

    public string UploadId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Direction { get; init; } = "upload";

    public string FileName { get; init; } = string.Empty;

    public string StoredFileName { get; init; } = string.Empty;

    public string StoredFilePath { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public long BytesTransferred { get; init; }

    public int SequenceNumber { get; init; }

    public string ChunkBase64 { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public string ParentDirectoryPath { get; init; } = string.Empty;

    public bool CanNavigateUp { get; init; }

    public bool EntriesTruncated { get; init; }

    public IReadOnlyList<RemoteFileBrowserEntry> Entries { get; init; } = Array.Empty<RemoteFileBrowserEntry>();
}
