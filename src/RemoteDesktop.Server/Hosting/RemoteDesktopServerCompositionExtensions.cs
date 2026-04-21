using System.ComponentModel.DataAnnotations;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Options;
using RemoteDesktop.Server.Services;
using RemoteDesktop.Server.Services.Auditing;
using RemoteDesktop.Server.Services.Security;
using RemoteDesktop.Server.Services.Settings;
using RemoteDesktop.Server.Services.Users;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Hosting;

public static class RemoteDesktopServerCompositionExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddRemoteDesktopServerCore(this IServiceCollection services)
    {
        services.AddSingleton<DashboardUpdateHub>();
        services.AddSingleton<DeviceBroker>();
        services.AddSingleton<AgentWebSocketHandler>();
        services.AddSingleton<ViewerWebSocketHandler>();
        services.AddSingleton<IAuditLogStore>(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ControlServerOptions>>().Value;
            return string.Equals(options.PersistenceMode, ControlServerOptions.PersistenceModeSqlServer, StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<SqlAuditLogStore>(serviceProvider)
                : ActivatorUtilities.CreateInstance<JsonAuditLogStore>(serviceProvider);
        });
        services.AddSingleton<AuditService>();
        services.AddSingleton<ServerHostSettingsStore>();
        services.AddSingleton<IServerHostSettingsStore>(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ControlServerOptions>>().Value;
            return string.Equals(options.PersistenceMode, ControlServerOptions.PersistenceModeSqlServer, StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<SqlServerHostSettingsStore>(serviceProvider)
                : serviceProvider.GetRequiredService<ServerHostSettingsStore>();
        });
        services.AddSingleton<IUserAccountStore, JsonUserAccountStore>();
        services.AddSingleton<UserAccountService>();
        services.AddSingleton<ConsoleSessionTokenService>();
        services.AddHostedService<AgentMonitorService>();
        return services;
    }

    public static WebApplication MapRemoteDesktopServerEndpoints(this WebApplication app)
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

        app.Map("/ws/viewer", branch =>
        {
            branch.Run(async context =>
            {
                var handler = context.RequestServices.GetRequiredService<ViewerWebSocketHandler>();
                await handler.HandleAsync(context);
            });
        });

        app.Map("/ws/dashboard", branch =>
        {
            branch.Run(async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var sessionTokenService = context.RequestServices.GetRequiredService<ConsoleSessionTokenService>();
                if (!sessionTokenService.TryAuthenticate(context.Request, out _))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                var dashboardUpdateHub = context.RequestServices.GetRequiredService<DashboardUpdateHub>();
                using var subscription = dashboardUpdateHub.Subscribe();
                using var socket = await context.WebSockets.AcceptWebSocketAsync();

                var readyPayload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new DashboardUpdateEnvelope
                {
                    Type = "dashboard-ready",
                    OccurredAt = DateTimeOffset.UtcNow
                }, JsonOptions));

                await socket.SendAsync(readyPayload, WebSocketMessageType.Text, true, context.RequestAborted);

                try
                {
                    await foreach (var update in subscription.Reader.ReadAllAsync(context.RequestAborted))
                    {
                        if (socket.State != WebSocketState.Open)
                        {
                            break;
                        }

                        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(update, JsonOptions));
                        await socket.SendAsync(payload, WebSocketMessageType.Text, true, context.RequestAborted);
                    }
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                {
                }
                catch (WebSocketException) when (context.RequestAborted.IsCancellationRequested || socket.State is WebSocketState.Aborted or WebSocketState.Closed)
                {
                }
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

        app.MapGet("/api/devices", async (HttpContext context, IDeviceRepository repository, ConsoleSessionTokenService sessionTokenService, int? take, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out _))
            {
                return Results.Unauthorized();
            }

            var devices = await repository.GetDevicesAsync(Math.Clamp(take ?? 100, 1, 500), cancellationToken);
            return Results.Ok(devices);
        });

        app.MapGet("/api/devices/{deviceId}", async (HttpContext context, string deviceId, IDeviceRepository repository, ConsoleSessionTokenService sessionTokenService, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out _))
            {
                return Results.Unauthorized();
            }

            var device = await repository.GetDeviceAsync(deviceId, cancellationToken);
            return device is null ? Results.NotFound() : Results.Ok(device);
        });

        app.MapGet("/api/devices/{deviceId}/inventory-history", async (HttpContext context, string deviceId, IDeviceRepository repository, ConsoleSessionTokenService sessionTokenService, int? take, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out _))
            {
                return Results.Unauthorized();
            }

            var items = await repository.GetInventoryHistoryAsync(deviceId, Math.Clamp(take ?? 100, 1, 500), cancellationToken);
            return Results.Ok(items);
        });

        app.MapGet("/api/presence-logs", async (HttpContext context, IDeviceRepository repository, ConsoleSessionTokenService sessionTokenService, int? take, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out _))
            {
                return Results.Unauthorized();
            }

            var logs = await repository.GetPresenceLogsAsync(Math.Clamp(take ?? 100, 1, 500), cancellationToken);
            return Results.Ok(logs);
        });

        app.MapPost("/api/auth/login", async (LoginRequest request, UserAccountService userAccountService, AuditService auditService, ConsoleSessionTokenService sessionTokenService, IOptions<ControlServerOptions> options, CancellationToken cancellationToken) =>
        {
            var session = await userAccountService.AuthenticateAsync(request.UserName, request.Password, cancellationToken);
            if (session is null)
            {
                await auditService.WriteAsync(new AuditLogEntryDto
                {
                    OccurredAt = DateTimeOffset.UtcNow,
                    ActorUserName = request.UserName,
                    ActorDisplayName = request.UserName,
                    Action = "user-sign-in",
                    TargetType = "console",
                    TargetId = options.Value.ConsoleName,
                    Succeeded = false,
                    Details = "帳號或密碼錯誤。 / Invalid user name or password."
                }, cancellationToken);

                return Results.Unauthorized();
            }

            session = sessionTokenService.IssueToken(session);

            await auditService.WriteAsync(new AuditLogEntryDto
            {
                OccurredAt = DateTimeOffset.UtcNow,
                ActorUserName = session.UserName,
                ActorDisplayName = session.DisplayName,
                Action = "user-sign-in",
                TargetType = "console",
                TargetId = options.Value.ConsoleName,
                Succeeded = true,
                Details = "使用者登入成功。 / User signed in successfully."
            }, cancellationToken);

            return Results.Ok(session);
        });

        app.MapGet("/api/users", async (HttpContext context, UserAccountService userAccountService, ConsoleSessionTokenService sessionTokenService, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out var session))
            {
                return Results.Unauthorized();
            }

            if (!sessionTokenService.IsInRole(session, "Administrator"))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var items = await userAccountService.GetAccountsAsync(cancellationToken);
            return Results.Ok(items);
        });

        app.MapPost("/api/users", async (HttpContext context, UserAccountUpsertRequest request, UserAccountService userAccountService, AuditService auditService, ConsoleSessionTokenService sessionTokenService, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out var session))
            {
                return Results.Unauthorized();
            }

            if (!sessionTokenService.IsInRole(session, "Administrator"))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            try
            {
                await userAccountService.SaveAccountAsync(request, cancellationToken);
                await auditService.WriteAsync(new AuditLogEntryDto
                {
                    OccurredAt = DateTimeOffset.UtcNow,
                    ActorUserName = session.UserName,
                    ActorDisplayName = session.DisplayName,
                    Action = "user-account-save",
                    TargetType = "user-account",
                    TargetId = request.UserName,
                    Succeeded = true,
                    Details = $"帳號「{request.UserName}」已以角色「{request.Role}」儲存。 / Account '{request.UserName}' was saved with role '{request.Role}'."
                }, cancellationToken);

                return Results.Ok();
            }
            catch (ValidationException exception)
            {
                return Results.BadRequest(exception.Message);
            }
        });

        app.MapDelete("/api/users/{userName}", async (HttpContext context, string userName, string? currentUserName, UserAccountService userAccountService, AuditService auditService, ConsoleSessionTokenService sessionTokenService, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out var session))
            {
                return Results.Unauthorized();
            }

            if (!sessionTokenService.IsInRole(session, "Administrator"))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            try
            {
                await userAccountService.DeleteAccountAsync(userName, session.UserName, cancellationToken);
                await auditService.WriteAsync(new AuditLogEntryDto
                {
                    OccurredAt = DateTimeOffset.UtcNow,
                    ActorUserName = session.UserName,
                    ActorDisplayName = session.DisplayName,
                    Action = "user-account-delete",
                    TargetType = "user-account",
                    TargetId = userName,
                    Succeeded = true,
                    Details = $"帳號「{userName}」已刪除。 / Account '{userName}' was deleted."
                }, cancellationToken);

                return Results.Ok();
            }
            catch (ValidationException exception)
            {
                return Results.BadRequest(exception.Message);
            }
        });

        app.MapGet("/api/audit-logs", async (HttpContext context, int? take, AuditService auditService, ConsoleSessionTokenService sessionTokenService, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out var session))
            {
                return Results.Unauthorized();
            }

            if (!sessionTokenService.IsInRole(session, "Administrator"))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var items = await auditService.GetRecentAsync(Math.Clamp(take ?? 250, 1, 1000), cancellationToken);
            return Results.Ok(items);
        });

        app.MapGet("/api/settings/host", async (HttpContext context, IServerHostSettingsStore hostSettingsStore, ConsoleSessionTokenService sessionTokenService, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out var session))
            {
                return Results.Unauthorized();
            }

            if (!sessionTokenService.IsInRole(session, "Administrator"))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var settings = await hostSettingsStore.LoadAsync(cancellationToken);
            return Results.Ok(settings);
        });

        app.MapPost("/api/settings/host", async (HttpContext context, HostSettingsDto request, IServerHostSettingsStore hostSettingsStore, AuditService auditService, ConsoleSessionTokenService sessionTokenService, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out var session))
            {
                return Results.Unauthorized();
            }

            if (!sessionTokenService.IsInRole(session, "Administrator"))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            try
            {
                await hostSettingsStore.SaveAsync(request, cancellationToken);
                await auditService.WriteAsync(new AuditLogEntryDto
                {
                    OccurredAt = DateTimeOffset.UtcNow,
                    ActorUserName = session.UserName,
                    ActorDisplayName = session.DisplayName,
                    Action = "host-settings-save",
                    TargetType = "host-settings",
                    TargetId = request.ConsoleName,
                    Succeeded = true,
                    Details = "中央 Host 設定已儲存。 / Central host settings were saved."
                }, cancellationToken);

                return Results.Ok();
            }
            catch (ValidationException exception)
            {
                return Results.BadRequest(exception.Message);
            }
        });

        app.MapPost("/api/audit-logs", async (HttpContext context, AuditLogEntryDto entry, AuditService auditService, ConsoleSessionTokenService sessionTokenService, CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out var session))
            {
                return Results.Unauthorized();
            }

            await auditService.WriteAsync(new AuditLogEntryDto
            {
                Id = entry.Id,
                OccurredAt = entry.OccurredAt,
                ActorUserName = session.UserName,
                ActorDisplayName = session.DisplayName,
                Action = entry.Action,
                TargetType = entry.TargetType,
                TargetId = entry.TargetId,
                Succeeded = entry.Succeeded,
                Details = entry.Details
            }, cancellationToken);
            return Results.Ok();
        });

        app.MapPost("/api/devices/{deviceId}/authorization", async (
            HttpContext context,
            string deviceId,
            bool isAuthorized,
            string changedBy,
            DeviceBroker broker,
            ConsoleSessionTokenService sessionTokenService,
            CancellationToken cancellationToken) =>
        {
            if (!sessionTokenService.TryAuthenticate(context.Request, out var session))
            {
                return Results.Unauthorized();
            }

            if (!sessionTokenService.IsInRole(session, "Administrator"))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return Results.BadRequest(new
                {
                    message = "deviceId is required."
                });
            }

            var success = await broker.SetDeviceAuthorizationAsync(deviceId, isAuthorized, session.UserName, cancellationToken);
            if (!success)
            {
                return Results.NotFound(new
                {
                    message = $"Device '{deviceId}' was not found."
                });
            }

            return Results.Ok(new
            {
                deviceId,
                isAuthorized
            });
        });

        return app;
    }
}


