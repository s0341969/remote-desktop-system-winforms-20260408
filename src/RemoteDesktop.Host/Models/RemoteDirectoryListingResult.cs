namespace RemoteDesktop.Host.Models;

public sealed class RemoteDirectoryListingResult
{
    public string DirectoryPath { get; init; } = string.Empty;

    public string ParentDirectoryPath { get; init; } = string.Empty;

    public bool CanNavigateUp { get; init; }

    public bool EntriesTruncated { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<string> RootPaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RemoteFileBrowserEntry> Entries { get; init; } = Array.Empty<RemoteFileBrowserEntry>();
}
