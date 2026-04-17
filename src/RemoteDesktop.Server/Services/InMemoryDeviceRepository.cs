using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services;

public sealed class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DeviceRecord> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, AgentPresenceLogRecord> _presenceLogs = new();
    private readonly Dictionary<string, List<InventoryHistoryRecord>> _inventoryHistory = new(StringComparer.OrdinalIgnoreCase);

    public Task InitializeSchemaAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task UpsertDeviceOnlineAsync(AgentDescriptor descriptor, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            var createdAt = _devices.TryGetValue(descriptor.DeviceId, out var existing)
                ? existing.CreatedAt
                : now;

            _devices[descriptor.DeviceId] = new DeviceRecord
            {
                DeviceId = descriptor.DeviceId,
                DeviceName = descriptor.DeviceName,
                HostName = descriptor.HostName,
                AgentVersion = descriptor.AgentVersion,
                ScreenWidth = descriptor.ScreenWidth,
                ScreenHeight = descriptor.ScreenHeight,
                Inventory = descriptor.Inventory,
                IsOnline = true,
                IsAuthorized = existing?.IsAuthorized ?? false,
                AuthorizedAt = existing?.AuthorizedAt,
                AuthorizedBy = existing?.AuthorizedBy,
                CreatedAt = createdAt,
                LastSeenAt = now,
                LastConnectedAt = now,
                LastDisconnectedAt = null
            };

            TrackInventoryChangeLocked(descriptor.DeviceId, existing?.Inventory, descriptor.Inventory);
        }

        return Task.CompletedTask;
    }

    public Task<Guid> StartPresenceAsync(AgentDescriptor descriptor, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            _presenceLogs[id] = new AgentPresenceLogRecord
            {
                PresenceId = id,
                DeviceId = descriptor.DeviceId,
                DeviceName = descriptor.DeviceName,
                HostName = descriptor.HostName,
                AgentVersion = descriptor.AgentVersion,
                ConnectedAt = now,
                LastSeenAt = now,
                OnlineSeconds = 0
            };
        }

        return Task.FromResult(id);
    }

    public Task TouchPresenceAsync(Guid presenceId, string deviceId, int screenWidth, int screenHeight, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (_devices.TryGetValue(deviceId, out var device))
            {
                _devices[deviceId] = new DeviceRecord
                {
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName,
                    HostName = device.HostName,
                    AgentVersion = device.AgentVersion,
                    ScreenWidth = screenWidth,
                    ScreenHeight = screenHeight,
                    Inventory = device.Inventory,
                    IsOnline = true,
                    IsAuthorized = device.IsAuthorized,
                    AuthorizedAt = device.AuthorizedAt,
                    AuthorizedBy = device.AuthorizedBy,
                    CreatedAt = device.CreatedAt,
                    LastSeenAt = now,
                    LastConnectedAt = device.LastConnectedAt,
                    LastDisconnectedAt = device.LastDisconnectedAt
                };
            }

            if (_presenceLogs.TryGetValue(presenceId, out var log))
            {
                _presenceLogs[presenceId] = new AgentPresenceLogRecord
                {
                    PresenceId = log.PresenceId,
                    DeviceId = log.DeviceId,
                    DeviceName = log.DeviceName,
                    HostName = log.HostName,
                    AgentVersion = log.AgentVersion,
                    ConnectedAt = log.ConnectedAt,
                    LastSeenAt = now,
                    DisconnectedAt = log.DisconnectedAt,
                    DisconnectReason = log.DisconnectReason,
                    OnlineSeconds = (long)Math.Max(0, (now - log.ConnectedAt).TotalSeconds)
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task ClosePresenceAsync(Guid presenceId, string deviceId, string reason, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (_devices.TryGetValue(deviceId, out var device))
            {
                _devices[deviceId] = new DeviceRecord
                {
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName,
                    HostName = device.HostName,
                    AgentVersion = device.AgentVersion,
                    ScreenWidth = device.ScreenWidth,
                    ScreenHeight = device.ScreenHeight,
                    Inventory = device.Inventory,
                    IsOnline = false,
                    IsAuthorized = device.IsAuthorized,
                    AuthorizedAt = device.AuthorizedAt,
                    AuthorizedBy = device.AuthorizedBy,
                    CreatedAt = device.CreatedAt,
                    LastSeenAt = now,
                    LastConnectedAt = device.LastConnectedAt,
                    LastDisconnectedAt = now
                };
            }

            if (_presenceLogs.TryGetValue(presenceId, out var log))
            {
                _presenceLogs[presenceId] = new AgentPresenceLogRecord
                {
                    PresenceId = log.PresenceId,
                    DeviceId = log.DeviceId,
                    DeviceName = log.DeviceName,
                    HostName = log.HostName,
                    AgentVersion = log.AgentVersion,
                    ConnectedAt = log.ConnectedAt,
                    LastSeenAt = now,
                    DisconnectedAt = now,
                    DisconnectReason = reason,
                    OnlineSeconds = (long)Math.Max(0, (now - log.ConnectedAt).TotalSeconds)
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task SetDeviceAuthorizationAsync(string deviceId, bool isAuthorized, string changedByUserName, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!_devices.TryGetValue(deviceId, out var device))
            {
                return Task.CompletedTask;
            }

            var authorizedAt = isAuthorized ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
            _devices[deviceId] = new DeviceRecord
            {
                DeviceId = device.DeviceId,
                DeviceName = device.DeviceName,
                HostName = device.HostName,
                AgentVersion = device.AgentVersion,
                ScreenWidth = device.ScreenWidth,
                ScreenHeight = device.ScreenHeight,
                Inventory = device.Inventory,
                IsOnline = device.IsOnline,
                IsAuthorized = isAuthorized,
                AuthorizedAt = authorizedAt,
                AuthorizedBy = isAuthorized ? changedByUserName : null,
                CreatedAt = device.CreatedAt,
                LastSeenAt = device.LastSeenAt,
                LastConnectedAt = device.LastConnectedAt,
                LastDisconnectedAt = device.LastDisconnectedAt
            };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            IReadOnlyList<DeviceRecord> result = _devices.Values
                .OrderByDescending(static item => item.IsOnline)
                .ThenByDescending(static item => item.LastSeenAt)
                .Take(take)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<DeviceRecord?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _devices.TryGetValue(deviceId, out var device);
            return Task.FromResult(device);
        }
    }

    public Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            IReadOnlyList<AgentPresenceLogRecord> result = _presenceLogs.Values
                .OrderByDescending(static item => item.ConnectedAt)
                .Take(take)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task UpdateInventoryAsync(string deviceId, AgentInventoryProfile inventory, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!_devices.TryGetValue(deviceId, out var device))
            {
                return Task.CompletedTask;
            }

            _devices[deviceId] = new DeviceRecord
            {
                DeviceId = device.DeviceId,
                DeviceName = device.DeviceName,
                HostName = device.HostName,
                AgentVersion = device.AgentVersion,
                ScreenWidth = device.ScreenWidth,
                ScreenHeight = device.ScreenHeight,
                Inventory = inventory,
                IsOnline = device.IsOnline,
                IsAuthorized = device.IsAuthorized,
                AuthorizedAt = device.AuthorizedAt,
                AuthorizedBy = device.AuthorizedBy,
                CreatedAt = device.CreatedAt,
                LastSeenAt = device.LastSeenAt,
                LastConnectedAt = device.LastConnectedAt,
                LastDisconnectedAt = device.LastDisconnectedAt
            };

            TrackInventoryChangeLocked(deviceId, device.Inventory, inventory);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InventoryHistoryRecord>> GetInventoryHistoryAsync(string deviceId, int take, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!_inventoryHistory.TryGetValue(deviceId, out var items))
            {
                return Task.FromResult<IReadOnlyList<InventoryHistoryRecord>>([]);
            }

            return Task.FromResult<IReadOnlyList<InventoryHistoryRecord>>(items
                .OrderByDescending(static item => item.RecordedAt)
                .Take(take)
                .ToList());
        }
    }

    private void TrackInventoryChangeLocked(string deviceId, AgentInventoryProfile? previousInventory, AgentInventoryProfile? currentInventory)
    {
        if (currentInventory is null)
        {
            return;
        }

        var fingerprint = CalculateFingerprint(currentInventory);
        var previousFingerprint = previousInventory is null ? null : CalculateFingerprint(previousInventory);
        if (string.Equals(fingerprint, previousFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        if (!_inventoryHistory.TryGetValue(deviceId, out var items))
        {
            items = [];
            _inventoryHistory[deviceId] = items;
        }

        items.Add(new InventoryHistoryRecord
        {
            HistoryId = Guid.NewGuid(),
            DeviceId = deviceId,
            InventoryFingerprint = fingerprint,
            ChangeSummary = BuildChangeSummary(previousInventory, currentInventory),
            CollectedAt = currentInventory.CollectedAt,
            RecordedAt = DateTimeOffset.UtcNow,
            Inventory = currentInventory
        });
    }

    private static string CalculateFingerprint(AgentInventoryProfile inventory)
    {
        var payload = string.Join("|", new[]
        {
            inventory.CpuName,
            inventory.InstalledMemoryBytes.ToString(),
            inventory.StorageSummary,
            inventory.OsName,
            inventory.OsVersion,
            inventory.OsBuildNumber,
            inventory.OfficeVersion,
            inventory.LastWindowsUpdateTitle,
            inventory.LastWindowsUpdateInstalledAt?.UtcDateTime.ToString("O") ?? string.Empty
        });

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)));
    }

    private static string BuildChangeSummary(AgentInventoryProfile? previousInventory, AgentInventoryProfile currentInventory)
    {
        if (previousInventory is null)
        {
            return "首次盤點。 / Initial inventory snapshot.";
        }

        var changes = new List<string>();
        AppendChange(changes, "CPU", previousInventory.CpuName, currentInventory.CpuName);
        AppendChange(changes, "記憶體", FormatBytes(previousInventory.InstalledMemoryBytes), FormatBytes(currentInventory.InstalledMemoryBytes));
        AppendChange(changes, "磁碟", previousInventory.StorageSummary, currentInventory.StorageSummary);
        AppendChange(changes, "作業系統", $"{previousInventory.OsName} {previousInventory.OsVersion} ({previousInventory.OsBuildNumber})", $"{currentInventory.OsName} {currentInventory.OsVersion} ({currentInventory.OsBuildNumber})");
        AppendChange(changes, "Office", previousInventory.OfficeVersion, currentInventory.OfficeVersion);
        AppendChange(changes, "最後更新", $"{previousInventory.LastWindowsUpdateTitle} {previousInventory.LastWindowsUpdateInstalledAt:yyyy-MM-dd}", $"{currentInventory.LastWindowsUpdateTitle} {currentInventory.LastWindowsUpdateInstalledAt:yyyy-MM-dd}");

        return changes.Count == 0
            ? "盤點時間更新，內容無變更。 / Inventory refreshed without content changes."
            : string.Join("；", changes);
    }

    private static void AppendChange(List<string> changes, string label, string? before, string? after)
    {
        var normalizedBefore = string.IsNullOrWhiteSpace(before) ? "-" : before.Trim();
        var normalizedAfter = string.IsNullOrWhiteSpace(after) ? "-" : after.Trim();
        if (string.Equals(normalizedBefore, normalizedAfter, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add($"{label}: {normalizedBefore} -> {normalizedAfter}");
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

