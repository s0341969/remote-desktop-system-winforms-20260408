using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<ControlServerOptions>()
    .Bind(builder.Configuration.GetSection(ControlServerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.LogoutPath = "/Login?handler=Logout";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.Cookie.Name = "remote_desk_admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});

builder.Services.AddSingleton<IDeviceRepository, SqlDeviceRepository>();
builder.Services.AddSingleton<DeviceBroker>();
builder.Services.AddSingleton<AgentWebSocketHandler>();
builder.Services.AddSingleton<ViewerWebSocketHandler>();
builder.Services.AddHostedService<AgentMonitorService>();

var app = builder.Build();

var options = app.Services.GetRequiredService<IOptions<ControlServerOptions>>().Value;
if (options.RequireHttpsRedirect)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

await app.Services.GetRequiredService<IDeviceRepository>().InitializeSchemaAsync(app.Lifetime.ApplicationStopping);

app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
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

app.Map("/ws/viewer", branch =>
{
    branch.Run(async context =>
    {
        var handler = context.RequestServices.GetRequiredService<ViewerWebSocketHandler>();
        await handler.HandleAsync(context);
    });
});

app.MapGet("/healthz", async (IDeviceRepository repository, CancellationToken cancellationToken) =>
{
    var devices = await repository.GetDevicesAsync(20, cancellationToken);
    return Results.Ok(new
    {
        status = "ok",
        onlineDevices = devices.Count(static item => item.IsOnline),
        totalDevices = devices.Count
    });
});

app.MapRazorPages();

app.Run();
