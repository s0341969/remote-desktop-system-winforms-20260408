using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services.Auditing;
using RemoteDesktop.Host.Services.Settings;

namespace RemoteDesktop.Host.Forms.Settings;

public partial class HostSettingsForm : Form
{
    private IHostSettingsStore? _settingsStore;
    private IAuditService? _auditService;
    private AuthenticatedUserSession? _currentUser;
    private bool _showResultDialogs;
    private bool _isLocalFallbackMode;

    public HostSettingsForm()
        : this(true)
    {
    }

    public HostSettingsForm(bool showResultDialogs)
    {
        _showResultDialogs = showResultDialogs;
        InitializeComponent();
        InitializeUiText();
    }

    public void Bind(IHostSettingsStore settingsStore, IAuditService auditService, AuthenticatedUserSession currentUser, HostSettingsDocument document, bool showResultDialogs = true)
    {
        _settingsStore = settingsStore;
        _auditService = auditService;
        _currentUser = currentUser;
        _showResultDialogs = showResultDialogs;
        chkEnableDatabase.Checked = document.EnableDatabase;
        txtConnectionString.Text = document.RemoteDesktopDbConnectionString;
        txtServerUrl.Text = document.ServerUrl;
        txtCentralServerUrl.Text = document.CentralServerUrl ?? string.Empty;
        txtConsoleName.Text = document.ConsoleName;
        txtAdminUserName.Text = document.AdminUserName;
        txtAdminPassword.Text = document.AdminPassword;
        txtSharedAccessKey.Text = document.SharedAccessKey;
        chkRequireHttpsRedirect.Checked = document.RequireHttpsRedirect;
        numHeartbeatTimeout.Value = Math.Clamp(document.AgentHeartbeatTimeoutSeconds, (int)numHeartbeatTimeout.Minimum, (int)numHeartbeatTimeout.Maximum);
        _isLocalFallbackMode = document.IsLocalFallbackMode;
        lblStatus.Text = document.SettingsStatusMessage
            ?? HostUiText.Bi("編輯設定並儲存後會更新 appsettings.json；儲存後需要重新啟動。", "Edit settings and save to update appsettings.json. A restart is required after saving.");
        UpdateDatabaseInputState();
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        if (_settingsStore is null)
        {
            lblStatus.Text = HostUiText.Bi("設定儲存服務尚未初始化。", "Settings store is not initialized.");
            return;
        }

        btnSave.Enabled = false;
        btnCancel.Enabled = false;
        lblStatus.Text = HostUiText.Bi("正在儲存設定...", "Saving settings...");

        try
        {
            var document = new HostSettingsDocument
            {
                EnableDatabase = chkEnableDatabase.Checked,
                RemoteDesktopDbConnectionString = txtConnectionString.Text.Trim(),
                ServerUrl = txtServerUrl.Text.Trim(),
                CentralServerUrl = string.IsNullOrWhiteSpace(txtCentralServerUrl.Text) ? null : txtCentralServerUrl.Text.Trim(),
                ConsoleName = txtConsoleName.Text.Trim(),
                AdminUserName = txtAdminUserName.Text.Trim(),
                AdminPassword = txtAdminPassword.Text,
                SharedAccessKey = txtSharedAccessKey.Text,
                RequireHttpsRedirect = chkRequireHttpsRedirect.Checked,
                AgentHeartbeatTimeoutSeconds = (int)numHeartbeatTimeout.Value
            };

            await _settingsStore.SaveAsync(document, CancellationToken.None);
            if (_auditService is not null && _currentUser is not null)
            {
                await _auditService.WriteAsync(
                    "host-settings-save",
                    _currentUser,
                    "host-settings",
                    document.ConsoleName,
                    true,
                    _isLocalFallbackMode
                        ? "中央 Server 離線時已改為本機儲存 Host 設定。 / Host settings were saved locally because the central server was unavailable."
                        : "Host 設定已儲存。 / Host settings were saved.",
                    CancellationToken.None);
            }

            lblStatus.Text = _isLocalFallbackMode
                ? HostUiText.Bi("中央 Server 目前無法連線，設定已先儲存到本機 appsettings.json；請重新啟動 Host 套用。", "Central server is currently unavailable. Settings were saved to the local appsettings.json. Restart Host to apply them.")
                : HostUiText.Bi("設定已儲存，請重新啟動 Host 套用新設定。", "Settings saved successfully. Restart Host to apply the new configuration.");
            if (_showResultDialogs)
            {
                MessageBox.Show(
                    _isLocalFallbackMode
                        ? HostUiText.Bi("中央 Server 目前無法連線，設定已先儲存到本機 appsettings.json；請重新啟動 Host 套用。", "Central server is currently unavailable. Settings were saved to the local appsettings.json. Restart Host to apply them.")
                        : HostUiText.Bi("Host 設定已儲存，請重新啟動 Host 套用新設定。", "Host settings were saved successfully. Restart Host to apply the new configuration."),
                    HostUiText.Window("Host 設定", "Host Settings"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            if (Modal)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception exception)
        {
            if (_auditService is not null && _currentUser is not null)
            {
                await _auditService.WriteAsync(
                    "host-settings-save",
                    _currentUser,
                    "host-settings",
                    txtConsoleName.Text.Trim(),
                    false,
                    $"Host 設定儲存失敗：{exception.Message} / Host settings save failed: {exception.Message}",
                    CancellationToken.None);
            }

            lblStatus.Text = HostUiText.Bi($"儲存失敗：{exception.Message}", $"Save failed: {exception.Message}");
            if (_showResultDialogs)
            {
                MessageBox.Show(HostUiText.Bi($"Host 設定儲存失敗：{exception.Message}", $"Failed to save Host settings: {exception.Message}"), HostUiText.Window("Host 設定", "Host Settings"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            btnSave.Enabled = true;
            btnCancel.Enabled = true;
        }
    }

    private void chkEnableDatabase_CheckedChanged(object sender, EventArgs e)
    {
        UpdateDatabaseInputState();
    }

    private void UpdateDatabaseInputState()
    {
        txtConnectionString.Enabled = chkEnableDatabase.Checked;
    }

    private void InitializeUiText()
    {
        Text = HostUiText.Window("Host 設定", "Host Settings");
        lblTitle.Text = HostUiText.Bi("Host 設定", "Host Settings");
        HostUiText.ApplyButton(btnSave, "儲存", "Save");
        HostUiText.ApplyButton(btnCancel, "取消", "Cancel");
        chkEnableDatabase.Text = HostUiText.Bi("啟用 MSSQL 以保存裝置與在線資料", "Enable MSSQL persistence for device and presence data");
        chkRequireHttpsRedirect.Text = HostUiText.Bi("啟用", "Enabled");
    }
}
