using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteDesktop.Agent.Forms;
using RemoteDesktop.Agent.Forms.Settings;
using RemoteDesktop.Agent.Options;
using RemoteDesktop.Agent.Services;
using RemoteDesktop.Agent.Services.Settings;
using RemoteDesktop.Host.Forms;
using RemoteDesktop.Host.Forms.Audit;
using RemoteDesktop.Host.Forms.Settings;
using RemoteDesktop.Host.Forms.Users;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;
using RemoteDesktop.Host.Services.Auditing;
using RemoteDesktop.Host.Services.Settings;
using RemoteDesktop.Host.Services.Users;

var tests = new (string Name, Action Body)[]
{
    ("Host Login UI", TestHostLoginForm),
    ("Host Settings UI", TestHostSettingsForm),
    ("Agent Settings UI", TestAgentSettingsForm),
    ("Host Main Dashboard UI", TestHostMainForm),
    ("Host Viewer File Upload UI", TestRemoteViewerUploadForm),
    ("Host User Management UI", TestHostUserManagementForm),
    ("Host Audit Log UI", TestHostAuditLogForm),
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
    throw new InvalidOperationException("UI_AUTOMATION_FAILED" + Environment.NewLine + string.Join(Environment.NewLine, failures));
}

Console.WriteLine("UI_AUTOMATION_PASSED");

