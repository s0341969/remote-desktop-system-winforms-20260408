using RemoteDesktop.Host.Models;

namespace RemoteDesktop.Host.Services;

public interface IDeviceRepository
{
    Task InitializeSchemaAsync(CancellationToken cancellationToken);

    Task UpsertDeviceOnlineAsync(AgentDescriptor descriptor, CancellationToken cancellationToken);

    Task<Guid> StartPresenceAsync(AgentDescriptor descriptor, CancellationToken cancellationToken);

    Task TouchPresenceAsync(Guid presenceId, string deviceId, int screenWidth, int screenHeight, CancellationToken cancellationToken);

    Task ClosePresenceAsync(Guid presenceId, string deviceId, string reason, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken);

    Task<DeviceRecord?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken);
}
