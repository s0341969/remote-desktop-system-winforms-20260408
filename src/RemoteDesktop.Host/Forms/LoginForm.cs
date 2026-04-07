using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public partial class LoginForm : Form
{
    private CredentialValidator? _credentialValidator;
    private ControlServerOptions? _options;

    public LoginForm()
    {
        InitializeComponent();
    }

    public string AuthenticatedUserName { get; private set; } = string.Empty;

    public void Bind(CredentialValidator credentialValidator, ControlServerOptions options)
    {
        _credentialValidator = credentialValidator;
        _options = options;
        lblConsoleName.Text = options.ConsoleName;
        txtUserName.Text = options.AdminUserName;
    }

    private void btnLogin_Click(object sender, EventArgs e)
    {
        if (_credentialValidator is null || _options is null)
        {
            lblError.Text = "登入服務尚未初始化。";
            return;
        }

        var userName = txtUserName.Text.Trim();
        var password = txtPassword.Text;

        if (_credentialValidator.Validate(userName, password))
        {
            AuthenticatedUserName = userName;
            lblError.Text = string.Empty;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        lblError.Text = "帳號或密碼錯誤。";
        txtPassword.SelectAll();
        txtPassword.Focus();
    }
}
