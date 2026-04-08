using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;
using RemoteDesktop.Host.Forms.Settings;

namespace RemoteDesktop.Host.Forms;

public partial class MainForm : Form
{
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private IDeviceRepository? _repository;
    private ControlServerOptions? _options;
    private RemoteViewerFormFactory? _remoteViewerFormFactory;
    private HostSettingsFormFactory? _hostSettingsFormFactory;
    private string _signedInUserName = string.Empty;
    private bool _refreshing;
    private string? _lastRefreshErrorMessage;

    public MainForm()
    {
        InitializeComponent();
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 5000
        };
        _refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();
    }

    public void Bind(
        IDeviceRepository repository,
        ControlServerOptions options,
        RemoteViewerFormFactory remoteViewerFormFactory,
        HostSettingsFormFactory hostSettingsFormFactory,
        string signedInUserName)
    {
        _repository = repository;
        _options = options;
        _remoteViewerFormFactory = remoteViewerFormFactory;
        _hostSettingsFormFactory = hostSettingsFormFactory;
        _signedInUserName = signedInUserName;
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
        lblSignedInUserValue.Text = _signedInUserName;

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

    private async void btnSettings_Click(object sender, EventArgs e)
    {
        if (_hostSettingsFormFactory is null)
        {
            return;
        }

        using var settingsForm = await _hostSettingsFormFactory.CreateAsync();
        settingsForm.ShowDialog(this);
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
        btnOpenViewer.Enabled = GetSelectedDevice() is { Source.IsOnline: true };
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
            btnSettings.Enabled = false;
            lblStatusValue.Text = "更新中...";

            var devicesTask = _repository.GetDevicesAsync(100, CancellationToken.None);
            var logsTask = _repository.GetPresenceLogsAsync(100, CancellationToken.None);
            await Task.WhenAll(devicesTask, logsTask);

            var devices = devicesTask.Result;
            var logs = logsTask.Result;

            gridDevices.DataSource = devices.Select(static item => new DeviceGridItem(item)).ToList();
            gridLogs.DataSource = logs.Select(static item => new PresenceLogGridItem(item)).ToList();

            var onlineCount = devices.Count(static item => item.IsOnline);
            lblOnlineCountValue.Text = onlineCount.ToString();
            lblTotalCountValue.Text = devices.Count.ToString();
            lblLastRefreshValue.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            lblStatusValue.Text = "就緒";
            _lastRefreshErrorMessage = null;
            btnOpenViewer.Enabled = GetSelectedDevice() is { Source.IsOnline: true };
        }
        catch (Exception exception)
        {
            lblStatusValue.Text = "更新失敗";
            if (showErrorDialog && !string.Equals(_lastRefreshErrorMessage, exception.Message, StringComparison.Ordinal))
            {
                MessageBox.Show(
                    $"讀取儀表板資料失敗：{exception.Message}",
                    "RemoteDesktop.Host",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            _lastRefreshErrorMessage = exception.Message;
        }
        finally
        {
            _refreshing = false;
            btnRefresh.Enabled = true;
            btnSettings.Enabled = true;
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
            MessageBox.Show("目前裝置不在線上，無法開啟遠端畫面。", "RemoteDesktop.Host", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var viewerForm = _remoteViewerFormFactory.Create(selected.Source, _signedInUserName);
        viewerForm.Show(this);
    }

    private DeviceGridItem? GetSelectedDevice()
    {
        return gridDevices.CurrentRow?.DataBoundItem as DeviceGridItem;
    }

    private sealed class DeviceGridItem
    {
        public DeviceGridItem(DeviceRecord source)
        {
            Source = source;
            StatusText = source.IsOnline ? "在線" : "離線";
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
            DisconnectReason = string.IsNullOrWhiteSpace(source.DisconnectReason) ? "-" : source.DisconnectReason;
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
}
