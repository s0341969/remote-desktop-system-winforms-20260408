using RemoteDesktop.Agent.Services;

namespace RemoteDesktop.Agent.Forms;

public partial class AgentMainForm : Form
{
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private AgentRuntimeState? _runtimeState;

    public AgentMainForm()
    {
        InitializeComponent();
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 1500
        };
        _refreshTimer.Tick += (_, _) => RefreshRuntimeState();
    }

    public void Bind(AgentRuntimeState runtimeState)
    {
        _runtimeState = runtimeState;
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

    private void RefreshRuntimeState()
    {
        if (_runtimeState is null)
        {
            return;
        }

        lblServerUrlValue.Text = _runtimeState.ServerUrl;
        lblDeviceIdValue.Text = _runtimeState.DeviceId;
        lblDeviceNameValue.Text = _runtimeState.DeviceName;
        lblStatusValue.Text = _runtimeState.CurrentStatus;
        lblLastConnectedValue.Text = _runtimeState.LastConnectedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        lblLastFrameValue.Text = _runtimeState.LastFrameSentAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        txtLastError.Text = string.IsNullOrWhiteSpace(_runtimeState.LastError) ? "無" : _runtimeState.LastError;
        listEvents.DataSource = _runtimeState.GetRecentEvents().ToList();
    }
}
