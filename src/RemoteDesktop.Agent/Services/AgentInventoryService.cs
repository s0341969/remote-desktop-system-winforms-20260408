using System.Globalization;
using System.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using RemoteDesktop.Agent.Models;

namespace RemoteDesktop.Agent.Services;

public sealed class AgentInventoryService
{
    private readonly ILogger<AgentInventoryService> _logger;

    public AgentInventoryService(ILogger<AgentInventoryService> logger)
    {
        _logger = logger;
    }

    public AgentInventoryProfile Collect()
    {
        var collectedAt = DateTimeOffset.UtcNow;
        var cpuName = TryCollectCpuName();
        var installedMemoryBytes = TryCollectInstalledMemoryBytes();
        var storageSummary = TryCollectStorageSummary();
        var (osName, osVersion, osBuildNumber) = TryCollectOperatingSystem();
        var officeVersion = TryCollectOfficeVersion();
        var (lastWindowsUpdateTitle, lastWindowsUpdateInstalledAt) = TryCollectLatestWindowsUpdate();

        return new AgentInventoryProfile
        {
            CpuName = cpuName,
            InstalledMemoryBytes = installedMemoryBytes,
            StorageSummary = storageSummary,
            OsName = osName,
            OsVersion = osVersion,
            OsBuildNumber = osBuildNumber,
            OfficeVersion = officeVersion,
            LastWindowsUpdateTitle = lastWindowsUpdateTitle,
            LastWindowsUpdateInstalledAt = lastWindowsUpdateInstalledAt,
            CollectedAt = collectedAt
        };
    }

    private string TryCollectCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (ManagementObject processor in searcher.Get())
            {
                var name = processor["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Collecting CPU name failed.");
        }

        return "未知 CPU";
    }

    private long TryCollectInstalledMemoryBytes()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (ManagementObject computerSystem in searcher.Get())
            {
                var raw = computerSystem["TotalPhysicalMemory"]?.ToString();
                if (long.TryParse(raw, out var bytes) && bytes > 0)
                {
                    return bytes;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Collecting installed memory failed.");
        }

        return 0;
    }

    private string TryCollectStorageSummary()
    {
        try
        {
            var fixedDrives = DriveInfo.GetDrives()
                .Where(static drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
                .OrderBy(static drive => drive.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static drive => $"{drive.Name.TrimEnd(Path.DirectorySeparatorChar)} {FormatBytes(drive.TotalSize)} / 可用 {FormatBytes(drive.AvailableFreeSpace)}")
                .ToArray();

            if (fixedDrives.Length > 0)
            {
                return string.Join(" | ", fixedDrives);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Collecting storage summary failed.");
        }

        return "未知磁碟資訊";
    }

    private (string OsName, string OsVersion, string OsBuildNumber) TryCollectOperatingSystem()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem");
            foreach (ManagementObject operatingSystem in searcher.Get())
            {
                var name = operatingSystem["Caption"]?.ToString()?.Trim();
                var version = operatingSystem["Version"]?.ToString()?.Trim();
                var buildNumber = operatingSystem["BuildNumber"]?.ToString()?.Trim();
                return
                (
                    string.IsNullOrWhiteSpace(name) ? "未知作業系統" : name,
                    string.IsNullOrWhiteSpace(version) ? "未知版本" : version,
                    string.IsNullOrWhiteSpace(buildNumber) ? "未知組建" : buildNumber
                );
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Collecting operating system information failed.");
        }

        return ("未知作業系統", "未知版本", "未知組建");
    }

    private string TryCollectOfficeVersion()
    {
        try
        {
            foreach (var registryView in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
                using var clickToRunConfiguration = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Office\ClickToRun\Configuration");
                if (clickToRunConfiguration is not null)
                {
                    var versionToReport = clickToRunConfiguration.GetValue("VersionToReport")?.ToString()?.Trim();
                    var productIds = clickToRunConfiguration.GetValue("ProductReleaseIds")?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(versionToReport))
                    {
                        return string.IsNullOrWhiteSpace(productIds)
                            ? $"Microsoft Office {versionToReport}"
                            : $"Microsoft Office {productIds} {versionToReport}";
                    }
                }

                using var officeRoot = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Office");
                if (officeRoot is null)
                {
                    continue;
                }

                var officeVersions = officeRoot.GetSubKeyNames()
                    .Where(static item => item.Count(static character => character == '.') == 1)
                    .OrderByDescending(static item => item, StringComparer.OrdinalIgnoreCase);

                foreach (var officeVersion in officeVersions)
                {
                    using var commonInstallRoot = officeRoot.OpenSubKey($@"{officeVersion}\Common\InstallRoot");
                    var path = commonInstallRoot?.GetValue("Path")?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return $"Microsoft Office {officeVersion}";
                    }
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Collecting Office version failed.");
        }

        return "未偵測到 Microsoft Office";
    }

    private (string Title, DateTimeOffset? InstalledAt) TryCollectLatestWindowsUpdate()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT HotFixID, Description, InstalledOn FROM Win32_QuickFixEngineering");
            var latest = searcher.Get()
                .OfType<ManagementObject>()
                .Select(static update => new
                {
                    HotFixId = update["HotFixID"]?.ToString()?.Trim(),
                    Description = update["Description"]?.ToString()?.Trim(),
                    InstalledOn = TryParseInstalledOn(update["InstalledOn"]?.ToString())
                })
                .Where(static update => update.InstalledOn.HasValue)
                .OrderByDescending(static update => update.InstalledOn)
                .FirstOrDefault();

            if (latest is not null)
            {
                var title = string.Join(" - ", new[] { latest.HotFixId, latest.Description }.Where(static part => !string.IsNullOrWhiteSpace(part)));
                return (string.IsNullOrWhiteSpace(title) ? "Windows 更新" : title, latest.InstalledOn);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Collecting latest Windows update failed.");
        }

        return ("未知更新", null);
    }

    private static DateTimeOffset? TryParseInstalledOn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime)
            || DateTime.TryParse(value, CultureInfo.GetCultureInfo("zh-TW"), DateTimeStyles.AssumeLocal, out dateTime)
            || DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.AssumeLocal, out dateTime))
        {
            return new DateTimeOffset(dateTime);
        }

        return null;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "未知";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
