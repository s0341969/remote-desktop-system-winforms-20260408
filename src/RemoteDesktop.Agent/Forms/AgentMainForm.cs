using RemoteDesktop.Agent.Forms.Settings;
using RemoteDesktop.Agent.Services;

namespace RemoteDesktop.Agent.Forms;

public partial class AgentMainForm : Form
{
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private AgentRuntimeState? _runtimeState;
    private AgentSettingsFormFactory? _agentSettingsFormFactory;
    private string[] _lastEvents = Array.Empty<string>();

    public AgentMainForm()
    {
        InitializeComponent();
        InitializeUiText();
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 1500
        };
        _refreshTimer.Tick += (_, _) => RefreshRuntimeState();
    }

    public void Bind(AgentRuntimeState runtimeState, AgentSettingsFormFactory agentSettingsFormFactory)
    {
        _runtimeState = runtimeState;
        _agentSettingsFormFactory = agentSettingsFormFactory;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RefreshRuntimeState();
        _refreshTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void btnActions_Click(object sender, EventArgs e)
    {
        menuActions.Show(btnActions, new Point(0, btnActions.Height));
    }

    private async void menuSettings_Click(object sender, EventArgs e)
    {
        if (_agentSettingsFormFactory is null)
        {
            return;
        }

        using var settingsForm = await _agentSettingsFormFactory.CreateAsync();
        if (settingsForm.ShowDialog(this) == DialogResult.OK)
        {
            MessageBox.Show(
                AgentUiText.Bi("Agent 設定已儲存，請重新啟動 Agent 套用全部變更。", "Agent settings were saved. Restart the Agent application to apply all changes."),
                AgentUiText.Window("Agent 設定", "Agent Settings"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void menuCopyDeviceId_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(lblDeviceIdValue.Text) || lblDeviceIdValue.Text == "-")
        {
            return;
        }

        Clipboard.SetText(lblDeviceIdValue.Text);
    }

    private void menuCopyServerUrl_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(lblServerUrlValue.Text) || lblServerUrlValue.Text == "-")
        {
            return;
        }

        Clipboard.SetText(lblServerUrlValue.Text);
    }

    private void menuRefresh_Click(object sender, EventArgs e)
    {
        RefreshRuntimeState();
    }

    private void RefreshRuntimeState()
    {
        if (_runtimeState is null)
        {
            return;
        }

        var snapshot = _runtimeState.GetSnapshot();
        lblServerUrlValue.Text = snapshot.ServerUrl;
        lblDeviceIdValue.Text = snapshot.DeviceId;
        lblDeviceNameValue.Text = snapshot.DeviceName;
        lblStatusValue.Text = snapshot.CurrentStatus;
        lblLastConnectedValue.Text = snapshot.LastConnectedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        lblLastFrameValue.Text = snapshot.LastFrameSentAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        txtLastError.Text = string.IsNullOrWhiteSpace(snapshot.LastError) ? "-" : snapshot.LastError;

        var events = snapshot.RecentEvents.ToArray();
        if (_lastEvents.SequenceEqual(events))
        {
            return;
        }

        _lastEvents = events;
        listEvents.DataSource = events;
    }

    private void InitializeUiText()
    {
        Text = AppBuildInfo.AppendToWindowTitle(AgentUiText.Window("遠端桌面 Agent", "RemoteDesktop Agent"));
        lblTitle.Text = AppBuildInfo.AppendToHeading(AgentUiText.Bi("遠端桌面 Agent", "RemoteDesktop Agent"));
        AgentUiText.ApplyButton(btnActions, "功能 v", "Actions v");
        menuSettings.Text = AgentUiText.Bi("設定", "Settings");
        menuCopyDeviceId.Text = AgentUiText.Bi("複製裝置 ID", "Copy device ID");
        menuCopyServerUrl.Text = AgentUiText.Bi("複製 Server 位址", "Copy server URL");
        menuRefresh.Text = AgentUiText.Bi("立即重新整理", "Refresh now");
        lblServerUrlCaption.Text = AgentUiText.Bi("Server 位址", "Server URL");
        lblDeviceIdCaption.Text = AgentUiText.Bi("裝置 ID", "Device ID");
        lblDeviceNameCaption.Text = AgentUiText.Bi("裝置名稱", "Device name");
        lblStatusCaption.Text = AgentUiText.Bi("狀態", "Status");
        lblLastConnectedCaption.Text = AgentUiText.Bi("最後連線", "Last connected");
        lblLastFrameCaption.Text = AgentUiText.Bi("最後送出畫面", "Last frame sent");
        lblLastErrorCaption.Text = AgentUiText.Bi("最近錯誤", "Last error");
        lblEventsCaption.Text = AgentUiText.Bi("最近事件", "Recent events");
    }
}

