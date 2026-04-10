using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Options;
using RemoteDesktop.Server.Services;
using RemoteDesktop.Server.Services.Auditing;
using RemoteDesktop.Server.Services.Security;
using RemoteDesktop.Server.Services.Users;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Hosting;

public static class RemoteDesktopServerCompositionExtensions
{
    public static IServiceCollection AddRemoteDesktopServerCore(this IServiceCollection services)
    {
        services.AddSingleton<DeviceBroker>();
        services.AddSingleton<AgentWebSocketHandler>();
        services.AddSingleton<ViewerWebSocketHandler>();
        services.AddSingleton<IAuditLogStore, JsonAuditLogStore>();
        services.AddSingleton<AuditService>();
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
