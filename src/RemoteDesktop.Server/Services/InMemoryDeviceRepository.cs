using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services;

public sealed class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DeviceRecord> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, AgentPresenceLogRecord> _presenceLogs = new();

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
}

