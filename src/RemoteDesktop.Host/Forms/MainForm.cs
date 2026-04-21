using RemoteDesktop.Host.Forms.Audit;
using RemoteDesktop.Host.Forms.Settings;
using RemoteDesktop.Host.Forms.Users;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;
using System.Runtime.InteropServices;

namespace RemoteDesktop.Host.Forms;

public partial class MainForm : Form
{
    private const int DashboardTakeLimit = 500;
    private const string DisplayTimestampFormat = "yyyy-MM-dd HH:mm:ss zzz";
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _dashboardRefreshDebounceTimer;
    private readonly BindingSource _deviceBindingSource = new();
    private readonly BindingSource _logBindingSource = new();
    private readonly TextBox _txtSearch = new();
    private readonly Button _btnApplySearch = new();
    private readonly Button _btnClearSearch = new();
    private readonly Label _lblSearch = new();
    private IMainDashboardDataSource? _dashboardDataSource;
    private ControlServerOptions? _options;
    private RemoteViewerFormFactory? _remoteViewerFormFactory;
    private DeviceInventoryDetailsFormFactory? _deviceInventoryDetailsFormFactory;
    private HostSettingsFormFactory? _hostSettingsFormFactory;
    private UserManagementFormFactory? _userManagementFormFactory;
    private AuditLogFormFactory? _auditLogFormFactory;
    private AuthenticatedUserSession? _signedInUser;
    private bool _refreshing;
    private string? _lastRefreshErrorMessage;
    private bool _dashboardUpdateSubscriptionAttached;
    private List<DeviceGridItem> _allDeviceGridItems = [];
    private List<DeviceGridItem> _deviceGridItems = [];
    private List<PresenceLogGridItem> _allLogGridItems = [];
    private List<PresenceLogGridItem> _logGridItems = [];
    private string? _deviceGridSnapshot;
    private string? _logGridSnapshot;
    private GridSortState? _deviceGridSortState = new(nameof(DeviceGridItem.DeviceId), SortOrder.Ascending);
    private GridSortState? _logGridSortState = new(nameof(PresenceLogGridItem.ConnectedAt), SortOrder.Descending);
    private string _searchKeyword = string.Empty;
    private bool _dashboardRefreshRequested;

