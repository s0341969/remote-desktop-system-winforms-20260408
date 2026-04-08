using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services.Auditing;
using RemoteDesktop.Host.Services.Users;

namespace RemoteDesktop.Host.Forms;

public partial class LoginForm : Form
{
    private AuthenticationService? _authenticationService;
    private AuditService? _auditService;
    private ControlServerOptions? _options;

    public LoginForm()
    {
        InitializeComponent();
        InitializeUiText();
    }

    public AuthenticatedUserSession? AuthenticatedUser { get; private set; }

    public void Bind(AuthenticationService authenticationService, AuditService auditService, ControlServerOptions options)
    {
        _authenticationService = authenticationService;
        _auditService = auditService;
        _options = options;
        lblConsoleName.Text = options.ConsoleName;
        txtUserName.Text = options.AdminUserName;
    }

    private async void btnLogin_Click(object sender, EventArgs e)
    {
        if (_authenticationService is null || _options is null)
        {
            lblError.Text = HostUiText.Bi("登入表單初始化失敗。", "Login form is not initialized correctly.");
            return;
        }

        var userName = txtUserName.Text.Trim();
        var password = txtPassword.Text;

        btnLogin.Enabled = false;
        btnCancel.Enabled = false;
        lblError.Text = string.Empty;

        try
        {
            var authenticatedUser = await _authenticationService.AuthenticateAsync(userName, password, CancellationToken.None);
            if (authenticatedUser is not null)
            {
                if (_auditService is not null)
                {
                    await _auditService.WriteAsync(
                        "user-sign-in",
                        authenticatedUser,
                        "console",
                        _options.ConsoleName,
                        true,
                        "使用者登入成功。 / User signed in successfully.",
                        CancellationToken.None);
                }

                AuthenticatedUser = authenticatedUser;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            if (_auditService is not null)
            {
                await _auditService.WriteAsync(
                    "user-sign-in",
                    userName,
                    userName,
                    "console",
                    _options.ConsoleName,
                    false,
                    "帳號或密碼錯誤。 / Invalid user name or password.",
                    CancellationToken.None);
            }

            lblError.Text = HostUiText.Bi("帳號或密碼錯誤。", "Invalid user name or password.");
            txtPassword.SelectAll();
            txtPassword.Focus();
        }
        catch (Exception exception)
        {
            if (_auditService is not null)
            {
                await _auditService.WriteAsync(
                    "user-sign-in",
                    userName,
                    userName,
                    "console",
                    _options.ConsoleName,
                    false,
                    $"登入失敗：{exception.Message} / Sign-in failed: {exception.Message}",
                    CancellationToken.None);
            }

            lblError.Text = HostUiText.Bi($"登入失敗：{exception.Message}", $"Sign-in failed: {exception.Message}");
        }
        finally
        {
            btnLogin.Enabled = true;
            btnCancel.Enabled = true;
        }
    }

    private void InitializeUiText()
    {
        Text = HostUiText.Window("主控台登入", "Console Sign-in");
        lblTitle.Text = HostUiText.Bi("登入主控台", "Sign in to Console");
        lblConsoleCaption.Text = HostUiText.Bi("主控台", "Console");
        lblUserName.Text = HostUiText.Bi("帳號", "User name");
        lblPassword.Text = HostUiText.Bi("密碼", "Password");
        HostUiText.ApplyButton(btnLogin, "登入", "Sign in");
        HostUiText.ApplyButton(btnCancel, "取消", "Cancel");
    }
}
