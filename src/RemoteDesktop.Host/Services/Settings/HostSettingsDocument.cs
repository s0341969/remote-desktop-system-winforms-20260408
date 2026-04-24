using System.ComponentModel.DataAnnotations;

namespace RemoteDesktop.Host.Services.Settings;

public sealed class HostSettingsDocument
{
    public bool IsCentralSettingsAvailable { get; set; } = true;

    public bool IsLocalFallbackMode { get; set; }

    public string? SettingsStatusMessage { get; set; }

    public bool EnableDatabase { get; set; }

    public string RemoteDesktopDbConnectionString { get; set; } = "Server=(localdb)\\MSSQLLocalDB;Database=RemoteDesktopControl;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;";

    [Required]
    [Url]
    public string ServerUrl { get; set; } = "http://localhost:5106";

    [Url]
    public string? CentralServerUrl { get; set; }

    [Required]
    [MinLength(3)]
    public string ConsoleName { get; set; } = "RemoteDesk Control";

    [Required]
    [MinLength(3)]
    public string AdminUserName { get; set; } = "admin";

    [Required]
    [MinLength(10)]
    public string AdminPassword { get; set; } = "ChangeMe!2026";

    [Required]
    [MinLength(12)]
    public string SharedAccessKey { get; set; } = "ChangeMe-Agent-Key";

    public bool RequireHttpsRedirect { get; set; }

    [Range(60, 600)]
    public int AgentHeartbeatTimeoutSeconds { get; set; } = 180;
}
