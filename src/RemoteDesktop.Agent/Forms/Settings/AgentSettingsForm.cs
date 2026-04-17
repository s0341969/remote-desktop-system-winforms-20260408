using RemoteDesktop.Agent.Services.Settings;
using RemoteDesktop.Agent.Services;

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
        InitializeUiText();
    }

    public void Bind(IAgentSettingsStore settingsStore, AgentSettingsDocument document, bool showResultDialogs = true)
    {
        _settingsStore = settingsStore;
        _showResultDialogs = showResultDialogs;
        txtServerUrl.Text = document.ServerUrl;
        txtDeviceId.Text = document.DeviceId;
        txtDeviceName.Text = document.DeviceName;
        txtDeviceId.ReadOnly = true;
        txtDeviceName.ReadOnly = true;
        txtSharedAccessKey.Text = document.SharedAccessKey;
        txtFileTransferDirectory.Text = document.FileTransferDirectory;
        numCaptureFps.Value = Math.Clamp(document.CaptureFramesPerSecond, (int)numCaptureFps.Minimum, (int)numCaptureFps.Maximum);
        numJpegQuality.Value = Math.Clamp((decimal)document.JpegQuality, numJpegQuality.Minimum, numJpegQuality.Maximum);
        numMaxFrameWidth.Value = Math.Clamp(document.MaxFrameWidth, (int)numMaxFrameWidth.Minimum, (int)numMaxFrameWidth.Maximum);
        numReconnectDelay.Value = Math.Clamp(document.ReconnectDelaySeconds, (int)numReconnectDelay.Minimum, (int)numReconnectDelay.Maximum);
        lblStatus.Text = AgentUiText.Bi("裝置 ID 與裝置名稱會固定使用本機主機名稱；儲存後會更新 appsettings.json，重新啟動後生效。", "Device ID and device name are fixed to the local machine name. Saving updates appsettings.json and takes effect after restart.");
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        if (_settingsStore is null)
        {
            lblStatus.Text = AgentUiText.Bi("設定儲存服務尚未初始化。", "Settings store is not initialized.");
            return;
        }

        btnSave.Enabled = false;
        btnCancel.Enabled = false;
        lblStatus.Text = AgentUiText.Bi("正在儲存設定...", "Saving settings...");

        try
        {
            var document = new AgentSettingsDocument
            {
                ServerUrl = txtServerUrl.Text.Trim(),
                DeviceId = AgentIdentity.GetMachineIdentity(),
                DeviceName = AgentIdentity.GetMachineIdentity(),
                SharedAccessKey = txtSharedAccessKey.Text,
                FileTransferDirectory = txtFileTransferDirectory.Text.Trim(),
                CaptureFramesPerSecond = (int)numCaptureFps.Value,
                JpegQuality = (long)numJpegQuality.Value,
                MaxFrameWidth = (int)numMaxFrameWidth.Value,
                ReconnectDelaySeconds = (int)numReconnectDelay.Value
            };

            await _settingsStore.SaveAsync(document, CancellationToken.None);
            lblStatus.Text = AgentUiText.Bi("設定已儲存，請重新啟動 Agent 套用新設定。", "Settings saved successfully. Restart Agent to apply the new configuration.");
            if (_showResultDialogs)
            {
                MessageBox.Show(AgentUiText.Bi("Agent 設定已儲存，請重新啟動 Agent 套用新設定。", "Agent settings were saved successfully. Restart Agent to apply the new configuration."), AgentUiText.Window("Agent 設定", "Agent Settings"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            if (Modal)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception exception)
        {
            lblStatus.Text = AgentUiText.Bi($"儲存失敗：{exception.Message}", $"Save failed: {exception.Message}");
            if (_showResultDialogs)
            {
                MessageBox.Show(AgentUiText.Bi($"Agent 設定儲存失敗：{exception.Message}", $"Failed to save Agent settings: {exception.Message}"), AgentUiText.Window("Agent 設定", "Agent Settings"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            btnSave.Enabled = true;
            btnCancel.Enabled = true;
        }
    }

    private void InitializeUiText()
    {
        Text = AgentUiText.Window("Agent 設定", "Agent Settings");
        lblTitle.Text = AgentUiText.Bi("Agent 設定", "Agent Settings");
        AgentUiText.ApplyButton(btnSave, "儲存", "Save");
        AgentUiText.ApplyButton(btnCancel, "取消", "Cancel");
    }
}
