using System.Reflection;
using System.Diagnostics;

namespace RemoteDesktop.Agent;

internal static class AppBuildInfo
{
    private static readonly Lazy<string> VersionValue = new(CreateVersion);
    private static readonly Lazy<string> DisplayValue = new(CreateDisplay);

    public static string Version => VersionValue.Value;

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
        var processPath = Environment.ProcessPath;
        var builtAt = !string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath)
            ? File.GetLastWriteTime(processPath)
            : DateTime.Now;

        return $"Build {Version} {builtAt:yyyy-MM-dd HH:mm:ss}";
    }

    private static string CreateVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppBuildInfo).Assembly;
        try
        {
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return informationalVersion;
            }
        }
        catch
        {
        }

        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                var productVersion = FileVersionInfo.GetVersionInfo(processPath).ProductVersion;
                if (!string.IsNullOrWhiteSpace(productVersion))
                {
                    return productVersion;
                }
            }
        }
        catch
        {
        }

        try
        {
            return assembly.GetName().Version?.ToString() ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }
}