static void TestHostLoginForm()
{
    var options = CreateHostOptions("UI Test Host");
    var authenticationService = CreateAuthenticationService(options.Value);
    var auditService = CreateAuditService();

    using var form = new LoginForm();
    form.Bind(authenticationService, auditService, options.Value);
    form.Show();
    PumpUi();

    GetControl<TextBox>(form, "txtUserName").Text = "admin";
    GetControl<TextBox>(form, "txtPassword").Text = "Password!2026";
    GetControl<Button>(form, "btnLogin").PerformClick();
    WaitUntil(() => form.DialogResult == DialogResult.OK, 3000);

    if (!string.Equals(form.AuthenticatedUser?.UserName, "admin", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Host login form did not produce the expected user session.");
    }
}

static void TestHostSettingsForm()
{
    var store = new InMemoryHostSettingsStore();
    var auditService = CreateAuditService();
    var currentUser = CreateAdministratorSession();
    var document = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
    using var form = new HostSettingsForm(false);
    form.Bind(store, auditService, currentUser, document, false);
    form.Show();
    PumpUi();

    GetControl<TextBox>(form, "txtConsoleName").Text = "UI Edited Host";
    GetControl<TextBox>(form, "txtServerUrl").Text = "http://localhost:5301";
    GetControl<TextBox>(form, "txtAdminPassword").Text = "UpdatedPass!2026";
    GetControl<TextBox>(form, "txtSharedAccessKey").Text = "Updated-Agent-Key-2026";
    GetControl<NumericUpDown>(form, "numHeartbeatTimeout").Value = 60;
    GetControl<Button>(form, "btnSave").PerformClick();
    PumpUi();

    var saved = store.LastSaved ?? throw new InvalidOperationException("Host settings were not saved.");
    if (!string.Equals(saved.ConsoleName, "UI Edited Host", StringComparison.Ordinal)
        || !string.Equals(saved.ServerUrl, "http://localhost:5301", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Host settings form did not persist the edited values.");
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
    GetControl<TextBox>(form, "txtFileTransferDirectory").Text = "C:\\Temp\\RemoteTransfers";
    GetControl<NumericUpDown>(form, "numCaptureFps").Value = 12;
    GetControl<NumericUpDown>(form, "numReconnectDelay").Value = 8;
    GetControl<Button>(form, "btnSave").PerformClick();
    PumpUi();

    var saved = store.LastSaved ?? throw new InvalidOperationException("Agent settings were not saved.");
    if (!string.Equals(saved.DeviceName, "QA Agent", StringComparison.Ordinal)
        || !string.Equals(saved.ServerUrl, "http://localhost:5402", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Agent settings form did not persist the edited values.");
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
            IsAuthorized = true,
            AuthorizedAt = DateTimeOffset.UtcNow,
            AuthorizedBy = "admin",
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            LastConnectedAt = DateTimeOffset.UtcNow
        },
        new DeviceRecord
        {
            DeviceId = "device-pending",
            DeviceName = "Pending Device",
            HostName = "HOST-02",
            AgentVersion = "1.0.0",
            ScreenWidth = 1280,
            ScreenHeight = 720,
            IsOnline = true,
            IsAuthorized = false,
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastConnectedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });

    var options = CreateHostOptions("UI Dashboard");
    var auditService = CreateAuditService();
    var loggerFactory = LoggerFactory.Create(static builder => { });
    var broker = new DeviceBroker(repo, options, loggerFactory.CreateLogger<DeviceBroker>(), auditService);
    var settingsFactory = new HostSettingsFormFactory(new InMemoryHostSettingsStore(), auditService);
    var authenticationService = CreateAuthenticationService(options.Value);
    var userManagementFactory = new UserManagementFormFactory(authenticationService, auditService);
    var auditLogFactory = new AuditLogFormFactory(auditService);
    var currentUser = AuthenticateOrThrow(authenticationService, "admin", "Password!2026");
    var viewerFactory = new RemoteViewerFormFactory(broker, new RemoteDesktop.Host.Services.FileTransferTraceService());

    using var form = new MainForm();
    form.Bind(repo, options.Value, broker, viewerFactory, settingsFactory, userManagementFactory, auditLogFactory, currentUser);
    form.Show();
    WaitUntil(() => GetControl<DataGridView>(form, "gridDevices").Rows.Count >= 2, 3000);

    var grid = GetControl<DataGridView>(form, "gridDevices");
    grid.Rows[0].Selected = true;
    grid.CurrentCell = grid.Rows[0].Cells[0];
    PumpUi();

    if (!GetControl<Button>(form, "btnOpenViewer").Enabled)
    {
        throw new InvalidOperationException("Open Viewer button should be enabled for an online device.");
    }

    if (!string.Equals(GetControl<Label>(form, "lblOnlineCountValue").Text, "2", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Host dashboard did not report the expected online device count.");
    }

    if (!GetControl<Button>(form, "btnUsers").Visible)
    {
        throw new InvalidOperationException("Administrator session should expose the Users button.");
    }

    if (!GetControl<Button>(form, "btnAudit").Visible)
    {
        throw new InvalidOperationException("Administrator session should expose the Audit button.");
    }

    if (!GetControl<Button>(form, "btnApproveDevice").Visible || !GetControl<Button>(form, "btnRevokeDevice").Visible)
    {
        throw new InvalidOperationException("Administrator session should expose device authorization controls.");
    }

    grid.ClearSelection();
    grid.Rows[1].Selected = true;
    grid.CurrentCell = grid.Rows[1].Cells[0];
    PumpUi();

    if (GetControl<Button>(form, "btnOpenViewer").Enabled)
    {
        throw new InvalidOperationException("Open Viewer button should stay disabled for a pending device.");
    }

    if (!GetControl<Button>(form, "btnApproveDevice").Enabled)
    {
        throw new InvalidOperationException("Approve button should be enabled for a pending device.");
    }
}

static void TestRemoteViewerUploadForm()
{
    var repo = new FakeDeviceRepository();
    var loggerFactory = LoggerFactory.Create(static builder => { });
    var options = CreateHostOptions("UI Viewer");
    var auditService = CreateAuditService();
    var broker = new DeviceBroker(repo, options, loggerFactory.CreateLogger<DeviceBroker>(), auditService);
    var currentUser = CreateAdministratorSession();
    var device = new DeviceRecord
    {
        DeviceId = "viewer-device-001",
        DeviceName = "Viewer Device",
        HostName = "HOST-VIEWER",
        AgentVersion = "1.0.0",
        ScreenWidth = 1920,
        ScreenHeight = 1080,
        IsOnline = true,
        IsAuthorized = true,
        AuthorizedAt = DateTimeOffset.UtcNow,
        AuthorizedBy = "admin",
        CreatedAt = DateTimeOffset.UtcNow,
        LastSeenAt = DateTimeOffset.UtcNow,
        LastConnectedAt = DateTimeOffset.UtcNow
    };

    var uploadFilePath = Path.Combine(Path.GetTempPath(), $"viewer-upload-{Guid.NewGuid():N}.txt");
    File.WriteAllText(uploadFilePath, "viewer-upload-ui-test");
    var storedFilePath = Path.Combine(Path.GetTempPath(), "RemoteDesktop Transfers", Path.GetFileName(uploadFilePath));
    Directory.CreateDirectory(Path.GetDirectoryName(storedFilePath)!);

    try
    {
        using var form = new TestRemoteViewerForm(uploadFilePath, storedFilePath);
        form.Bind(device, currentUser, broker);
        form.Show();
        PumpUi();

        var zoomCombo = GetControl<ComboBox>(form, "cboZoom");
        if (zoomCombo.Items.Count < 2)
        {
            throw new InvalidOperationException("Viewer zoom presets were not initialized.");
        }

        zoomCombo.SelectedIndex = 3;
        PumpUi();
        if (!string.Equals(zoomCombo.SelectedItem?.ToString(), "100%", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Viewer zoom selector did not switch to the expected preset.");
        }

        var fullscreenButton = GetControl<Button>(form, "btnFullscreen");
        fullscreenButton.PerformClick();
        PumpUi();
        if (GetControl<Panel>(form, "panelTop").Visible)
        {
            throw new InvalidOperationException("Viewer did not enter fullscreen mode.");
        }

        var keyDownHandler = typeof(RemoteViewerForm).GetMethod("RemoteViewerForm_KeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RemoteViewerForm_KeyDown method not found.");
        keyDownHandler.Invoke(form, new object[] { form, new KeyEventArgs(Keys.Escape) });
        PumpUi();
        if (!GetControl<Panel>(form, "panelTop").Visible)
        {
            throw new InvalidOperationException("Viewer did not exit fullscreen mode.");
        }

        var uploadButton = GetControl<Button>(form, "btnUploadFile");
        var workflowTask = form.TriggerUploadWorkflowAsync();
        WaitUntil(() => !uploadButton.Enabled, 3000);
        WaitUntil(() => GetControl<Label>(form, "lblTransferPathValue").Text.Contains(storedFilePath, StringComparison.Ordinal), 3000);
        WaitUntil(() => GetControl<Button>(form, "btnOpenTransferFolder").Enabled, 3000);
        workflowTask.GetAwaiter().GetResult();

        form.TriggerOpenTransferFolder();
        if (!string.Equals(form.OpenedFolderPath, Path.GetDirectoryName(storedFilePath), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Viewer did not try to open the expected transfer folder.");
        }
    }
    finally
    {
        try
        {
            if (File.Exists(uploadFilePath))
            {
                File.Delete(uploadFilePath);
            }

            var transferDirectory = Path.GetDirectoryName(storedFilePath);
            if (!string.IsNullOrWhiteSpace(transferDirectory) && Directory.Exists(transferDirectory))
            {
                Directory.Delete(transferDirectory, recursive: false);
            }
        }
        catch
        {
        }
    }
}

static void TestHostUserManagementForm()
{
    var options = CreateHostOptions("UI User Management");
    var authenticationService = CreateAuthenticationService(options.Value);
    var auditService = CreateAuditService();
    var currentUser = AuthenticateOrThrow(authenticationService, "admin", "Password!2026");

    using var form = new UserManagementForm();
    form.Bind(authenticationService, auditService, currentUser);
    form.Show();
    WaitUntil(() => GetControl<DataGridView>(form, "gridUsers").Rows.Count >= 1, 3000);

    GetControl<Button>(form, "btnNew").PerformClick();
    PumpUi();

    GetControl<TextBox>(form, "txtUserName").Text = "operator1";
    GetControl<TextBox>(form, "txtDisplayName").Text = "Operator One";
    GetControl<ComboBox>(form, "cboRole").SelectedItem = UserRole.Operator;
    GetControl<TextBox>(form, "txtPassword").Text = "OperatorPass!2026";
    GetControl<CheckBox>(form, "chkEnabled").Checked = true;
    GetControl<Button>(form, "btnSave").PerformClick();

    WaitUntil(() =>
    {
        var accounts = authenticationService.GetAccountsAsync(CancellationToken.None).GetAwaiter().GetResult();
        return accounts.Any(account => string.Equals(account.UserName, "operator1", StringComparison.Ordinal));
    }, 3000);

    var saved = authenticationService.GetAccountsAsync(CancellationToken.None).GetAwaiter().GetResult()
        .FirstOrDefault(account => string.Equals(account.UserName, "operator1", StringComparison.Ordinal));

    if (saved is null || saved.Role != UserRole.Operator || !saved.IsEnabled)
    {
        throw new InvalidOperationException("User management form did not persist the new operator account.");
    }
}

static void TestHostAuditLogForm()
{
    var auditService = CreateAuditService();
    auditService.WriteAsync("user-sign-in", "admin", "Administrator", "console", "UI Audit", true, "User signed in successfully.", CancellationToken.None)
        .GetAwaiter().GetResult();

    using var form = new AuditLogForm();
    form.Bind(auditService);
    form.Show();
    WaitUntil(() => GetControl<DataGridView>(form, "gridAuditLogs").Rows.Count >= 1, 3000);

    var grid = GetControl<DataGridView>(form, "gridAuditLogs");
    var actionText = grid.Rows[0].Cells[2].Value?.ToString() ?? string.Empty;
    if (!actionText.Contains("使用者登入", StringComparison.Ordinal) || !actionText.Contains("User sign-in", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Audit log form did not render the expected entry.");
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
        throw new InvalidOperationException("Agent main form did not render the expected device id.");
    }

    var actualStatus = GetControl<Label>(form, "lblStatusValue").Text;
    if (!actualStatus.Contains("已連線", StringComparison.Ordinal) || !actualStatus.Contains("Connected", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Agent main form did not render the expected status. Actual: {actualStatus}");
    }
}

static IOptions<ControlServerOptions> CreateHostOptions(string consoleName)
{
    return Options.Create(new ControlServerOptions
    {
        ServerUrl = "http://localhost:5106",
        ConsoleName = consoleName,
        AdminUserName = "admin",
        AdminPassword = "Password!2026",
        SharedAccessKey = "ChangeMe-Agent-Key",
        AgentHeartbeatTimeoutSeconds = 45
    });
}

static AuthenticationService CreateAuthenticationService(ControlServerOptions options)
{
    var store = new InMemoryUserAccountStore(options);
    return new AuthenticationService(store);
}

static AuditService CreateAuditService()
{
    return new AuditService(new InMemoryAuditLogStore());
}

static AuthenticatedUserSession AuthenticateOrThrow(AuthenticationService authenticationService, string userName, string password)
{
    return authenticationService.AuthenticateAsync(userName, password, CancellationToken.None).GetAwaiter().GetResult()
        ?? throw new InvalidOperationException($"Authentication failed for user '{userName}'.");
}

static AuthenticatedUserSession CreateAdministratorSession()
{
    return new AuthenticatedUserSession
    {
        UserName = "admin",
        DisplayName = "Administrator",
        Role = UserRole.Administrator
    };
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

    throw new InvalidOperationException($"Control not found: {name}");
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

    throw new TimeoutException("Timed out waiting for the UI state to update.");
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
    public Task SetDeviceAuthorizationAsync(string deviceId, bool isAuthorized, string changedByUserName, CancellationToken cancellationToken)
    {
        var index = _devices.FindIndex(item => string.Equals(item.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return Task.CompletedTask;
        }

        var device = _devices[index];
        _devices[index] = new DeviceRecord
        {
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            HostName = device.HostName,
            AgentVersion = device.AgentVersion,
            ScreenWidth = device.ScreenWidth,
            ScreenHeight = device.ScreenHeight,
            IsOnline = device.IsOnline,
            IsAuthorized = isAuthorized,
            AuthorizedAt = isAuthorized ? DateTimeOffset.UtcNow : (DateTimeOffset?)null,
            AuthorizedBy = isAuthorized ? changedByUserName : null,
            CreatedAt = device.CreatedAt,
            LastSeenAt = device.LastSeenAt,
            LastConnectedAt = device.LastConnectedAt,
            LastDisconnectedAt = device.LastDisconnectedAt
        };

        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeviceRecord>>(_devices.Take(take).ToList());
    public Task<DeviceRecord?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken) => Task.FromResult(_devices.FirstOrDefault(x => x.DeviceId == deviceId));
    public Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AgentPresenceLogRecord>>(_logs.Take(take).ToList());
}

internal sealed class InMemoryUserAccountStore : IUserAccountStore
{
    private readonly List<UserAccount> _accounts;

    public InMemoryUserAccountStore(ControlServerOptions options)
    {
        var passwordResult = PasswordHasher.HashPassword(options.AdminPassword);
        _accounts = new List<UserAccount>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserName = options.AdminUserName,
                DisplayName = "Administrator",
                Role = UserRole.Administrator,
                IsEnabled = true,
                PasswordHash = passwordResult.Hash,
                PasswordSalt = passwordResult.Salt,
                PasswordIterations = passwordResult.Iterations,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };
    }

    public Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<UserAccount>>(_accounts
            .OrderBy(static account => account.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    public Task<UserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        return Task.FromResult(_accounts.FirstOrDefault(account => string.Equals(account.UserName, userName, StringComparison.OrdinalIgnoreCase)));
    }

    public Task UpsertAsync(UserAccount account, CancellationToken cancellationToken)
    {
        var index = _accounts.FindIndex(existing => string.Equals(existing.UserName, account.UserName, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _accounts[index] = account;
        }
        else
        {
            _accounts.Add(account);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string userName, CancellationToken cancellationToken)
    {
        _accounts.RemoveAll(account => string.Equals(account.UserName, userName, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task UpdateLastLoginAsync(string userName, DateTimeOffset lastLoginAt, CancellationToken cancellationToken)
    {
        var index = _accounts.FindIndex(account => string.Equals(account.UserName, userName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return Task.CompletedTask;
        }

        var account = _accounts[index];
        _accounts[index] = new UserAccount
        {
            Id = account.Id,
            UserName = account.UserName,
            DisplayName = account.DisplayName,
            Role = account.Role,
            IsEnabled = account.IsEnabled,
            PasswordHash = account.PasswordHash,
            PasswordSalt = account.PasswordSalt,
            PasswordIterations = account.PasswordIterations,
            CreatedAt = account.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = lastLoginAt
        };

        return Task.CompletedTask;
    }
}

internal sealed class InMemoryAuditLogStore : IAuditLogStore
{
    private readonly List<AuditLogEntry> _entries = new();

    public Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(_entries
            .OrderByDescending(static entry => entry.OccurredAt)
            .Take(take)
            .ToList());
    }
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
        FileTransferDirectory = _document.FileTransferDirectory,
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

internal sealed class TestRemoteViewerForm : RemoteViewerForm
{
    private readonly string _selectedFilePath;
    private readonly string _storedFilePath;

    public TestRemoteViewerForm(string selectedFilePath, string storedFilePath)
    {
        _selectedFilePath = selectedFilePath;
        _storedFilePath = storedFilePath;
    }

    public string? OpenedFolderPath { get; private set; }

    protected override void OnShown(EventArgs e)
    {

        var configureZoom = typeof(RemoteViewerForm).GetMethod("ConfigureZoomOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConfigureZoomOptions method not found.");
        var applyLayout = typeof(RemoteViewerForm).GetMethod("ApplyPictureLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ApplyPictureLayout method not found.");

        configureZoom.Invoke(this, null);
        applyLayout.Invoke(this, null);
    }

    public Task TriggerUploadWorkflowAsync()
    {
        HandleUploadSelection();
        return Task.CompletedTask;
    }

    public void TriggerOpenTransferFolder() => HandleOpenTransferFolder();

    protected override string? SelectUploadFilePath(IWin32Window owner)
    {
        return _selectedFilePath;
    }

    protected override async Task UploadFileCoreAsync(FileInfo fileInfo, string uploadId)
    {
        ApplyTransferStatus(new RemoteDesktop.Host.Models.AgentFileTransferStatusMessage
        {
            UploadId = uploadId,
            Status = "started",
            FileName = fileInfo.Name,
            StoredFileName = Path.GetFileName(_storedFilePath),
            StoredFilePath = _storedFilePath,
            FileSize = fileInfo.Length,
            BytesTransferred = 0,
            Message = $"開始接收 {fileInfo.Name}"
        });

        await Task.Delay(600);

        ApplyTransferStatus(new RemoteDesktop.Host.Models.AgentFileTransferStatusMessage
        {
            UploadId = uploadId,
            Status = "completed",
            FileName = fileInfo.Name,
            StoredFileName = Path.GetFileName(_storedFilePath),
            StoredFilePath = _storedFilePath,
            FileSize = fileInfo.Length,
            BytesTransferred = fileInfo.Length,
            Message = $"檔案已儲存到 {Path.GetFileName(_storedFilePath)}。"
        });
    }

    protected override void OpenTransferFolder(string directoryPath)
    {
        OpenedFolderPath = directoryPath;
    }
}





















