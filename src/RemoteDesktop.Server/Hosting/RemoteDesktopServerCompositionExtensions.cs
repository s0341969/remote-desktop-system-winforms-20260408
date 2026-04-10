using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Options;
using RemoteDesktop.Server.Services;

namespace RemoteDesktop.Server.Hosting;

public static class RemoteDesktopServerCompositionExtensions
{
    public static IServiceCollection AddRemoteDesktopServerCore(this IServiceCollection services)
    {
        services.AddSingleton<DeviceBroker>();
        services.AddSingleton<AgentWebSocketHandler>();
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

        return app;
    }
}
