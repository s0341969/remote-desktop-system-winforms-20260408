#nullable disable
namespace RemoteDesktop.Host.Forms;

partial class LoginForm
{
    private System.ComponentModel.IContainer components = null;
    private Label lblTitle;
    private Label lblConsoleCaption;
    private Label lblConsoleName;
    private Label lblUserName;
    private Label lblPassword;
    private TextBox txtUserName;
    private TextBox txtPassword;
    private Button btnLogin;
    private Button btnCancel;
    private Label lblError;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components is not null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblTitle = new Label();
        lblConsoleCaption = new Label();
        lblConsoleName = new Label();
        lblUserName = new Label();
        lblPassword = new Label();
        txtUserName = new TextBox();
        txtPassword = new TextBox();
        btnLogin = new Button();
        btnCancel = new Button();
        lblError = new Label();
        SuspendLayout();
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft JhengHei UI", 16F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.Location = new Point(28, 24);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(210, 28);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "遠端桌面主控台登入";
        lblConsoleCaption.AutoSize = true;
        lblConsoleCaption.Location = new Point(31, 70);
        lblConsoleCaption.Name = "lblConsoleCaption";
        lblConsoleCaption.Size = new Size(67, 15);
        lblConsoleCaption.TabIndex = 1;
        lblConsoleCaption.Text = "主控台名稱";
        lblConsoleName.AutoSize = true;
        lblConsoleName.Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
        lblConsoleName.Location = new Point(31, 90);
        lblConsoleName.Name = "lblConsoleName";
        lblConsoleName.Size = new Size(131, 18);
        lblConsoleName.TabIndex = 2;
        lblConsoleName.Text = "RemoteDesk Control";
        lblUserName.AutoSize = true;
        lblUserName.Location = new Point(31, 131);
        lblUserName.Name = "lblUserName";
        lblUserName.Size = new Size(31, 15);
        lblUserName.TabIndex = 3;
        lblUserName.Text = "帳號";
        lblPassword.AutoSize = true;
        lblPassword.Location = new Point(31, 191);
        lblPassword.Name = "lblPassword";
        lblPassword.Size = new Size(31, 15);
        lblPassword.TabIndex = 4;
        lblPassword.Text = "密碼";
        txtUserName.Location = new Point(31, 150);
        txtUserName.Name = "txtUserName";
        txtUserName.Size = new Size(321, 23);
        txtUserName.TabIndex = 0;
        txtPassword.Location = new Point(31, 210);
        txtPassword.Name = "txtPassword";
        txtPassword.PasswordChar = '●';
        txtPassword.Size = new Size(321, 23);
        txtPassword.TabIndex = 1;
        btnLogin.Location = new Point(196, 284);
        btnLogin.Name = "btnLogin";
        btnLogin.Size = new Size(75, 32);
        btnLogin.TabIndex = 2;
        btnLogin.Text = "登入";
        btnLogin.UseVisualStyleBackColor = true;
        btnLogin.Click += btnLogin_Click;
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(277, 284);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(75, 32);
        btnCancel.TabIndex = 3;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = true;
        lblError.ForeColor = Color.Firebrick;
        lblError.Location = new Point(31, 246);
        lblError.Name = "lblError";
        lblError.Size = new Size(321, 26);
        lblError.TabIndex = 9;
        AcceptButton = btnLogin;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnCancel;
        ClientSize = new Size(388, 343);
        Controls.Add(lblError);
        Controls.Add(btnCancel);
        Controls.Add(btnLogin);
        Controls.Add(txtPassword);
        Controls.Add(txtUserName);
        Controls.Add(lblPassword);
        Controls.Add(lblUserName);
        Controls.Add(lblConsoleName);
        Controls.Add(lblConsoleCaption);
        Controls.Add(lblTitle);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "LoginForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RemoteDesktop.Host";
        ResumeLayout(false);
        PerformLayout();
    }
}
#nullable restore
