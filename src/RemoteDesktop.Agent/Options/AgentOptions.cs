using System.ComponentModel.DataAnnotations;

namespace RemoteDesktop.Agent.Options;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    [Required]
    [Url]
    public string ServerUrl { get; init; } = "http://localhost:5106";

    [Required]
    [MinLength(3)]
    public string DeviceId { get; init; } = "device-demo-001";

    [Required]
    [MinLength(3)]
    public string DeviceName { get; init; } = "Demo Operator PC";

    [Required]
    [MinLength(12)]
    public string SharedAccessKey { get; init; } = "ChangeMe-Agent-Key";

    public string FileTransferDirectory { get; init; } = string.Empty;

    [Range(1, 24)]
    public int CaptureFramesPerSecond { get; init; } = 8;

    [Range(30, 90)]
    public long JpegQuality { get; init; } = 55;

    [Range(640, 3840)]
    public int MaxFrameWidth { get; init; } = 1600;

    [Range(1, 60)]
    public int ReconnectDelaySeconds { get; init; } = 5;

    [Range(5, 1440)]
    public int InventoryRefreshMinutes { get; init; } = 360;

    public bool AutoRecoverInteractiveSessionOnWindowsServer { get; init; } = true;

    public bool StartHidden { get; init; } = true;

    public bool ShowTrayIcon { get; init; } = true;
}
