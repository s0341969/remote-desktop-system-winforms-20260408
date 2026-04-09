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

    public string? UploadId { get; init; }

    public string? FileName { get; init; }

    public long FileSize { get; init; }

    public int SequenceNumber { get; init; }

    public string? ChunkBase64 { get; init; }

    public string? ClipboardText { get; init; }

    public string? RemotePath { get; init; }
}
