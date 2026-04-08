using System.Windows.Forms;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
                "ControlServer:PersistenceMode must be either Memory or SqlServer.")
            .ValidateOnStart();

        builder.Services.AddSingleton<IDeviceRepository>(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ControlServerOptions>>().Value;
            return string.Equals(options.PersistenceMode, ControlServerOptions.PersistenceModeSqlServer, StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<SqlDeviceRepository>(serviceProvider)
                : ActivatorUtilities.CreateInstance<InMemoryDeviceRepository>(serviceProvider);
        });

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

            if (loginForm.AuthenticatedUser is null)
            {
                MessageBox.Show(
                    HostUiText.Bi("登入完成但未建立使用者工作階段。", "Sign-in completed without a user session."),
                    HostUiText.Window("啟動錯誤", "Startup Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            using var mainForm = app.Services.GetRequiredService<MainFormFactory>().Create(loginForm.AuthenticatedUser);
            Application.Run(mainForm);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                HostUiText.Bi($"Host 啟動失敗：{exception.Message}", $"Host startup failed: {exception.Message}"),
                HostUiText.Window("啟動錯誤", "Startup Error"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
