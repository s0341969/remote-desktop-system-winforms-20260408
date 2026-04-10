using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Options;
using RemoteDesktop.Server.Services;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Hosting;

public static class RemoteDesktopServerCompositionExtensions
{
    public static IServiceCollection AddRemoteDesktopServerCore(this IServiceCollection services)
    {
        services.AddSingleton<DeviceBroker>();
        services.AddSingleton<AgentWebSocketHandler>();
        services.AddSingleton<ViewerWebSocketHandler>();
        services.AddHostedService<AgentMonitorService>();
        return services;
    }

    public static WebApplication MapRemoteDesktopServerEndpoints(this WebApplication app)
    {
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(20)
        });

        app.Map("/ws/agent", branch =>
        {
            branch.Run(async context =>
            {
                var handler = context.RequestServices.GetRequiredService<AgentWebSocketHandler>();
                await handler.HandleAsync(context);
            });
        });

        app.Map("/ws/viewer", branch =>
        {
            branch.Run(async context =>
            {
                var handler = context.RequestServices.GetRequiredService<ViewerWebSocketHandler>();
                await handler.HandleAsync(context);
            });
        });

        app.MapGet("/healthz", async (IDeviceRepository repository, IOptions<ControlServerOptions> options, CancellationToken cancellationToken) =>
        {
            var devices = await repository.GetDevicesAsync(200, cancellationToken);
            return Results.Ok(new
            {
                status = "ok",
                persistenceMode = options.Value.PersistenceMode,
                onlineDevices = devices.Count(static item => item.IsOnline),
                totalDevices = devices.Count
            });
        });

        app.MapGet("/api/devices", async (IDeviceRepository repository, int? take, CancellationToken cancellationToken) =>
        {
            var devices = await repository.GetDevicesAsync(Math.Clamp(take ?? 100, 1, 500), cancellationToken);
            return Results.Ok(devices);
        });

        app.MapGet("/api/presence-logs", async (IDeviceRepository repository, int? take, CancellationToken cancellationToken) =>
        {
            var logs = await repository.GetPresenceLogsAsync(Math.Clamp(take ?? 100, 1, 500), cancellationToken);
            return Results.Ok(logs);
        });

        app.MapPost("/api/devices/{deviceId}/authorization", async (
            string deviceId,
            bool isAuthorized,
            string changedBy,
            DeviceBroker broker,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(changedBy))
            {
                return Results.BadRequest(new
                {
                    message = "deviceId and changedBy are required."
                });
            }

            var success = await broker.SetDeviceAuthorizationAsync(deviceId, isAuthorized, changedBy, cancellationToken);
            if (!success)
            {
                return Results.NotFound(new
                {
                    message = $"Device '{deviceId}' was not found."
                });
            }

            return Results.Ok(new
            {
                deviceId,
                isAuthorized
            });
        });

        return app;
    }
}
