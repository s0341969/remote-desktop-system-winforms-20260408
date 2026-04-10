using System.ComponentModel.DataAnnotations;

namespace RemoteDesktop.Host.Options;

public sealed class ControlServerOptions : IValidatableObject
{
    public const string SectionName = "ControlServer";

    public const string PersistenceModeMemory = "Memory";
    public const string PersistenceModeSqlServer = "SqlServer";

    [Required]
    [Url]
    public string ServerUrl { get; init; } = "http://localhost:5106";

    public string? CentralServerUrl { get; init; }

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

    [Required]
    public string PersistenceMode { get; init; } = PersistenceModeMemory;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(CentralServerUrl))
        {
            if (!Uri.TryCreate(CentralServerUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps &&
                 uri.Scheme != Uri.UriSchemeFtp))
            {
                yield return new ValidationResult(
                    "CentralServerUrl must be empty or a valid absolute http, https, or ftp URL.",
                    [nameof(CentralServerUrl)]);
            }
        }

        if (!string.Equals(PersistenceMode, PersistenceModeMemory, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(PersistenceMode, PersistenceModeSqlServer, StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"PersistenceMode must be '{PersistenceModeMemory}' or '{PersistenceModeSqlServer}'.",
                [nameof(PersistenceMode)]);
        }
    }
}
