using System.ComponentModel.DataAnnotations;

namespace RemoteDesktop.Agent.Services.Settings;

public sealed class AgentSettingsDocument
{
    private static readonly string MachineIdentity = Agent.Services.AgentIdentity.GetMachineIdentity();

    [Required]
    [Url]
    public string ServerUrl { get; set; } = "http://localhost:5106";

    [Required]
    [MinLength(3)]
    public string DeviceId { get; set; } = MachineIdentity;

    [Required]
    [MinLength(3)]
    public string DeviceName { get; set; } = MachineIdentity;

    [Required]
    [MinLength(12)]
    public string SharedAccessKey { get; set; } = "ChangeMe-Agent-Key";

    public string FileTransferDirectory { get; set; } = string.Empty;

    [Range(1, 24)]
    public int CaptureFramesPerSecond { get; set; } = 8;

    [Range(30, 90)]
    public long JpegQuality { get; set; } = 55;

    [Range(640, 3840)]
    public int MaxFrameWidth { get; set; } = 1600;

    [Range(1, 60)]
    public int ReconnectDelaySeconds { get; set; } = 5;
}
