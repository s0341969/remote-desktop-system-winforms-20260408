using RemoteDesktop.Host.Services.Settings;

namespace RemoteDesktop.Host.Forms.Settings;

public partial class HostSettingsForm : Form
{
    private IHostSettingsStore? _settingsStore;
    private bool _showResultDialogs;

    public HostSettingsForm()
        : this(true)
    {
    }

    public HostSettingsForm(bool showResultDialogs)
    {
        _showResultDialogs = showResultDialogs;
        InitializeComponent();
    }

    public void Bind(IHostSettingsStore settingsStore, HostSettingsDocument document, bool showResultDialogs = true)
    {
        _settingsStore = settingsStore;
        _showResultDialogs = showResultDialogs;
        txtConnectionString.Text = document.RemoteDesktopDbConnectionString;
        txtServerUrl.Text = document.ServerUrl;
        txtConsoleName.Text = document.ConsoleName;
        txtAdminUserName.Text = document.AdminUserName;
        txtAdminPassword.Text = document.AdminPassword;
        txtSharedAccessKey.Text = document.SharedAccessKey;
        chkRequireHttpsRedirect.Checked = document.RequireHttpsRedirect;
        numHeartbeatTimeout.Value = Math.Clamp(document.AgentHeartbeatTimeoutSeconds, (int)numHeartbeatTimeout.Minimum, (int)numHeartbeatTimeout.Maximum);
        lblStatus.Text = "修改後按儲存，重新啟動 Host 後生效。";
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
            var document = new HostSettingsDocument
            {
                RemoteDesktopDbConnectionString = txtConnectionString.Text.Trim(),
                ServerUrl = txtServerUrl.Text.Trim(),
                ConsoleName = txtConsoleName.Text.Trim(),
                AdminUserName = txtAdminUserName.Text.Trim(),
                AdminPassword = txtAdminPassword.Text,
                SharedAccessKey = txtSharedAccessKey.Text,
                RequireHttpsRedirect = chkRequireHttpsRedirect.Checked,
                AgentHeartbeatTimeoutSeconds = (int)numHeartbeatTimeout.Value
            };

            await _settingsStore.SaveAsync(document, CancellationToken.None);
            lblStatus.Text = "已儲存，重新啟動 Host 後生效。";
            if (_showResultDialogs)
            {
                MessageBox.Show("Host 設定已儲存，重新啟動後生效。", "RemoteDesktop.Host", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show($"儲存 Host 設定失敗：{exception.Message}", "RemoteDesktop.Host", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            btnSave.Enabled = true;
            btnCancel.Enabled = true;
        }
    }
}
