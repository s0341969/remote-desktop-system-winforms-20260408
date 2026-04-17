using RemoteDesktop.Agent.Forms.Settings;
using RemoteDesktop.Agent.Options;
using RemoteDesktop.Agent.Services;
using System.Drawing;

namespace RemoteDesktop.Agent.Forms;

public partial class AgentMainForm : Form
{
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private AgentRuntimeState? _runtimeState;
    private AgentSettingsFormFactory? _agentSettingsFormFactory;
    private AgentOptions? _agentOptions;
    private string[] _lastEvents = Array.Empty<string>();
    private bool _allowExit;
    private bool _startHidden;
    private bool _balloonTipShown;

    public AgentMainForm()
    {
        InitializeComponent();
        InitializeUiText();
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 1500
        };
        _refreshTimer.Tick += (_, _) => RefreshRuntimeState();
        notifyAgent.Icon = SystemIcons.Application;
    }

    public void Bind(
        AgentRuntimeState runtimeState,
        AgentSettingsFormFactory agentSettingsFormFactory,
        AgentOptions agentOptions)
    {
        _runtimeState = runtimeState;
        _agentSettingsFormFactory = agentSettingsFormFactory;
        _agentOptions = agentOptions;
        _startHidden = agentOptions.StartHidden;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RefreshRuntimeState();
        _refreshTimer.Start();
        if (_startHidden)
        {
            BeginInvoke(new Action(HideToTray));
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        notifyAgent.Visible = false;
        notifyAgent.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized && Visible)
        {
            HideToTray();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    private void btnActions_Click(object sender, EventArgs e)
    {
        menuActions.Show(btnActions, new Point(0, btnActions.Height));
    }

    private async void menuSettings_Click(object sender, EventArgs e)
    {
        await ShowSettingsAsync();
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

    private void notifyAgent_DoubleClick(object? sender, EventArgs e)
    {
        ShowFromTray();
    }

    private void menuTrayShow_Click(object sender, EventArgs e)
    {
        ShowFromTray();
    }

    private async void menuTraySettings_Click(object sender, EventArgs e)
    {
        ShowFromTray();
        await ShowSettingsAsync();
    }

    private void menuTrayExit_Click(object sender, EventArgs e)
    {
        _allowExit = true;
        notifyAgent.Visible = false;
        Close();
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

    private async Task ShowSettingsAsync()
    {
        if (_agentSettingsFormFactory is null)
        {
            return;
        }

        using var settingsForm = await _agentSettingsFormFactory.CreateAsync();
        var dialogOwner = Visible ? this : null;
        if (settingsForm.ShowDialog(dialogOwner) == DialogResult.OK)
        {
            MessageBox.Show(
                AgentUiText.Bi("Agent 設定已儲存，請重新啟動 Agent 套用全部變更。", "Agent settings were saved. Restart the Agent application to apply all changes."),
                AgentUiText.Window("Agent 設定", "Agent Settings"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        WindowState = FormWindowState.Normal;
        if (!_balloonTipShown)
        {
            notifyAgent.BalloonTipTitle = AgentUiText.Window("遠端桌面 Agent", "RemoteDesktop Agent");
            notifyAgent.BalloonTipText = AgentUiText.Bi("Agent 已隱藏到系統匣，雙擊圖示可重新開啟。", "The Agent is running in the system tray. Double-click the icon to open it again.");
            notifyAgent.ShowBalloonTip(2500);
            _balloonTipShown = true;
        }
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
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
        notifyAgent.Text = AgentUiText.Window("遠端桌面 Agent", "RemoteDesktop Agent");
        menuTrayShow.Text = AgentUiText.Bi("顯示主視窗", "Show window");
        menuTraySettings.Text = AgentUiText.Bi("開啟設定", "Open settings");
        menuTrayExit.Text = AgentUiText.Bi("結束", "Exit");
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

