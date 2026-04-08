using RemoteDesktop.Host.Forms.Audit;
using RemoteDesktop.Host.Forms.Settings;
using RemoteDesktop.Host.Forms.Users;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public partial class MainForm : Form
{
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private IDeviceRepository? _repository;
    private ControlServerOptions? _options;
    private DeviceBroker? _deviceBroker;
    private RemoteViewerFormFactory? _remoteViewerFormFactory;
    private HostSettingsFormFactory? _hostSettingsFormFactory;
    private UserManagementFormFactory? _userManagementFormFactory;
    private AuditLogFormFactory? _auditLogFormFactory;
    private AuthenticatedUserSession? _signedInUser;
    private bool _refreshing;
    private string? _lastRefreshErrorMessage;

    public MainForm()
    {
        InitializeComponent();
        InitializeUiText();
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 5000
        };
        _refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();
    }

    public void Bind(
        IDeviceRepository repository,
        ControlServerOptions options,
        DeviceBroker deviceBroker,
        RemoteViewerFormFactory remoteViewerFormFactory,
        HostSettingsFormFactory hostSettingsFormFactory,
        UserManagementFormFactory userManagementFormFactory,
        AuditLogFormFactory auditLogFormFactory,
        AuthenticatedUserSession signedInUser)
    {
        _repository = repository;
        _options = options;
        _deviceBroker = deviceBroker;
        _remoteViewerFormFactory = remoteViewerFormFactory;
        _hostSettingsFormFactory = hostSettingsFormFactory;
        _userManagementFormFactory = userManagementFormFactory;
        _auditLogFormFactory = auditLogFormFactory;
        _signedInUser = signedInUser;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (_options is null)
        {
            return;
        }

        lblConsoleNameValue.Text = _options.ConsoleName;
        lblServerUrlValue.Text = _options.ServerUrl;
        lblHealthUrlValue.Text = $"{_options.ServerUrl.TrimEnd('/')}/healthz";
        lblSignedInUserValue.Text = _signedInUser is null
            ? "-"
            : $"{_signedInUser.DisplayName} ({_signedInUser.RoleDisplayName})";
        btnAudit.Visible = _signedInUser?.CanViewAuditLogs == true;
        btnApproveDevice.Visible = _signedInUser?.CanManageDeviceAuthorization == true;
        btnSettings.Visible = _signedInUser?.CanManageSettings == true;
        btnRevokeDevice.Visible = _signedInUser?.CanManageDeviceAuthorization == true;
        btnUsers.Visible = _signedInUser?.CanManageUsers == true;

        await RefreshDashboardAsync(showErrorDialog: true);
        _refreshTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        base.OnFormClosed(e);
    }

    private async void btnRefresh_Click(object sender, EventArgs e)
    {
        await RefreshDashboardAsync(showErrorDialog: true);
    }

    private void btnOpenViewer_Click(object sender, EventArgs e)
    {
        OpenSelectedViewer();
    }

    private async void btnApproveDevice_Click(object sender, EventArgs e)
    {
        await ChangeSelectedDeviceAuthorizationAsync(true);
    }

    private async void btnRevokeDevice_Click(object sender, EventArgs e)
    {
        await ChangeSelectedDeviceAuthorizationAsync(false);
    }

    private async void btnSettings_Click(object sender, EventArgs e)
    {
        if (_hostSettingsFormFactory is null || _signedInUser is not { CanManageSettings: true })
        {
            return;
        }

        using var settingsForm = await _hostSettingsFormFactory.CreateAsync(_signedInUser);
        if (settingsForm.ShowDialog(this) == DialogResult.OK)
        {
            MessageBox.Show(
                HostUiText.Bi("Host 設定已儲存，請重新啟動 Host 套用全部變更。", "Host settings were saved. Restart the Host application to apply all changes."),
                HostUiText.Window("設定", "Settings"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void btnUsers_Click(object sender, EventArgs e)
    {
        if (_userManagementFormFactory is null || _signedInUser is not { CanManageUsers: true })
        {
            return;
        }

        using var usersForm = _userManagementFormFactory.Create(_signedInUser);
        usersForm.ShowDialog(this);
    }

    private void btnAudit_Click(object sender, EventArgs e)
    {
        if (_auditLogFormFactory is null || _signedInUser is not { CanViewAuditLogs: true })
        {
            return;
        }

        using var auditForm = _auditLogFormFactory.Create();
        auditForm.ShowDialog(this);
    }

    private void gridDevices_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0)
        {
            OpenSelectedViewer();
        }
    }

    private void gridDevices_SelectionChanged(object sender, EventArgs e)
    {
        UpdateSelectedDeviceActions();
    }

    private async Task RefreshDashboardAsync(bool showErrorDialog = false)
    {
        if (_repository is null || _refreshing)
        {
            return;
        }

        try
        {
            _refreshing = true;
            btnRefresh.Enabled = false;
            btnOpenViewer.Enabled = false;
            btnApproveDevice.Enabled = false;
            btnAudit.Enabled = false;
            btnSettings.Enabled = false;
            btnRevokeDevice.Enabled = false;
            btnUsers.Enabled = false;
            lblStatusValue.Text = HostUiText.Bi("重新整理中...", "Refreshing...");

            var selectedDeviceId = GetSelectedDevice()?.Source.DeviceId;
            var devicesTask = _repository.GetDevicesAsync(100, CancellationToken.None);
            var logsTask = _repository.GetPresenceLogsAsync(100, CancellationToken.None);
            await Task.WhenAll(devicesTask, logsTask);

            var devices = await devicesTask;
            var logs = await logsTask;

            BindDeviceGrid(devices, selectedDeviceId);
            gridLogs.DataSource = logs.Select(static item => new PresenceLogGridItem(item)).ToList();

            var onlineCount = devices.Count(static item => item.IsOnline);
            lblOnlineCountValue.Text = onlineCount.ToString();
            lblTotalCountValue.Text = devices.Count.ToString();
            lblLastRefreshValue.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            lblStatusValue.Text = HostUiText.Bi("就緒", "Ready");
            _lastRefreshErrorMessage = null;
            UpdateSelectedDeviceActions();
        }
        catch (Exception exception)
        {
            lblStatusValue.Text = HostUiText.Bi("重新整理失敗", "Refresh failed");
            if (showErrorDialog && !string.Equals(_lastRefreshErrorMessage, exception.Message, StringComparison.Ordinal))
            {
                MessageBox.Show(
                    HostUiText.Bi($"儀表板資料重新整理失敗：{exception.Message}", $"Failed to refresh dashboard data: {exception.Message}"),
                    HostUiText.Window("錯誤", "Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            _lastRefreshErrorMessage = exception.Message;
        }
        finally
        {
            _refreshing = false;
            btnRefresh.Enabled = true;
            btnAudit.Enabled = _signedInUser?.CanViewAuditLogs == true;
            btnApproveDevice.Enabled = false;
            btnSettings.Enabled = _signedInUser?.CanManageSettings == true;
            btnRevokeDevice.Enabled = false;
            btnUsers.Enabled = _signedInUser?.CanManageUsers == true;
            UpdateSelectedDeviceActions();
        }
    }

    private void BindDeviceGrid(IReadOnlyList<DeviceRecord> devices, string? selectedDeviceId)
    {
        var items = devices.Select(static item => new DeviceGridItem(item)).ToList();
        gridDevices.DataSource = items;

        if (string.IsNullOrWhiteSpace(selectedDeviceId))
        {
            return;
        }

        foreach (DataGridViewRow row in gridDevices.Rows)
        {
            if (row.DataBoundItem is not DeviceGridItem item || !string.Equals(item.DeviceId, selectedDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            row.Selected = true;
            gridDevices.CurrentCell = row.Cells[0];
            break;
        }
    }

    private void OpenSelectedViewer()
    {
        if (_remoteViewerFormFactory is null)
        {
            return;
        }

        var selected = GetSelectedDevice();
        if (selected is null)
        {
            return;
        }

        if (!selected.Source.IsOnline)
        {
            MessageBox.Show(
                HostUiText.Bi("所選裝置目前離線，無法開啟 Viewer 工作階段。", "The selected device is currently offline and cannot open a viewer session."),
                HostUiText.Window("裝置狀態", "Device Status"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!selected.Source.IsAuthorized)
        {
            MessageBox.Show(
                HostUiText.Bi("所選裝置仍在等待核准，請先核准無人值守存取後再開啟 Viewer。", "The selected device is waiting for approval. Approve unattended access before opening a viewer session."),
                HostUiText.Window("裝置授權", "Device Authorization"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (_signedInUser is null)
        {
            return;
        }

        var viewerForm = _remoteViewerFormFactory.Create(selected.Source, _signedInUser);
        viewerForm.Show(this);
    }

    private DeviceGridItem? GetSelectedDevice()
    {
        if (gridDevices.SelectedRows.Count > 0)
        {
            return gridDevices.SelectedRows[0].DataBoundItem as DeviceGridItem;
        }

        return gridDevices.CurrentRow?.DataBoundItem as DeviceGridItem;
    }

    private async Task ChangeSelectedDeviceAuthorizationAsync(bool isAuthorized)
    {
        if (_deviceBroker is null || _signedInUser is not { CanManageDeviceAuthorization: true })
        {
            return;
        }

        var selected = GetSelectedDevice();
        if (selected is null)
        {
            return;
        }

        btnApproveDevice.Enabled = false;
        btnRevokeDevice.Enabled = false;
        lblStatusValue.Text = isAuthorized
            ? HostUiText.Bi("正在核准裝置...", "Approving device...")
            : HostUiText.Bi("正在撤銷裝置授權...", "Revoking device...");

        try
        {
            var success = await _deviceBroker.SetDeviceAuthorizationAsync(selected.Source.DeviceId, isAuthorized, _signedInUser.UserName, CancellationToken.None);
            if (!success)
            {
                MessageBox.Show(
                    HostUiText.Bi("找不到所選裝置，請重新整理後再試一次。", "The selected device could not be found anymore. Refresh the dashboard and try again."),
                    HostUiText.Window("裝置授權", "Device Authorization"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            await RefreshDashboardAsync(showErrorDialog: success);
        }
        catch (Exception exception)
        {
            lblStatusValue.Text = HostUiText.Bi("授權更新失敗", "Authorization update failed");
            MessageBox.Show(
                HostUiText.Bi($"更新裝置授權失敗：{exception.Message}", $"Failed to update device authorization: {exception.Message}"),
                HostUiText.Window("裝置授權", "Device Authorization"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void UpdateSelectedDeviceActions()
    {
        var selected = GetSelectedDevice();
        btnOpenViewer.Enabled = selected is { Source.IsOnline: true, Source.IsAuthorized: true };
        btnApproveDevice.Enabled = _signedInUser?.CanManageDeviceAuthorization == true && selected is not null && !selected.Source.IsAuthorized;
        btnRevokeDevice.Enabled = _signedInUser?.CanManageDeviceAuthorization == true && selected is not null && selected.Source.IsAuthorized;
    }

    private void InitializeUiText()
    {
        Text = HostUiText.Window("遠端桌面 Windows 主控台", "RemoteDesktop Windows Console");
        lblTitle.Text = HostUiText.Bi("遠端桌面 Windows 主控台", "RemoteDesktop Windows Console");
        HostUiText.ApplyButton(btnApproveDevice, "核准", "Approve");
        HostUiText.ApplyButton(btnAudit, "稽核", "Audit");
        HostUiText.ApplyButton(btnRevokeDevice, "撤銷", "Revoke");
        HostUiText.ApplyButton(btnUsers, "使用者", "Users");
        HostUiText.ApplyButton(btnSettings, "設定", "Settings");
        HostUiText.ApplyButton(btnOpenViewer, "開啟 Viewer", "Open Viewer");
        HostUiText.ApplyButton(btnRefresh, "重新整理", "Refresh");
        lblConsoleNameCaption.Text = HostUiText.Bi("主控台", "Console");
        lblServerUrlCaption.Text = HostUiText.Bi("Agent 位址", "Agent URL");
        lblHealthUrlCaption.Text = HostUiText.Bi("健康檢查位址", "Health URL");
        lblSignedInUserCaption.Text = HostUiText.Bi("目前登入使用者", "Signed-in user");
        lblOnlineCountCaption.Text = HostUiText.Bi("在線裝置數", "Online devices");
        lblTotalCountCaption.Text = HostUiText.Bi("裝置總數", "Total devices");
        lblLastRefreshCaption.Text = HostUiText.Bi("最後更新時間", "Last refresh");
        lblDevicesTitle.Text = HostUiText.Bi("已連線裝置", "Connected devices");
        lblLogsTitle.Text = HostUiText.Bi("在線紀錄", "Presence logs");
        lblStatusCaption.Text = HostUiText.Bi("狀態", "Status");
        lblStatusValue.Text = HostUiText.Bi("就緒", "Ready");

        if (gridDevices.Columns.Count >= 10)
        {
            gridDevices.Columns[0].HeaderText = HostUiText.Bi("狀態", "Status");
            gridDevices.Columns[1].HeaderText = HostUiText.Bi("存取", "Access");
            gridDevices.Columns[2].HeaderText = HostUiText.Bi("裝置 ID", "Device ID");
            gridDevices.Columns[3].HeaderText = HostUiText.Bi("裝置名稱", "Device name");
            gridDevices.Columns[4].HeaderText = HostUiText.Bi("主機名稱", "Host name");
            gridDevices.Columns[5].HeaderText = HostUiText.Bi("解析度", "Resolution");
            gridDevices.Columns[6].HeaderText = HostUiText.Bi("Agent 版本", "Agent version");
            gridDevices.Columns[7].HeaderText = HostUiText.Bi("最後上線", "Last seen");
            gridDevices.Columns[8].HeaderText = HostUiText.Bi("最後連線", "Last connected");
            gridDevices.Columns[9].HeaderText = HostUiText.Bi("最後離線", "Last disconnected");
        }

        if (gridLogs.Columns.Count >= 8)
        {
            gridLogs.Columns[0].HeaderText = HostUiText.Bi("裝置 ID", "Device ID");
            gridLogs.Columns[1].HeaderText = HostUiText.Bi("裝置名稱", "Device name");
            gridLogs.Columns[2].HeaderText = HostUiText.Bi("主機名稱", "Host name");
            gridLogs.Columns[3].HeaderText = HostUiText.Bi("連線時間", "Connected at");
            gridLogs.Columns[4].HeaderText = HostUiText.Bi("最後上線", "Last seen");
            gridLogs.Columns[5].HeaderText = HostUiText.Bi("離線時間", "Disconnected at");
            gridLogs.Columns[6].HeaderText = HostUiText.Bi("離線原因", "Disconnect reason");
            gridLogs.Columns[7].HeaderText = HostUiText.Bi("在線秒數", "Online seconds");
        }
    }

    private sealed class DeviceGridItem
    {
        public DeviceGridItem(DeviceRecord source)
        {
            Source = source;
            StatusText = source.IsOnline ? HostUiText.Bi("在線", "Online") : HostUiText.Bi("離線", "Offline");
            AccessText = source.IsAuthorized ? HostUiText.Bi("已核准", "Approved") : HostUiText.Bi("待核准", "Pending approval");
            DeviceId = source.DeviceId;
            DeviceName = source.DeviceName;
            HostName = source.HostName;
            Resolution = $"{source.ScreenWidth} x {source.ScreenHeight}";
            AgentVersion = source.AgentVersion;
            LastSeenAt = source.LastSeenAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            LastConnectedAt = source.LastConnectedAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
            LastDisconnectedAt = source.LastDisconnectedAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        }

        public DeviceRecord Source { get; }
        public string StatusText { get; }
        public string AccessText { get; }
        public string DeviceId { get; }
        public string DeviceName { get; }
        public string HostName { get; }
        public string Resolution { get; }
        public string AgentVersion { get; }
        public string LastSeenAt { get; }
        public string LastConnectedAt { get; }
        public string LastDisconnectedAt { get; }
    }

    private sealed class PresenceLogGridItem
    {
        public PresenceLogGridItem(AgentPresenceLogRecord source)
        {
            DeviceId = source.DeviceId;
            DeviceName = source.DeviceName;
            HostName = source.HostName;
            ConnectedAt = source.ConnectedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            LastSeenAt = source.LastSeenAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            DisconnectedAt = source.DisconnectedAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
            DisconnectReason = string.IsNullOrWhiteSpace(source.DisconnectReason) ? "-" : FormatDisconnectReason(source.DisconnectReason);
            OnlineSeconds = source.OnlineSeconds.ToString();
        }

        public string DeviceId { get; }
        public string DeviceName { get; }
        public string HostName { get; }
        public string ConnectedAt { get; }
        public string LastSeenAt { get; }
        public string DisconnectedAt { get; }
        public string DisconnectReason { get; }
        public string OnlineSeconds { get; }
    }

    private static string FormatDisconnectReason(string reason)
    {
        return reason switch
        {
            "offline" => HostUiText.Bi("離線", "Offline"),
            "socket-closed" => HostUiText.Bi("連線已關閉", "Socket closed"),
            "heartbeat-timeout" => HostUiText.Bi("心跳逾時", "Heartbeat timeout"),
            "server-restart" => HostUiText.Bi("伺服器重新啟動", "Server restart"),
            "replaced-by-new-agent" => HostUiText.Bi("被新的 Agent 連線取代", "Replaced by new agent"),
            _ => reason
        };
    }
}
