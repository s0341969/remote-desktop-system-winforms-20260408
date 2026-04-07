using System.Windows.Forms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteDesktop.Host.Forms;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var builder = WebApplication.CreateBuilder(args);
        var configuredOptions = builder.Configuration.GetSection(ControlServerOptions.SectionName).Get<ControlServerOptions>() ?? new ControlServerOptions();

        builder.WebHost.UseUrls(configuredOptions.ServerUrl);

        builder.Services
            .AddOptions<ControlServerOptions>()
            .Bind(builder.Configuration.GetSection(ControlServerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IDeviceRepository, SqlDeviceRepository>();
        builder.Services.AddSingleton<DeviceBroker>();
        builder.Services.AddSingleton<AgentWebSocketHandler>();
        builder.Services.AddSingleton<ViewerWebSocketHandler>();
        builder.Services.AddSingleton<CredentialValidator>();
        builder.Services.AddSingleton<LoginFormFactory>();
        builder.Services.AddSingleton<MainFormFactory>();
        builder.Services.AddSingleton<RemoteViewerFormFactory>();
        builder.Services.AddHostedService<AgentMonitorService>();

        await using var app = builder.Build();

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

        try
        {
            await app.Services.GetRequiredService<IDeviceRepository>().InitializeSchemaAsync(CancellationToken.None);
            await app.StartAsync();

            using var loginForm = app.Services.GetRequiredService<LoginFormFactory>().Create();
            if (loginForm.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            using var mainForm = app.Services.GetRequiredService<MainFormFactory>().Create(loginForm.AuthenticatedUserName);
            Application.Run(mainForm);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"主控台啟動失敗：{exception.Message}",
                "RemoteDesktop.Host",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
