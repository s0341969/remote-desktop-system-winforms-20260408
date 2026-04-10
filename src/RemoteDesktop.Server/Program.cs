using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Hosting;
using RemoteDesktop.Server.Options;
using RemoteDesktop.Server.Services;

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

builder.Services.AddRemoteDesktopServerCore();

await using var app = builder.Build();
app.MapRemoteDesktopServerEndpoints();
await app.Services.GetRequiredService<IDeviceRepository>().InitializeSchemaAsync(CancellationToken.None);
await app.RunAsync();