    public MainForm()
    {
        InitializeComponent();
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        UpdateStyles();
        InitializeUiText();
        InitializeSearchUi();
        InitializeGridBehavior();
        InitializeResponsiveLayout();
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 5000
        };
        _refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();
        _dashboardRefreshDebounceTimer = new System.Windows.Forms.Timer
        {
            Interval = 800
        };
        _dashboardRefreshDebounceTimer.Tick += DashboardRefreshDebounceTimer_Tick;
        Resize += MainForm_Resize;
    }

    public void Bind(
        IMainDashboardDataSource dashboardDataSource,
        ControlServerOptions options,
        RemoteViewerFormFactory remoteViewerFormFactory,
        DeviceInventoryDetailsFormFactory deviceInventoryDetailsFormFactory,
        HostSettingsFormFactory hostSettingsFormFactory,
        UserManagementFormFactory userManagementFormFactory,
        AuditLogFormFactory auditLogFormFactory,
        AuthenticatedUserSession signedInUser)
    {
        _dashboardDataSource = dashboardDataSource;
        _options = options;
        _remoteViewerFormFactory = remoteViewerFormFactory;
        _deviceInventoryDetailsFormFactory = deviceInventoryDetailsFormFactory;
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
        lblServerUrlValue.Text = _dashboardDataSource?.DashboardServerUrl ?? _options.ServerUrl;
        lblHealthUrlValue.Text = _dashboardDataSource?.HealthUrl ?? $"{_options.ServerUrl.TrimEnd('/')}/healthz";
        lblSignedInUserValue.Text = _signedInUser is null
            ? "-"
            : $"{_signedInUser.DisplayName} ({_signedInUser.RoleDisplayName})";
        btnAudit.Visible = _signedInUser?.CanViewAuditLogs == true;
        btnApproveDevice.Visible = _signedInUser?.CanManageDeviceAuthorization == true;
        btnSettings.Visible = _signedInUser?.CanManageSettings == true;
        btnRevokeDevice.Visible = _signedInUser?.CanManageDeviceAuthorization == true;
        btnUsers.Visible = _signedInUser?.CanManageUsers == true;

        if (_dashboardDataSource is not null && !_dashboardUpdateSubscriptionAttached)
        {
            _dashboardDataSource.DashboardUpdated += DashboardDataSource_DashboardUpdated;
            _dashboardUpdateSubscriptionAttached = true;
        }

        if (_dashboardDataSource is not null)
        {
            _refreshTimer.Interval = _dashboardDataSource.SupportsRealtimeUpdates ? 30000 : 5000;
            await _dashboardDataSource.StartAsync(CancellationToken.None);
        }

        await RefreshDashboardAsync(showErrorDialog: true, isUserInitiated: true);
        _refreshTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _dashboardRefreshDebounceTimer.Stop();
        _dashboardRefreshDebounceTimer.Dispose();
        if (_dashboardDataSource is not null && _dashboardUpdateSubscriptionAttached)
        {
            _dashboardDataSource.DashboardUpdated -= DashboardDataSource_DashboardUpdated;
            _dashboardUpdateSubscriptionAttached = false;
        }

        (_dashboardDataSource as IDisposable)?.Dispose();
        base.OnFormClosed(e);
    }

    private async void btnRefresh_Click(object sender, EventArgs e)
    {
        await RefreshDashboardAsync(showErrorDialog: true, isUserInitiated: true);
    }

    private void btnOpenViewer_Click(object sender, EventArgs e)
    {
        OpenSelectedViewer();
    }

    private void btnDeviceDetails_Click(object sender, EventArgs e)
    {
        OpenSelectedDeviceDetails();
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

    private async Task RefreshDashboardAsync(bool showErrorDialog = false, bool isUserInitiated = false)
    {
        if (_dashboardDataSource is null || _refreshing)
        {
            return;
        }

        try
        {
            _refreshing = true;
            if (isUserInitiated)
            {
                btnRefresh.Enabled = false;
                btnOpenViewer.Enabled = false;
                btnDeviceDetails.Enabled = false;
                btnApproveDevice.Enabled = false;
                btnAudit.Enabled = false;
                btnSettings.Enabled = false;
                btnRevokeDevice.Enabled = false;
                btnUsers.Enabled = false;
                lblStatusValue.Text = HostUiText.Bi("重新整理中...", "Refreshing...");
            }

            var selectedDeviceId = GetSelectedDevice()?.Source.DeviceId;
            var devicesTask = _dashboardDataSource.GetDevicesAsync(DashboardTakeLimit, CancellationToken.None);
            var logsTask = _dashboardDataSource.GetPresenceLogsAsync(DashboardTakeLimit, CancellationToken.None);
            await Task.WhenAll(devicesTask, logsTask);

            var devices = await devicesTask;
            var logs = await logsTask;

            BindDeviceGrid(devices, selectedDeviceId);
            BindLogGrid(logs);

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
            if (isUserInitiated)
            {
                btnRefresh.Enabled = true;
                btnAudit.Enabled = _signedInUser?.CanViewAuditLogs == true;
                btnApproveDevice.Enabled = false;
                btnSettings.Enabled = _signedInUser?.CanManageSettings == true;
                btnRevokeDevice.Enabled = false;
                btnUsers.Enabled = _signedInUser?.CanManageUsers == true;
            }
            UpdateSelectedDeviceActions();
        }
    }

    private void DashboardDataSource_DashboardUpdated(object? sender, EventArgs e)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        _dashboardRefreshRequested = true;
        if (_dashboardRefreshDebounceTimer.Enabled)
        {
            _dashboardRefreshDebounceTimer.Stop();
        }

        _dashboardRefreshDebounceTimer.Start();
    }

    private async void DashboardRefreshDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _dashboardRefreshDebounceTimer.Stop();
        if (!_dashboardRefreshRequested || IsDisposed)
        {
            return;
        }

        _dashboardRefreshRequested = false;
        await RefreshDashboardAsync();
    }

    private void BindDeviceGrid(IReadOnlyList<DeviceRecord> devices, string? selectedDeviceId)
    {
        var snapshot = BuildDeviceSnapshot(devices);
        if (!string.Equals(_deviceGridSnapshot, snapshot, StringComparison.Ordinal))
        {
            _allDeviceGridItems = devices.Select(static item => new DeviceGridItem(item)).ToList();
            RebindDeviceGrid(selectedDeviceId);
            _deviceGridSnapshot = snapshot;
        }
    }

    private void BindLogGrid(IReadOnlyList<AgentPresenceLogRecord> logs)
    {
        var snapshot = BuildLogSnapshot(logs);
        if (string.Equals(_logGridSnapshot, snapshot, StringComparison.Ordinal))
        {
            return;
        }

        _allLogGridItems = logs.Select(static item => new PresenceLogGridItem(item)).ToList();
        RebindLogGrid();
        _logGridSnapshot = snapshot;
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

    private void OpenSelectedDeviceDetails()
    {
        if (_deviceInventoryDetailsFormFactory is null)
        {
            return;
        }

        var selected = GetSelectedDevice();
        if (selected is null)
        {
            return;
        }

        using var detailsForm = _deviceInventoryDetailsFormFactory.Create(selected.Source.DeviceId);
        detailsForm.ShowDialog(this);
    }

    private DeviceGridItem? GetSelectedDevice()
    {
        return gridDevices.CurrentRow?.DataBoundItem as DeviceGridItem;
    }

    private async Task ChangeSelectedDeviceAuthorizationAsync(bool isAuthorized)
    {
        if (_dashboardDataSource is null || _signedInUser is not { CanManageDeviceAuthorization: true })
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
            var success = await _dashboardDataSource.SetDeviceAuthorizationAsync(selected.Source.DeviceId, isAuthorized, _signedInUser.UserName, CancellationToken.None);
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
        btnOpenViewer.Enabled = _dashboardDataSource is { SupportsViewerSessions: true } && selected is { Source.IsOnline: true, Source.IsAuthorized: true };
        btnDeviceDetails.Enabled = selected is not null;
        btnApproveDevice.Enabled = _signedInUser?.CanManageDeviceAuthorization == true && selected is not null && !selected.Source.IsAuthorized;
        btnRevokeDevice.Enabled = _signedInUser?.CanManageDeviceAuthorization == true && selected is not null && selected.Source.IsAuthorized;
    }

    private void InitializeUiText()
    {
        Text = AppBuildInfo.AppendToWindowTitle(HostUiText.Window("遠端桌面 Windows 主控台", "RemoteDesktop Windows Console"));
        lblTitle.Text = AppBuildInfo.AppendToHeading(HostUiText.Bi("遠端桌面 Windows 主控台", "RemoteDesktop Windows Console"));
        HostUiText.ApplyButton(btnApproveDevice, "核准", "Approve");
        HostUiText.ApplyButton(btnAudit, "稽核", "Audit");
        HostUiText.ApplyButton(btnRevokeDevice, "撤銷", "Revoke");
        HostUiText.ApplyButton(btnUsers, "使用者", "Users");
        HostUiText.ApplyButton(btnSettings, "設定", "Settings");
        HostUiText.ApplyButton(btnDeviceDetails, "裝置詳細資訊", "Device Details");
        HostUiText.ApplyButton(btnOpenViewer, "開啟 Viewer", "Open Viewer");
        HostUiText.ApplyButton(btnRefresh, "重新整理", "Refresh");
        lblConsoleNameCaption.Text = HostUiText.Bi("主控台", "Console");
        lblServerUrlCaption.Text = HostUiText.Bi("資料來源", "Data source");
        lblHealthUrlCaption.Text = HostUiText.Bi("健康檢查位址", "Health URL");
        lblSignedInUserCaption.Text = HostUiText.Bi("目前登入使用者", "Signed-in user");
        lblOnlineCountCaption.Text = HostUiText.Bi("在線裝置數", "Online devices");
        lblTotalCountCaption.Text = HostUiText.Bi("裝置總數", "Total devices");
        lblLastRefreshCaption.Text = HostUiText.Bi("最後更新時間", "Last refresh");
        lblDevicesTitle.Text = HostUiText.Bi("已連線裝置", "Connected devices");
        lblLogsTitle.Text = HostUiText.Bi("在線紀錄", "Presence logs");
        lblStatusCaption.Text = HostUiText.Bi("狀態", "Status");
        lblStatusValue.Text = HostUiText.Bi("就緒", "Ready");
        _lblSearch.Text = HostUiText.Bi("查詢主機名稱 / IP", "Search host / IP");
        HostUiText.ApplyButton(_btnApplySearch, "查詢", "Search");
        HostUiText.ApplyButton(_btnClearSearch, "清除", "Clear");

        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.StatusText), "狀態", "Status");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.AccessText), "存取", "Access");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.DeviceId), "裝置 ID", "Device ID");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.DeviceName), "裝置名稱", "Device name");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.HostName), "主機名稱", "Host name");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.RemoteIpAddress), "IP 位址", "IP address");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.Resolution), "解析度", "Resolution");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.AgentVersion), "Agent 版本", "Agent version");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.HardwareSummary), "硬體摘要", "Hardware");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.OsSummary), "作業系統", "OS");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.OfficeSummary), "Office", "Office");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.LastUpdateSummary), "最後更新", "Last update");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.LastSeenAt), "最後上線", "Last seen");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.LastConnectedAt), "最後連線", "Last connected");
        SetGridColumnHeader(gridDevices, nameof(DeviceGridItem.LastDisconnectedAt), "最後離線", "Last disconnected");

        SetGridColumnHeader(gridLogs, nameof(PresenceLogGridItem.DeviceId), "裝置 ID", "Device ID");
        SetGridColumnHeader(gridLogs, nameof(PresenceLogGridItem.DeviceName), "裝置名稱", "Device name");
        SetGridColumnHeader(gridLogs, nameof(PresenceLogGridItem.HostName), "主機名稱", "Host name");
        SetGridColumnHeader(gridLogs, nameof(PresenceLogGridItem.RemoteIpAddress), "IP 位址", "IP address");
        SetGridColumnHeader(gridLogs, nameof(PresenceLogGridItem.ConnectedAt), "連線時間", "Connected at");
        SetGridColumnHeader(gridLogs, nameof(PresenceLogGridItem.LastSeenAt), "最後上線", "Last seen");
        SetGridColumnHeader(gridLogs, nameof(PresenceLogGridItem.DisconnectedAt), "離線時間", "Disconnected at");
        SetGridColumnHeader(gridLogs, nameof(PresenceLogGridItem.DisconnectReason), "離線原因", "Disconnect reason");
        SetGridColumnHeader(gridLogs, nameof(PresenceLogGridItem.OnlineSeconds), "在線秒數", "Online seconds");
    }

    private void InitializeGridBehavior()
    {
        EnsureSearchInputBehavior();
        EnsureIpColumns();
        ConfigureGridForStableRefresh(gridDevices);
        ConfigureGridForStableRefresh(gridLogs);

        gridDevices.DataSource = _deviceBindingSource;
        gridLogs.DataSource = _logBindingSource;

        gridDevices.ColumnHeaderMouseClick += gridDevices_ColumnHeaderMouseClick;
        gridLogs.ColumnHeaderMouseClick += gridLogs_ColumnHeaderMouseClick;
    }

    private void InitializeResponsiveLayout()
    {
        layoutRoot.RowStyles[0].Height = 126F;
        layoutRoot.RowStyles[1].Height = 176F;

        ConfigureSummaryLabel(lblConsoleNameCaption, isValue: false);
        ConfigureSummaryLabel(lblConsoleNameValue, isValue: true);
        ConfigureSummaryLabel(lblServerUrlCaption, isValue: false);
        ConfigureSummaryLabel(lblServerUrlValue, isValue: true);
        ConfigureSummaryLabel(lblHealthUrlCaption, isValue: false);
        ConfigureSummaryLabel(lblHealthUrlValue, isValue: true);
        ConfigureSummaryLabel(lblSignedInUserCaption, isValue: false);
        ConfigureSummaryLabel(lblSignedInUserValue, isValue: true);
        ConfigureSummaryLabel(lblOnlineCountCaption, isValue: false);
        ConfigureSummaryLabel(lblOnlineCountValue, isValue: true);
        ConfigureSummaryLabel(lblTotalCountCaption, isValue: false);
        ConfigureSummaryLabel(lblTotalCountValue, isValue: true);
        ConfigureSummaryLabel(lblLastRefreshCaption, isValue: false);
        ConfigureSummaryLabel(lblLastRefreshValue, isValue: true);

        RefreshResponsiveLayout();
    }

    private static void ConfigureGridForStableRefresh(DataGridView grid)
    {
        TryEnableDoubleBuffer(grid);
        grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
        grid.AllowUserToResizeRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.MultiSelect = false;

        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.SortMode = DataGridViewColumnSortMode.Programmatic;
        }
    }

    private void gridDevices_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 || e.ColumnIndex >= gridDevices.Columns.Count)
        {
            return;
        }

        var column = gridDevices.Columns[e.ColumnIndex];
        _deviceGridSortState = BuildNextSortState(_deviceGridSortState, column.DataPropertyName);
        RebindDeviceGrid(GetSelectedDevice()?.Source.DeviceId);
    }

    private void gridLogs_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 || e.ColumnIndex >= gridLogs.Columns.Count)
        {
            return;
        }

        var column = gridLogs.Columns[e.ColumnIndex];
        _logGridSortState = BuildNextSortState(_logGridSortState, column.DataPropertyName);
        RebindLogGrid();
    }

    private void InitializeSearchUi()
    {
        _lblSearch.AutoSize = true;
        _lblSearch.Name = "lblSearch";

        _txtSearch.Name = "txtSearch";
        _txtSearch.Size = new Size(240, 23);
        _txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _btnApplySearch.Name = "btnApplySearch";
        _btnApplySearch.Size = new Size(62, 28);
        _btnApplySearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnApplySearch.Click += btnApplySearch_Click;

        _btnClearSearch.Name = "btnClearSearch";
        _btnClearSearch.Size = new Size(62, 28);
        _btnClearSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnClearSearch.Click += btnClearSearch_Click;

        splitMain.Panel1.Controls.Add(_lblSearch);
        splitMain.Panel1.Controls.Add(_txtSearch);
        splitMain.Panel1.Controls.Add(_btnApplySearch);
        splitMain.Panel1.Controls.Add(_btnClearSearch);
        splitMain.Panel1.Resize += (_, _) => RefreshSearchLayout();
        RefreshSearchLayout();
    }

    private void EnsureSearchInputBehavior()
    {
        _txtSearch.KeyDown += txtSearch_KeyDown;
    }

    private void EnsureIpColumns()
    {
        InsertGridColumnIfMissing(gridDevices, nameof(DeviceGridItem.RemoteIpAddress), 5, "IP", 95F);
        InsertGridColumnIfMissing(gridLogs, nameof(PresenceLogGridItem.RemoteIpAddress), 3, "IP", 95F);
    }

    private static void InsertGridColumnIfMissing(DataGridView grid, string dataPropertyName, int index, string headerText, float fillWeight)
    {
        if (grid.Columns.Cast<DataGridViewColumn>().Any(column => string.Equals(column.DataPropertyName, dataPropertyName, StringComparison.Ordinal)))
        {
            return;
        }

        var column = new DataGridViewTextBoxColumn
        {
            DataPropertyName = dataPropertyName,
            HeaderText = headerText,
            FillWeight = fillWeight
        };

        grid.Columns.Insert(index, column);
    }

    private static void SetGridColumnHeader(DataGridView grid, string dataPropertyName, string zhTw, string enUs)
    {
        var column = grid.Columns.Cast<DataGridViewColumn>()
            .FirstOrDefault(item => string.Equals(item.DataPropertyName, dataPropertyName, StringComparison.Ordinal));
        if (column is not null)
        {
            column.HeaderText = HostUiText.Bi(zhTw, enUs);
        }
    }

    private static void TryEnableDoubleBuffer(DataGridView grid)
    {
        typeof(DataGridView).InvokeMember(
            "DoubleBuffered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
            null,
            grid,
            [true]);
    }

    private void btnApplySearch_Click(object? sender, EventArgs e)
    {
        ApplySearchFromInput();
    }

    private void btnClearSearch_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtSearch.Text) && string.IsNullOrWhiteSpace(_searchKeyword))
        {
            return;
        }

        _txtSearch.Text = string.Empty;
        ApplySearchFromInput();
    }

    private void txtSearch_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        ApplySearchFromInput();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void ApplySearchFromInput()
    {
        var nextKeyword = _txtSearch.Text.Trim();
        if (string.Equals(_searchKeyword, nextKeyword, StringComparison.Ordinal))
        {
            return;
        }

        _searchKeyword = nextKeyword;
        _btnClearSearch.Enabled = !string.IsNullOrWhiteSpace(_searchKeyword);
        RebindDeviceGrid(GetSelectedDevice()?.Source.DeviceId);
        RebindLogGrid();
    }

    private void RebindDeviceGrid(string? selectedDeviceId)
    {
        _deviceGridItems = ApplySearch(_allDeviceGridItems, static (item, keyword) => item.MatchesSearch(keyword));
        ApplySort(_deviceGridItems, _deviceGridSortState, GetDeviceGridSortValue);
        UpdateBindingSource(gridDevices, _deviceBindingSource, _deviceGridItems);
        RestoreDeviceSelection(selectedDeviceId);
    }

    private void RebindLogGrid()
    {
        _logGridItems = ApplySearch(_allLogGridItems, static (item, keyword) => item.MatchesSearch(keyword));
        ApplySort(_logGridItems, _logGridSortState, GetPresenceLogGridSortValue);
        UpdateBindingSource(gridLogs, _logBindingSource, _logGridItems);
    }

    private List<TItem> ApplySearch<TItem>(IReadOnlyList<TItem> items, Func<TItem, string, bool> matches)
    {
        if (string.IsNullOrWhiteSpace(_searchKeyword))
        {
            return items.ToList();
        }

        return items.Where(item => matches(item, _searchKeyword)).ToList();
    }

    private void RestoreDeviceSelection(string? selectedDeviceId)
    {
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

            gridDevices.CurrentCell = row.Cells[0];
            return;
        }
    }

    private void UpdateBindingSource<TItem>(DataGridView grid, BindingSource bindingSource, List<TItem> items)
    {
        var selectedKey = GetSelectedKey(grid);
        var currentColumnIndex = GetCurrentColumnIndex(grid);
        var firstDisplayedRowIndex = GetFirstDisplayedRowIndex(grid);

        using var redrawScope = new RedrawScope(grid);
        grid.SuspendLayout();
        bindingSource.DataSource = items;

        RestoreSelectedRow(grid, selectedKey, currentColumnIndex);
        RestoreFirstDisplayedRow(grid, firstDisplayedRowIndex);
        ApplySortGlyphs(grid, grid == gridDevices ? _deviceGridSortState : _logGridSortState);
        grid.ResumeLayout();
    }

    private static string? GetSelectedKey(DataGridView grid)
    {
        var item = grid.CurrentRow?.DataBoundItem;

        return item switch
        {
            DeviceGridItem deviceItem => deviceItem.DeviceId,
            PresenceLogGridItem logItem => logItem.RowKey,
            _ => null
        };
    }

    private static int GetCurrentColumnIndex(DataGridView grid)
    {
        if (grid.CurrentCell is null)
        {
            return 0;
        }

        return grid.CurrentCell.ColumnIndex >= 0 ? grid.CurrentCell.ColumnIndex : 0;
    }

    private static int? GetFirstDisplayedRowIndex(DataGridView grid)
    {
        try
        {
            var rowIndex = grid.FirstDisplayedScrollingRowIndex;
            return rowIndex >= 0 ? rowIndex : null;
        }
        catch
        {
            return null;
        }
    }

    private static void RestoreSelectedRow(DataGridView grid, string? selectedKey, int currentColumnIndex)
    {
        if (string.IsNullOrWhiteSpace(selectedKey))
        {
            return;
        }

        foreach (DataGridViewRow row in grid.Rows)
        {
            var key = row.DataBoundItem switch
            {
                DeviceGridItem deviceItem => deviceItem.DeviceId,
                PresenceLogGridItem logItem => logItem.RowKey,
                _ => null
            };

            if (!string.Equals(key, selectedKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (row.Cells.Count > 0)
            {
                var targetColumnIndex = Math.Clamp(currentColumnIndex, 0, row.Cells.Count - 1);
                grid.CurrentCell = row.Cells[targetColumnIndex];
            }

            return;
        }
    }

    private static void RestoreFirstDisplayedRow(DataGridView grid, int? firstDisplayedRowIndex)
    {
        if (!firstDisplayedRowIndex.HasValue)
        {
            return;
        }

        if (grid.Rows.Count == 0)
        {
            return;
        }

        var targetIndex = Math.Clamp(firstDisplayedRowIndex.Value, 0, grid.Rows.Count - 1);
        try
        {
            grid.FirstDisplayedScrollingRowIndex = targetIndex;
        }
        catch
        {
        }
    }

    private static GridSortState BuildNextSortState(GridSortState? currentState, string dataPropertyName)
    {
        var direction = currentState is not null &&
                        string.Equals(currentState.ColumnName, dataPropertyName, StringComparison.Ordinal) &&
                        currentState.Direction == SortOrder.Ascending
            ? SortOrder.Descending
            : SortOrder.Ascending;

        return new GridSortState(dataPropertyName, direction);
    }

    private static void ApplySort<TItem>(List<TItem> items, GridSortState? sortState, Func<TItem, string, IComparable> getSortValue)
    {
        if (sortState is null || string.IsNullOrWhiteSpace(sortState.ColumnName))
        {
            return;
        }

        items.Sort((left, right) =>
        {
            var leftValue = getSortValue(left, sortState.ColumnName);
            var rightValue = getSortValue(right, sortState.ColumnName);
            var result = Comparer<IComparable>.Default.Compare(leftValue, rightValue);
            if (result == 0)
            {
                result = StringComparer.OrdinalIgnoreCase.Compare(left?.ToString(), right?.ToString());
            }

            return sortState.Direction == SortOrder.Descending ? -result : result;
        });
    }

    private static void ApplySortGlyphs(DataGridView grid, GridSortState? sortState)
    {
        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.HeaderCell.SortGlyphDirection = SortOrder.None;
        }

        if (sortState is null)
        {
            return;
        }

        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (!string.Equals(column.DataPropertyName, sortState.ColumnName, StringComparison.Ordinal))
            {
                continue;
            }

            column.HeaderCell.SortGlyphDirection = sortState.Direction;
            return;
        }
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        RefreshResponsiveLayout();
    }

    private void RefreshResponsiveLayout()
    {
        var maxTitleWidth = Math.Max(320, btnAudit.Left - lblTitle.Left - 24);
        var measuredTitleSize = TextRenderer.MeasureText(
            lblTitle.Text,
            lblTitle.Font,
            new Size(maxTitleWidth, int.MaxValue),
            TextFormatFlags.WordBreak);

        lblTitle.AutoSize = false;
        lblTitle.Location = new Point(16, 12);
        lblTitle.Size = new Size(maxTitleWidth, measuredTitleSize.Height);
    }

    private void RefreshSearchLayout()
    {
        if (!splitMain.Panel1.IsHandleCreated && splitMain.Panel1.ClientSize.Width <= 0)
        {
            return;
        }

        const int top = 8;
        const int rightPadding = 10;
        const int gap = 6;
        const int buttonWidth = 62;
        const int minTextWidth = 200;
        const int maxTextWidth = 280;

        _lblSearch.Location = new Point(220, 12);

        var clearX = splitMain.Panel1.ClientSize.Width - rightPadding - buttonWidth;
        var applyX = clearX - gap - buttonWidth;
        var textWidth = Math.Clamp(applyX - gap - (_lblSearch.Right + gap), minTextWidth, maxTextWidth);
        var textX = applyX - gap - textWidth;

        _txtSearch.Location = new Point(textX, top);
        _txtSearch.Size = new Size(textWidth, 23);
        _btnApplySearch.Location = new Point(applyX, top - 1);
        _btnClearSearch.Location = new Point(clearX, top - 1);
    }

    private static void ConfigureSummaryLabel(Label label, bool isValue)
    {
        label.AutoSize = false;
        label.Dock = DockStyle.Fill;
        label.AutoEllipsis = true;
        label.Margin = isValue ? new Padding(6, 0, 12, 0) : new Padding(12, 0, 6, 0);
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private static string BuildDeviceSnapshot(IReadOnlyList<DeviceRecord> devices)
    {
        return string.Join(
            '\n',
            devices
                .OrderBy(static item => item.DeviceId, StringComparer.OrdinalIgnoreCase)
                .Select(static item => string.Join(
                    '|',
                    item.DeviceId,
                    item.DeviceName,
                    item.HostName,
                    item.RemoteIpAddress,
                    item.IsOnline,
                    item.IsAuthorized,
                    item.ScreenWidth,
                    item.ScreenHeight,
                    item.AgentVersion,
                    item.LastSeenAt.UtcDateTime.Ticks,
                    item.LastConnectedAt?.UtcDateTime.Ticks ?? 0,
                    item.LastDisconnectedAt?.UtcDateTime.Ticks ?? 0,
                    item.Inventory?.CpuName ?? string.Empty,
                    item.Inventory?.InstalledMemoryBytes ?? 0,
                    item.Inventory?.OsName ?? string.Empty,
                    item.Inventory?.OsVersion ?? string.Empty,
                    item.Inventory?.OsBuildNumber ?? string.Empty,
                    item.Inventory?.OfficeVersion ?? string.Empty,
                    item.Inventory?.LastWindowsUpdateTitle ?? string.Empty,
                    item.Inventory?.LastWindowsUpdateInstalledAt?.UtcDateTime.Ticks ?? 0)));
    }

    private static string BuildLogSnapshot(IReadOnlyList<AgentPresenceLogRecord> logs)
    {
        return string.Join(
            '\n',
            logs
                .OrderBy(static item => item.PresenceId)
                .Select(static item => string.Join(
                    '|',
                    item.PresenceId,
                    item.DeviceId,
                    item.DeviceName,
                    item.HostName,
                    item.RemoteIpAddress,
                    item.ConnectedAt.UtcDateTime.Ticks,
                    item.LastSeenAt.UtcDateTime.Ticks,
                    item.DisconnectedAt?.UtcDateTime.Ticks ?? 0,
                    item.DisconnectReason ?? string.Empty,
                    item.OnlineSeconds)));
    }

    private static IComparable GetDeviceGridSortValue(DeviceGridItem item, string propertyName)
    {
        return propertyName switch
        {
            nameof(DeviceGridItem.StatusText) => item.Source.IsOnline ? 1 : 0,
            nameof(DeviceGridItem.AccessText) => item.Source.IsAuthorized ? 1 : 0,
            nameof(DeviceGridItem.DeviceId) => item.DeviceId,
            nameof(DeviceGridItem.DeviceName) => item.DeviceName,
            nameof(DeviceGridItem.HostName) => item.HostName,
            nameof(DeviceGridItem.RemoteIpAddress) => item.RemoteIpAddress,
            nameof(DeviceGridItem.Resolution) => item.Source.ScreenWidth * 100000 + item.Source.ScreenHeight,
            nameof(DeviceGridItem.AgentVersion) => item.AgentVersion,
            nameof(DeviceGridItem.HardwareSummary) => item.HardwareSummary,
            nameof(DeviceGridItem.OsSummary) => item.OsSummary,
            nameof(DeviceGridItem.OfficeSummary) => item.OfficeSummary,
            nameof(DeviceGridItem.LastUpdateSummary) => item.LastUpdateSummary,
            nameof(DeviceGridItem.LastSeenAt) => item.Source.LastSeenAt.UtcDateTime,
            nameof(DeviceGridItem.LastConnectedAt) => item.Source.LastConnectedAt?.UtcDateTime ?? DateTime.MinValue,
            nameof(DeviceGridItem.LastDisconnectedAt) => item.Source.LastDisconnectedAt?.UtcDateTime ?? DateTime.MinValue,
            _ => item.DeviceId
        };
    }

    private static IComparable GetPresenceLogGridSortValue(PresenceLogGridItem item, string propertyName)
    {
        return propertyName switch
        {
            nameof(PresenceLogGridItem.DeviceId) => item.DeviceId,
            nameof(PresenceLogGridItem.DeviceName) => item.DeviceName,
            nameof(PresenceLogGridItem.HostName) => item.HostName,
            nameof(PresenceLogGridItem.RemoteIpAddress) => item.RemoteIpAddress,
            nameof(PresenceLogGridItem.ConnectedAt) => item.Source.ConnectedAt.UtcDateTime,
            nameof(PresenceLogGridItem.LastSeenAt) => item.Source.LastSeenAt.UtcDateTime,
            nameof(PresenceLogGridItem.DisconnectedAt) => item.Source.DisconnectedAt?.UtcDateTime ?? DateTime.MinValue,
            nameof(PresenceLogGridItem.DisconnectReason) => item.DisconnectReason,
            nameof(PresenceLogGridItem.OnlineSeconds) => item.Source.OnlineSeconds,
            _ => item.RowKey
        };
    }

    private sealed record GridSortState(string ColumnName, SortOrder Direction);

    private sealed class RedrawScope : IDisposable
    {
        private const int WmSetRedraw = 0x000B;
        private readonly Control _control;

        public RedrawScope(Control control)
        {
            _control = control;
            if (_control.IsHandleCreated)
            {
                SendMessage(_control.Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public void Dispose()
        {
            if (!_control.IsHandleCreated)
            {
                return;
            }

            SendMessage(_control.Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
            _control.Invalidate(true);
            _control.Update();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
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
            RemoteIpAddress = string.IsNullOrWhiteSpace(source.RemoteIpAddress) ? "-" : source.RemoteIpAddress;
            Resolution = $"{source.ScreenWidth} x {source.ScreenHeight}";
            AgentVersion = source.AgentVersion;
            HardwareSummary = BuildHardwareSummary(source.Inventory);
            OsSummary = BuildOsSummary(source.Inventory);
            OfficeSummary = BuildOfficeSummary(source.Inventory);
            LastUpdateSummary = BuildLastUpdateSummary(source.Inventory);
            LastSeenAt = FormatDisplayTimestamp(source.LastSeenAt);
            LastConnectedAt = source.LastConnectedAt.HasValue ? FormatDisplayTimestamp(source.LastConnectedAt.Value) : "-";
            LastDisconnectedAt = source.LastDisconnectedAt.HasValue ? FormatDisplayTimestamp(source.LastDisconnectedAt.Value) : "-";
        }

        public DeviceRecord Source { get; }
        public string StatusText { get; }
        public string AccessText { get; }
        public string DeviceId { get; }
        public string DeviceName { get; }
        public string HostName { get; }
        public string RemoteIpAddress { get; }
        public string Resolution { get; }
        public string AgentVersion { get; }
        public string HardwareSummary { get; }
        public string OsSummary { get; }
        public string OfficeSummary { get; }
        public string LastUpdateSummary { get; }
        public string LastSeenAt { get; }
        public string LastConnectedAt { get; }
        public string LastDisconnectedAt { get; }

        private static string BuildHardwareSummary(AgentInventoryProfile? inventory)
        {
            if (inventory is null)
            {
                return "-";
            }

            var memoryText = inventory.InstalledMemoryBytes > 0
                ? FormatBytes(inventory.InstalledMemoryBytes)
                : "未知記憶體";
            return $"{TrimForGrid(inventory.CpuName, 40)} / {memoryText}";
        }

        private static string BuildOsSummary(AgentInventoryProfile? inventory)
        {
            if (inventory is null)
            {
                return "-";
            }

            var name = string.IsNullOrWhiteSpace(inventory.OsName) ? "未知作業系統" : inventory.OsName;
            var version = string.IsNullOrWhiteSpace(inventory.OsVersion) ? "?" : inventory.OsVersion;
            var build = string.IsNullOrWhiteSpace(inventory.OsBuildNumber) ? "?" : inventory.OsBuildNumber;
            return $"{TrimForGrid(name, 30)} {version} ({build})";
        }

        private static string BuildOfficeSummary(AgentInventoryProfile? inventory)
        {
            return inventory is null ? "-" : TrimForGrid(inventory.OfficeVersion, 36);
        }

        private static string BuildLastUpdateSummary(AgentInventoryProfile? inventory)
        {
            if (inventory is null)
            {
                return "-";
            }

            var title = string.IsNullOrWhiteSpace(inventory.LastWindowsUpdateTitle)
                ? "未知更新"
                : TrimForGrid(inventory.LastWindowsUpdateTitle, 30);
            var date = inventory.LastWindowsUpdateInstalledAt?.ToString("yyyy-MM-dd") ?? "未知日期";
            return $"{title} / {date}";
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
            var value = (double)Math.Max(bytes, 0);
            var unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return $"{value:0.##} {units[unitIndex]}";
        }

        private static string TrimForGrid(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..(maxLength - 3)]}...";
        }

        public bool MatchesSearch(string keyword)
        {
            return ContainsSearch(DeviceId, keyword)
                || ContainsSearch(DeviceName, keyword)
                || ContainsSearch(HostName, keyword)
                || ContainsSearch(RemoteIpAddress, keyword);
        }
    }

    private sealed class PresenceLogGridItem
    {
        public PresenceLogGridItem(AgentPresenceLogRecord source)
        {
            Source = source;
            RowKey = source.PresenceId.ToString("N");
            DeviceId = source.DeviceId;
            DeviceName = source.DeviceName;
            HostName = source.HostName;
            RemoteIpAddress = string.IsNullOrWhiteSpace(source.RemoteIpAddress) ? "-" : source.RemoteIpAddress;
            ConnectedAt = FormatDisplayTimestamp(source.ConnectedAt);
            LastSeenAt = FormatDisplayTimestamp(source.LastSeenAt);
            DisconnectedAt = source.DisconnectedAt.HasValue ? FormatDisplayTimestamp(source.DisconnectedAt.Value) : "-";
            DisconnectReason = string.IsNullOrWhiteSpace(source.DisconnectReason) ? "-" : FormatDisconnectReason(source.DisconnectReason);
            OnlineSeconds = source.OnlineSeconds.ToString();
        }

        public AgentPresenceLogRecord Source { get; }
        public string RowKey { get; }
        public string DeviceId { get; }
        public string DeviceName { get; }
        public string HostName { get; }
        public string RemoteIpAddress { get; }
        public string ConnectedAt { get; }
        public string LastSeenAt { get; }
        public string DisconnectedAt { get; }
        public string DisconnectReason { get; }
        public string OnlineSeconds { get; }

        public bool MatchesSearch(string keyword)
        {
            return ContainsSearch(DeviceId, keyword)
                || ContainsSearch(DeviceName, keyword)
                || ContainsSearch(HostName, keyword)
                || ContainsSearch(RemoteIpAddress, keyword);
        }
    }

    private static bool ContainsSearch(string? value, string keyword)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDisplayTimestamp(DateTimeOffset value)
    {
        return value.ToString(DisplayTimestampFormat);
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

