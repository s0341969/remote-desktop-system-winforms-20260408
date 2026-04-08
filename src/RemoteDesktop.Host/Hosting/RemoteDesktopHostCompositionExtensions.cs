using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RemoteDesktop.Host.Forms;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Hosting;

public static class RemoteDesktopHostCompositionExtensions
{
    public static IServiceCollection AddRemoteDesktopHostCore(this IServiceCollection services)
    {
        services.AddSingleton<DeviceBroker>();
        services.AddSingleton<AgentWebSocketHandler>();
        services.AddSingleton<ViewerWebSocketHandler>();
        services.AddSingleton<CredentialValidator>();
        services.AddSingleton<LoginFormFactory>();
        services.AddSingleton<MainFormFactory>();
        services.AddSingleton<RemoteViewerFormFactory>();
        services.AddHostedService<AgentMonitorService>();
        return services;
    }

    public static WebApplication MapRemoteDesktopHostEndpoints(this WebApplication app)
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

        app.MapGet("/healthz", async (IDeviceRepository repository, CancellationToken cancellationToken) =>
        {
            var devices = await repository.GetDevicesAsync(200, cancellationToken);
            return Results.Ok(new
            {
                status = "ok",
                onlineDevices = devices.Count(static item => item.IsOnline),
                totalDevices = devices.Count
            });
        });

        return app;
    }
}
