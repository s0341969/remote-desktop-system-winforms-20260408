using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using RemoteDesktop.Agent.Compatibility;
using RemoteDesktop.Agent.Options;

namespace RemoteDesktop.Agent.Services;

public sealed class InteractiveSessionRecoveryService
{
    private static readonly TimeSpan AttemptCooldown = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);
    private readonly AgentOptions _options;
    private readonly ILogger<InteractiveSessionRecoveryService> _logger;
    private readonly SemaphoreSlim _attemptGate = new(1, 1);
    private DateTimeOffset _lastAttemptAt = DateTimeOffset.MinValue;
    private bool? _isWindowsServer;

    public InteractiveSessionRecoveryService(
        IOptions<AgentOptions> options,
        ILogger<InteractiveSessionRecoveryService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InteractiveSessionRecoveryResult> TryRecoverAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoRecoverInteractiveSessionOnWindowsServer)
        {
            return InteractiveSessionRecoveryResult.CreateNotApplicable("Windows Server session auto-recovery is disabled.");
        }

        if (!IsWindowsServer())
        {
            return InteractiveSessionRecoveryResult.CreateNotApplicable("Current operating system is not Windows Server.");
        }

        if (!await _attemptGate.WaitAsync(0, cancellationToken))
        {
            return InteractiveSessionRecoveryResult.CreateSkipped("A previous session recovery attempt is still running.");
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastAttemptAt < AttemptCooldown)
            {
                return InteractiveSessionRecoveryResult.CreateSkipped("Session recovery is cooling down.");
            }

            _lastAttemptAt = now;

            var currentSessionId = Process.GetCurrentProcess().SessionId;
            var consoleSessionId = unchecked((int)WTSGetActiveConsoleSessionId());
            if (consoleSessionId == currentSessionId)
            {
                return InteractiveSessionRecoveryResult.CreateNotNeeded("Current process is already attached to the active console session.");
            }

            var tsconPath = Path.Combine(Environment.SystemDirectory, "tscon.exe");
            if (!File.Exists(tsconPath))
            {
                return InteractiveSessionRecoveryResult.CreateFailed($"Could not find tscon.exe at '{tsconPath}'.");
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = tsconPath,
                    Arguments = $"{currentSessionId} /dest:console",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            if (!process.Start())
            {
                return InteractiveSessionRecoveryResult.CreateFailed("tscon.exe could not be started.");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(AttemptTimeout);
            try
            {
                await process.WaitForExitAsyncCompat(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return InteractiveSessionRecoveryResult.CreateFailed("tscon.exe timed out while switching the session back to the console.");
            }

            if (process.ExitCode != 0)
            {
                return InteractiveSessionRecoveryResult.CreateFailed($"tscon.exe exited with code {process.ExitCode}.");
            }

            _logger.LogInformation(
                "Recovered interactive session by switching session {SessionId} back to the console session.",
                currentSessionId);
            return InteractiveSessionRecoveryResult.CreateRecovered("The current RDP session was switched back to the console.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Interactive session recovery failed unexpectedly.");
            return InteractiveSessionRecoveryResult.CreateFailed(exception.Message);
        }
        finally
        {
            _attemptGate.Release();
        }
    }

    private bool IsWindowsServer()
    {
        if (_isWindowsServer.HasValue)
        {
            return _isWindowsServer.Value;
        }

        const string productOptionsKey = @"SYSTEM\CurrentControlSet\Control\ProductOptions";
        using var key = Registry.LocalMachine.OpenSubKey(productOptionsKey, writable: false);
        var productType = key?.GetValue("ProductType") as string;
        _isWindowsServer = !string.Equals(productType, "WinNT", StringComparison.OrdinalIgnoreCase);
        return _isWindowsServer.Value;
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();
}

public sealed record InteractiveSessionRecoveryResult(bool Attempted, bool Recovered, string Message)
{
    public static InteractiveSessionRecoveryResult CreateNotApplicable(string message) => new(false, false, message);

    public static InteractiveSessionRecoveryResult CreateNotNeeded(string message) => new(false, false, message);

    public static InteractiveSessionRecoveryResult CreateSkipped(string message) => new(false, false, message);

    public static InteractiveSessionRecoveryResult CreateFailed(string message) => new(true, false, message);

    public static InteractiveSessionRecoveryResult CreateRecovered(string message) => new(true, true, message);
}
