namespace RemoteDesktop.Agent.Models;

public sealed record DesktopFrame(byte[] Payload, int Width, int Height);
