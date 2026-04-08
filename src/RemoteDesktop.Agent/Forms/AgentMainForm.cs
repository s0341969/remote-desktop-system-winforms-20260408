using RemoteDesktop.Agent.Forms.Settings;
using RemoteDesktop.Agent.Services;

namespace RemoteDesktop.Agent.Forms;

public partial class AgentMainForm : Form
{
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private AgentRuntimeState? _runtimeState;
    private AgentSettingsFormFactory? _agentSettingsFormFactory;

    public AgentMainForm()
    {
        InitializeComponent();
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

    private async void btnSettings_Click(object sender, EventArgs e)
    {
        if (_agentSettingsFormFactory is null)
        {
            return;
        }

        using var settingsForm = await _agentSettingsFormFactory.CreateAsync();
        settingsForm.ShowDialog(this);
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
        txtLastError.Text = string.IsNullOrWhiteSpace(snapshot.LastError) ? "無" : snapshot.LastError;
        listEvents.DataSource = snapshot.RecentEvents.ToList();
    }
}
