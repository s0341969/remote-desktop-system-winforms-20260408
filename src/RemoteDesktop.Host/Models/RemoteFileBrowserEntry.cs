namespace RemoteDesktop.Host.Models;

public sealed class RemoteFileBrowserEntry
{
    public string Name { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public long Size { get; init; }

    public DateTimeOffset? LastModifiedAt { get; init; }
}
