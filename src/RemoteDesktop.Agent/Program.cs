using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteDesktop.Agent.Forms;
using RemoteDesktop.Agent.Forms.Settings;
using RemoteDesktop.Agent.Options;
using RemoteDesktop.Agent.Services;
using RemoteDesktop.Agent.Services.Settings;

namespace RemoteDesktop.Agent;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        InitializeWindowsFormsApplication();

        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services
                    .AddOptions<AgentOptions>()
                    .Bind(context.Configuration.GetSection(AgentOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddSingleton<AgentRuntimeState>();
                services.AddSingleton<AgentInventoryService>();
                services.AddSingleton<DesktopCaptureService>();
                services.AddSingleton<InteractiveSessionRecoveryService>();
                services.AddSingleton<ClipboardSyncService>();
                services.AddSingleton<InputInjectionService>();
                services.AddSingleton<FileTransferTraceService>();
                services.AddSingleton<FileTransferService>();
                services.AddSingleton<IAgentSettingsStore, AgentSettingsStore>();
                services.AddSingleton<AgentMainFormFactory>();
                services.AddSingleton<AgentSettingsFormFactory>();
                services.AddHostedService<RemoteAgentService>();
            });

        using var host = builder.Build();

        try
        {
            host.Services.GetRequiredService<AgentRuntimeState>().MarkStarting();
            await host.StartAsync();

            using var form = host.Services.GetRequiredService<AgentMainFormFactory>().Create();
            Application.Run(form);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                AgentUiText.Bi($"Agent 啟動失敗：{exception.Message}", $"Agent startup failed: {exception.Message}"),
                AgentUiText.Window("啟動錯誤", "Startup Error"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static void InitializeWindowsFormsApplication()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
    }
}
