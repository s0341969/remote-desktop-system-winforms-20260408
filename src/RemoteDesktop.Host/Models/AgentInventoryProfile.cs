namespace RemoteDesktop.Host.Models;

public sealed class AgentInventoryProfile
{
    public string CpuName { get; init; } = string.Empty;

    public long InstalledMemoryBytes { get; init; }

    public string StorageSummary { get; init; } = string.Empty;

    public string OsName { get; init; } = string.Empty;

    public string OsVersion { get; init; } = string.Empty;

    public string OsBuildNumber { get; init; } = string.Empty;

    public string OfficeVersion { get; init; } = string.Empty;

    public string LastWindowsUpdateTitle { get; init; } = string.Empty;

    public DateTimeOffset? LastWindowsUpdateInstalledAt { get; init; }

    public DateTimeOffset CollectedAt { get; init; }
}
