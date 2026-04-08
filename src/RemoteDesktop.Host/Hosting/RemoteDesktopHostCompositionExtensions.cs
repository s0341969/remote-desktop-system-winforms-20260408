using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Forms;
using RemoteDesktop.Host.Forms.Audit;
using RemoteDesktop.Host.Forms.Settings;
using RemoteDesktop.Host.Forms.Users;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;
using RemoteDesktop.Host.Services.Auditing;
using RemoteDesktop.Host.Services.Settings;
using RemoteDesktop.Host.Services.Users;

namespace RemoteDesktop.Host.Hosting;

public static class RemoteDesktopHostCompositionExtensions
{
    public static IServiceCollection AddRemoteDesktopHostCore(this IServiceCollection services)
    {
        services.AddSingleton<DeviceBroker>();
        services.AddSingleton<AgentWebSocketHandler>();
        services.AddSingleton<ViewerWebSocketHandler>();
        services.AddSingleton<CredentialValidator>();
        services.AddSingleton<IAuditLogStore, JsonAuditLogStore>();
        services.AddSingleton<AuditService>();
        services.AddSingleton<IUserAccountStore, JsonUserAccountStore>();
        services.AddSingleton<AuthenticationService>();
        services.AddSingleton<IHostSettingsStore, HostSettingsStore>();
        services.AddSingleton<LoginFormFactory>();
        services.AddSingleton<MainFormFactory>();
        services.AddSingleton<RemoteViewerFormFactory>();
        services.AddSingleton<HostSettingsFormFactory>();
        services.AddSingleton<UserManagementFormFactory>();
        services.AddSingleton<AuditLogFormFactory>();
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
