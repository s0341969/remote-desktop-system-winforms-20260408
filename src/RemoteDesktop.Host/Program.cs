using System.Windows.Forms;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteDesktop.Host.Forms;
using RemoteDesktop.Host.Hosting;
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
            .Validate(static options =>
                string.Equals(options.PersistenceMode, ControlServerOptions.PersistenceModeMemory, StringComparison.OrdinalIgnoreCase)
                || string.Equals(options.PersistenceMode, ControlServerOptions.PersistenceModeSqlServer, StringComparison.OrdinalIgnoreCase),
                "ControlServer:PersistenceMode 僅支援 Memory 或 SqlServer。")
            .ValidateOnStart();

        if (string.Equals(configuredOptions.PersistenceMode, ControlServerOptions.PersistenceModeSqlServer, StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IDeviceRepository, SqlDeviceRepository>();
        }
        else
        {
            builder.Services.AddSingleton<IDeviceRepository, InMemoryDeviceRepository>();
        }

        builder.Services.AddRemoteDesktopHostCore();

        await using var app = builder.Build();
        app.MapRemoteDesktopHostEndpoints();

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
