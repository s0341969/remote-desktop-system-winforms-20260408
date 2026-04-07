using System.ComponentModel.DataAnnotations;

namespace RemoteDesktop.Host.Options;

public sealed class ControlServerOptions
{
    public const string SectionName = "ControlServer";

    [Required]
    [MinLength(3)]
    public string ConsoleName { get; init; } = "RemoteDesk Control";

    [Required]
    [MinLength(3)]
    public string AdminUserName { get; init; } = "admin";

    [Required]
    [MinLength(10)]
    public string AdminPassword { get; init; } = "ChangeMe!2026";

    [Required]
    [MinLength(12)]
    public string SharedAccessKey { get; init; } = "ChangeMe-Agent-Key";

    public bool RequireHttpsRedirect { get; init; }

    [Range(15, 300)]
    public int AgentHeartbeatTimeoutSeconds { get; init; } = 45;
}
