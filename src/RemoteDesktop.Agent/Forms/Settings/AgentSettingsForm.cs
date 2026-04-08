using RemoteDesktop.Agent.Services.Settings;

namespace RemoteDesktop.Agent.Forms.Settings;

public partial class AgentSettingsForm : Form
{
    private IAgentSettingsStore? _settingsStore;
    private bool _showResultDialogs;

    public AgentSettingsForm()
        : this(true)
    {
    }

    public AgentSettingsForm(bool showResultDialogs)
    {
        _showResultDialogs = showResultDialogs;
        InitializeComponent();
    }

    public void Bind(IAgentSettingsStore settingsStore, AgentSettingsDocument document, bool showResultDialogs = true)
    {
        _settingsStore = settingsStore;
        _showResultDialogs = showResultDialogs;
        txtServerUrl.Text = document.ServerUrl;
        txtDeviceId.Text = document.DeviceId;
        txtDeviceName.Text = document.DeviceName;
        txtSharedAccessKey.Text = document.SharedAccessKey;
        numCaptureFps.Value = Math.Clamp(document.CaptureFramesPerSecond, (int)numCaptureFps.Minimum, (int)numCaptureFps.Maximum);
        numJpegQuality.Value = Math.Clamp((decimal)document.JpegQuality, numJpegQuality.Minimum, numJpegQuality.Maximum);
        numMaxFrameWidth.Value = Math.Clamp(document.MaxFrameWidth, (int)numMaxFrameWidth.Minimum, (int)numMaxFrameWidth.Maximum);
        numReconnectDelay.Value = Math.Clamp(document.ReconnectDelaySeconds, (int)numReconnectDelay.Minimum, (int)numReconnectDelay.Maximum);
        lblStatus.Text = "修改後按儲存，重新啟動 Agent 後生效。";
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        if (_settingsStore is null)
        {
            lblStatus.Text = "設定服務尚未初始化。";
            return;
        }

        btnSave.Enabled = false;
        btnCancel.Enabled = false;
        lblStatus.Text = "儲存中...";

        try
        {
            var document = new AgentSettingsDocument
            {
                ServerUrl = txtServerUrl.Text.Trim(),
                DeviceId = txtDeviceId.Text.Trim(),
                DeviceName = txtDeviceName.Text.Trim(),
                SharedAccessKey = txtSharedAccessKey.Text,
                CaptureFramesPerSecond = (int)numCaptureFps.Value,
                JpegQuality = (long)numJpegQuality.Value,
                MaxFrameWidth = (int)numMaxFrameWidth.Value,
                ReconnectDelaySeconds = (int)numReconnectDelay.Value
            };

            await _settingsStore.SaveAsync(document, CancellationToken.None);
            lblStatus.Text = "已儲存，重新啟動 Agent 後生效。";
            if (_showResultDialogs)
            {
                MessageBox.Show("Agent 設定已儲存，重新啟動後生效。", "RemoteDesktop.Agent", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            if (Modal)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception exception)
        {
            lblStatus.Text = $"儲存失敗：{exception.Message}";
            if (_showResultDialogs)
            {
                MessageBox.Show($"儲存 Agent 設定失敗：{exception.Message}", "RemoteDesktop.Agent", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            btnSave.Enabled = true;
            btnCancel.Enabled = true;
        }
    }
}
