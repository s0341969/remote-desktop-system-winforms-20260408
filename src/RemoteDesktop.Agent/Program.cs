using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteDesktop.Agent.Options;
using RemoteDesktop.Agent.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<DesktopCaptureService>();
builder.Services.AddSingleton<InputInjectionService>();
builder.Services.AddHostedService<RemoteAgentService>();

var host = builder.Build();
await host.RunAsync();
