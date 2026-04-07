using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteDesktop.Agent.Forms;
using RemoteDesktop.Agent.Options;
using RemoteDesktop.Agent.Services;

namespace RemoteDesktop.Agent;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services
            .AddOptions<AgentOptions>()
            .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<AgentRuntimeState>();
        builder.Services.AddSingleton<DesktopCaptureService>();
        builder.Services.AddSingleton<InputInjectionService>();
        builder.Services.AddSingleton<AgentMainFormFactory>();
        builder.Services.AddHostedService<RemoteAgentService>();

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
                $"Agent 啟動失敗：{exception.Message}",
                "RemoteDesktop.Agent",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
