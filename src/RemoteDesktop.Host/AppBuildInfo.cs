using System.Reflection;

namespace RemoteDesktop.Host;

internal static class AppBuildInfo
{
    private static readonly Lazy<string> DisplayValue = new(CreateDisplay);

    public static string Display => DisplayValue.Value;

    public static string AppendToWindowTitle(string title)
    {
        return $"{title} [{Display}]";
    }

    public static string AppendToHeading(string heading)
    {
        return $"{heading}\r\n{Display}";
    }

    private static string CreateDisplay()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppBuildInfo).Assembly;
        var version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        var location = assembly.Location;
        var builtAt = !string.IsNullOrWhiteSpace(location) && File.Exists(location)
            ? File.GetLastWriteTime(location)
            : DateTime.Now;

        return $"Build {version} {builtAt:yyyy-MM-dd HH:mm:ss}";
    }
}
