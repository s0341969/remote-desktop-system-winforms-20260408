using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteDesktop.Agent.Forms;
using RemoteDesktop.Agent.Forms.Settings;
using RemoteDesktop.Agent.Options;
using RemoteDesktop.Agent.Services;
using RemoteDesktop.Agent.Services.Settings;
using RemoteDesktop.Host.Forms;
using RemoteDesktop.Host.Forms.Settings;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;
using RemoteDesktop.Host.Services.Settings;

var tests = new (string Name, Action Body)[]
{
    ("Host Login UI", TestHostLoginForm),
    ("Host Settings UI", TestHostSettingsForm),
    ("Agent Settings UI", TestAgentSettingsForm),
    ("Host Main Dashboard UI", TestHostMainForm),
    ("Agent Main Runtime UI", TestAgentMainForm)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        RunOnSta(test.Body);
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

if (failures.Count > 0)
{
    throw new InvalidOperationException("UI_AUTOMATION_FAILED\n" + string.Join(Environment.NewLine, failures));
}

Console.WriteLine("UI_AUTOMATION_PASSED");

static void TestHostLoginForm()
{
    var options = Options.Create(new ControlServerOptions
    {
        ServerUrl = "http://localhost:5106",
        ConsoleName = "UI Test Host",
        AdminUserName = "admin",
        AdminPassword = "Password!2026",
        SharedAccessKey = "ChangeMe-Agent-Key",
        AgentHeartbeatTimeoutSeconds = 45
    });

    using var form = new LoginForm();
    form.Bind(new CredentialValidator(options), options.Value);
    form.Show();
    PumpUi();

    GetControl<TextBox>(form, "txtUserName").Text = "admin";
    GetControl<TextBox>(form, "txtPassword").Text = "Password!2026";
    GetControl<Button>(form, "btnLogin").PerformClick();
    PumpUi();

    if (form.DialogResult != DialogResult.OK)
    {
        throw new InvalidOperationException("登入表單未回傳成功。");
    }

    if (!string.Equals(form.AuthenticatedUserName, "admin", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("登入後的使用者名稱不正確。");
    }
}

static void TestHostSettingsForm()
{
    var store = new InMemoryHostSettingsStore();
    var document = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
    using var form = new HostSettingsForm(false);
    form.Bind(store, document, false);
    form.Show();
    PumpUi();

    GetControl<TextBox>(form, "txtConsoleName").Text = "UI Edited Host";
    GetControl<TextBox>(form, "txtServerUrl").Text = "http://localhost:5301";
    GetControl<TextBox>(form, "txtAdminPassword").Text = "UpdatedPass!2026";
    GetControl<TextBox>(form, "txtSharedAccessKey").Text = "Updated-Agent-Key-2026";
    GetControl<NumericUpDown>(form, "numHeartbeatTimeout").Value = 60;
    GetControl<Button>(form, "btnSave").PerformClick();
    PumpUi();

    var saved = store.LastSaved ?? throw new InvalidOperationException("Host 設定未送出到儲存服務。");
    if (!string.Equals(saved.ConsoleName, "UI Edited Host", StringComparison.Ordinal) || !string.Equals(saved.ServerUrl, "http://localhost:5301", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Host 設定 UI 未正確提交更新後的欄位值。");
    }
}

static void TestAgentSettingsForm()
{
    var store = new InMemoryAgentSettingsStore();
    var document = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
    using var form = new AgentSettingsForm(false);
    form.Bind(store, document, false);
    form.Show();
    PumpUi();

    GetControl<TextBox>(form, "txtServerUrl").Text = "http://localhost:5402";
    GetControl<TextBox>(form, "txtDeviceName").Text = "QA Agent";
    GetControl<NumericUpDown>(form, "numCaptureFps").Value = 12;
    GetControl<NumericUpDown>(form, "numReconnectDelay").Value = 8;
    GetControl<Button>(form, "btnSave").PerformClick();
    PumpUi();

    var saved = store.LastSaved ?? throw new InvalidOperationException("Agent 設定未送出到儲存服務。");
    if (!string.Equals(saved.DeviceName, "QA Agent", StringComparison.Ordinal) || !string.Equals(saved.ServerUrl, "http://localhost:5402", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Agent 設定 UI 未正確提交更新後的欄位值。");
    }
}

static void TestHostMainForm()
{
    var repo = new FakeDeviceRepository();
    repo.Seed(
        new DeviceRecord
        {
            DeviceId = "device-online",
            DeviceName = "Online Device",
            HostName = "HOST-01",
            AgentVersion = "1.0.0",
            ScreenWidth = 1920,
            ScreenHeight = 1080,
            IsOnline = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            LastConnectedAt = DateTimeOffset.UtcNow
        },
        new DeviceRecord
        {
            DeviceId = "device-offline",
            DeviceName = "Offline Device",
            HostName = "HOST-02",
            AgentVersion = "1.0.0",
            ScreenWidth = 1280,
            ScreenHeight = 720,
            IsOnline = false,
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastConnectedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastDisconnectedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });

    var options = Options.Create(new ControlServerOptions
    {
        ServerUrl = "http://localhost:5106",
        ConsoleName = "UI Dashboard",
        AdminUserName = "admin",
        AdminPassword = "Password!2026",
        SharedAccessKey = "ChangeMe-Agent-Key",
        AgentHeartbeatTimeoutSeconds = 45
    });

    var loggerFactory = LoggerFactory.Create(builder => { });
    var broker = new DeviceBroker(repo, options, loggerFactory.CreateLogger<DeviceBroker>());
    var settingsFactory = new HostSettingsFormFactory(new InMemoryHostSettingsStore());
    var viewerFactory = new RemoteViewerFormFactory(broker);

    using var form = new MainForm();
    form.Bind(repo, options.Value, viewerFactory, settingsFactory, "admin");
    form.Show();
    WaitUntil(() => GetControl<DataGridView>(form, "gridDevices").Rows.Count >= 2, 3000);

    var grid = GetControl<DataGridView>(form, "gridDevices");
    grid.Rows[0].Selected = true;
    grid.CurrentCell = grid.Rows[0].Cells[0];
    PumpUi();

    if (GetControl<Button>(form, "btnOpenViewer").Enabled != true)
    {
        throw new InvalidOperationException("主控台未針對在線裝置啟用遠端畫面按鈕。");
    }

    if (!string.Equals(GetControl<Label>(form, "lblOnlineCountValue").Text, "1", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("主控台在線裝置統計不正確。");
    }
}

static void TestAgentMainForm()
{
    var options = Options.Create(new AgentOptions
    {
        ServerUrl = "http://localhost:5106",
        DeviceId = "agent-ui-001",
        DeviceName = "Agent UI",
        SharedAccessKey = "ChangeMe-Agent-Key",
        CaptureFramesPerSecond = 8,
        JpegQuality = 55,
        MaxFrameWidth = 1600,
        ReconnectDelaySeconds = 5
    });

    var runtimeState = new AgentRuntimeState(options);
    runtimeState.MarkStarting();
    runtimeState.MarkConnected();
    runtimeState.MarkFrameSent();
    var settingsFactory = new AgentSettingsFormFactory(new InMemoryAgentSettingsStore());

    using var form = new AgentMainForm();
    form.Bind(runtimeState, settingsFactory);
    form.Show();
    PumpUi();

    if (!string.Equals(GetControl<Label>(form, "lblDeviceIdValue").Text, "agent-ui-001", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Agent 主畫面未顯示正確的 DeviceId。");
    }

    if (!string.Equals(GetControl<Label>(form, "lblStatusValue").Text, "已連線", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Agent 主畫面未顯示連線狀態。");
    }
}

static T GetControl<T>(Control root, string name) where T : Control
{
    foreach (Control control in root.Controls)
    {
        if (control.Name == name && control is T match)
        {
            return match;
        }

        var nested = TryFindNested<T>(control, name);
        if (nested is not null)
        {
            return nested;
        }
    }

    throw new InvalidOperationException($"找不到控制項 {name}。");
}

static T? TryFindNested<T>(Control root, string name) where T : Control
{
    foreach (Control child in root.Controls)
    {
        if (child.Name == name && child is T match)
        {
            return match;
        }

        var nested = TryFindNested<T>(child, name);
        if (nested is not null)
        {
            return nested;
        }
    }

    return null;
}

static void RunOnSta(Action body)
{
    Exception? captured = null;
    var completed = new ManualResetEventSlim(false);
    var thread = new Thread(() =>
    {
        try
        {
            body();
        }
        catch (Exception exception)
        {
            captured = exception;
        }
        finally
        {
            completed.Set();
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    completed.Wait();
    if (captured is not null)
    {
        throw captured;
    }
}

static void PumpUi(int cycles = 8)
{
    for (var i = 0; i < cycles; i++)
    {
        Application.DoEvents();
        Thread.Sleep(25);
    }
}

static void WaitUntil(Func<bool> predicate, int timeoutMs)
{
    var startedAt = Environment.TickCount64;
    while (Environment.TickCount64 - startedAt < timeoutMs)
    {
        PumpUi(1);
        if (predicate())
        {
            return;
        }
    }

    throw new TimeoutException("等待 UI 狀態逾時。");
}

internal sealed class FakeDeviceRepository : IDeviceRepository
{
    private readonly List<DeviceRecord> _devices = new();
    private readonly List<AgentPresenceLogRecord> _logs = new();

    public void Seed(params DeviceRecord[] devices)
    {
        _devices.Clear();
        _devices.AddRange(devices);
        _logs.Clear();
        _logs.AddRange(devices.Select(device => new AgentPresenceLogRecord
        {
            PresenceId = Guid.NewGuid(),
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            HostName = device.HostName,
            AgentVersion = device.AgentVersion,
            ConnectedAt = device.LastConnectedAt ?? DateTimeOffset.UtcNow,
            LastSeenAt = device.LastSeenAt,
            DisconnectedAt = device.LastDisconnectedAt,
            DisconnectReason = device.IsOnline ? null : "offline",
            OnlineSeconds = 30
        }));
    }

    public Task InitializeSchemaAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task UpsertDeviceOnlineAsync(AgentDescriptor descriptor, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<Guid> StartPresenceAsync(AgentDescriptor descriptor, CancellationToken cancellationToken) => Task.FromResult(Guid.NewGuid());
    public Task TouchPresenceAsync(Guid presenceId, string deviceId, int screenWidth, int screenHeight, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ClosePresenceAsync(Guid presenceId, string deviceId, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeviceRecord>>(_devices.Take(take).ToList());
    public Task<DeviceRecord?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken) => Task.FromResult(_devices.FirstOrDefault(x => x.DeviceId == deviceId));
    public Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AgentPresenceLogRecord>>(_logs.Take(take).ToList());
}

internal sealed class InMemoryHostSettingsStore : IHostSettingsStore
{
    private HostSettingsDocument _document = new();

    public HostSettingsDocument? LastSaved { get; private set; }

    public Task<HostSettingsDocument> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(new HostSettingsDocument
    {
        EnableDatabase = _document.EnableDatabase,
        RemoteDesktopDbConnectionString = _document.RemoteDesktopDbConnectionString,
        ServerUrl = _document.ServerUrl,
        ConsoleName = _document.ConsoleName,
        AdminUserName = _document.AdminUserName,
        AdminPassword = _document.AdminPassword,
        SharedAccessKey = _document.SharedAccessKey,
        RequireHttpsRedirect = _document.RequireHttpsRedirect,
        AgentHeartbeatTimeoutSeconds = _document.AgentHeartbeatTimeoutSeconds
    });

    public Task SaveAsync(HostSettingsDocument document, CancellationToken cancellationToken)
    {
        _document = document;
        LastSaved = document;
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryAgentSettingsStore : IAgentSettingsStore
{
    private AgentSettingsDocument _document = new();

    public AgentSettingsDocument? LastSaved { get; private set; }

    public Task<AgentSettingsDocument> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(new AgentSettingsDocument
    {
        ServerUrl = _document.ServerUrl,
        DeviceId = _document.DeviceId,
        DeviceName = _document.DeviceName,
        SharedAccessKey = _document.SharedAccessKey,
        CaptureFramesPerSecond = _document.CaptureFramesPerSecond,
        JpegQuality = _document.JpegQuality,
        MaxFrameWidth = _document.MaxFrameWidth,
        ReconnectDelaySeconds = _document.ReconnectDelaySeconds
    });

    public Task SaveAsync(AgentSettingsDocument document, CancellationToken cancellationToken)
    {
        _document = document;
        LastSaved = document;
        return Task.CompletedTask;
    }
}

