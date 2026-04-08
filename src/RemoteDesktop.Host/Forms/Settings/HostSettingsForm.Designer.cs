#nullable disable
namespace RemoteDesktop.Host.Forms.Settings;

partial class HostSettingsForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutRoot;
    private Label lblTitle;
    private TableLayoutPanel layoutFields;
    private CheckBox chkEnableDatabase;
    private TextBox txtConnectionString;
    private TextBox txtServerUrl;
    private TextBox txtConsoleName;
    private TextBox txtAdminUserName;
    private TextBox txtAdminPassword;
    private TextBox txtSharedAccessKey;
    private CheckBox chkRequireHttpsRedirect;
    private NumericUpDown numHeartbeatTimeout;
    private FlowLayoutPanel panelButtons;
    private Button btnSave;
    private Button btnCancel;
    private Label lblStatus;

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
        layoutRoot = new TableLayoutPanel();
        lblTitle = new Label();
        layoutFields = new TableLayoutPanel();
        chkEnableDatabase = new CheckBox();
        txtConnectionString = new TextBox();
        txtServerUrl = new TextBox();
        txtConsoleName = new TextBox();
        txtAdminUserName = new TextBox();
        txtAdminPassword = new TextBox();
        txtSharedAccessKey = new TextBox();
        chkRequireHttpsRedirect = new CheckBox();
        numHeartbeatTimeout = new NumericUpDown();
        panelButtons = new FlowLayoutPanel();
        btnSave = new Button();
        btnCancel = new Button();
        lblStatus = new Label();
        layoutRoot.SuspendLayout();
        layoutFields.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numHeartbeatTimeout).BeginInit();
        panelButtons.SuspendLayout();
        SuspendLayout();
        layoutRoot.ColumnCount = 1;
        layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutRoot.Controls.Add(lblTitle, 0, 0);
        layoutRoot.Controls.Add(layoutFields, 0, 1);
        layoutRoot.Controls.Add(lblStatus, 0, 2);
        layoutRoot.Controls.Add(panelButtons, 0, 3);
        layoutRoot.Dock = DockStyle.Fill;
        layoutRoot.Location = new Point(0, 0);
        layoutRoot.Name = "layoutRoot";
        layoutRoot.Padding = new Padding(16);
        layoutRoot.RowCount = 4;
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        layoutRoot.Size = new Size(740, 530);
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft JhengHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.Text = "Host 設定";
        layoutFields.ColumnCount = 2;
        layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
        layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "啟用資料庫", Anchor = AnchorStyles.Left }, 0, 0);
        layoutFields.Controls.Add(chkEnableDatabase, 1, 0);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "MSSQL 連線字串", Anchor = AnchorStyles.Left }, 0, 1);
        layoutFields.Controls.Add(txtConnectionString, 1, 1);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "Server URL", Anchor = AnchorStyles.Left }, 0, 2);
        layoutFields.Controls.Add(txtServerUrl, 1, 2);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "主控台名稱", Anchor = AnchorStyles.Left }, 0, 3);
        layoutFields.Controls.Add(txtConsoleName, 1, 3);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "管理帳號", Anchor = AnchorStyles.Left }, 0, 4);
        layoutFields.Controls.Add(txtAdminUserName, 1, 4);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "管理密碼", Anchor = AnchorStyles.Left }, 0, 5);
        layoutFields.Controls.Add(txtAdminPassword, 1, 5);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "共享金鑰", Anchor = AnchorStyles.Left }, 0, 6);
        layoutFields.Controls.Add(txtSharedAccessKey, 1, 6);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "心跳逾時秒數", Anchor = AnchorStyles.Left }, 0, 7);
        layoutFields.Controls.Add(numHeartbeatTimeout, 1, 7);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "HTTPS 轉址", Anchor = AnchorStyles.Left }, 0, 8);
        layoutFields.Controls.Add(chkRequireHttpsRedirect, 1, 8);
        layoutFields.Dock = DockStyle.Fill;
        layoutFields.Location = new Point(19, 63);
        layoutFields.Name = "layoutFields";
        layoutFields.RowCount = 9;
        for (var i = 0; i < 9; i++)
        {
            layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        }
        chkEnableDatabase.AutoSize = true;
        chkEnableDatabase.Name = "chkEnableDatabase";
        chkEnableDatabase.Text = "使用 MSSQL 儲存裝置與連線紀錄";
        chkEnableDatabase.CheckedChanged += chkEnableDatabase_CheckedChanged;
        txtConnectionString.Dock = DockStyle.Fill;
        txtConnectionString.Name = "txtConnectionString";
        txtServerUrl.Dock = DockStyle.Fill;
        txtServerUrl.Name = "txtServerUrl";
        txtConsoleName.Dock = DockStyle.Fill;
        txtConsoleName.Name = "txtConsoleName";
        txtAdminUserName.Dock = DockStyle.Fill;
        txtAdminUserName.Name = "txtAdminUserName";
        txtAdminPassword.Dock = DockStyle.Fill;
        txtAdminPassword.Name = "txtAdminPassword";
        txtAdminPassword.PasswordChar = '●';
        txtSharedAccessKey.Dock = DockStyle.Fill;
        txtSharedAccessKey.Name = "txtSharedAccessKey";
        txtSharedAccessKey.PasswordChar = '●';
        numHeartbeatTimeout.Maximum = 300;
        numHeartbeatTimeout.Minimum = 15;
        numHeartbeatTimeout.Name = "numHeartbeatTimeout";
        numHeartbeatTimeout.Size = new Size(130, 23);
        chkRequireHttpsRedirect.AutoSize = true;
        chkRequireHttpsRedirect.Name = "chkRequireHttpsRedirect";
        chkRequireHttpsRedirect.Text = "啟用";
        lblStatus.AutoSize = true;
        lblStatus.ForeColor = Color.DimGray;
        lblStatus.Name = "lblStatus";
        panelButtons.Controls.Add(btnSave);
        panelButtons.Controls.Add(btnCancel);
        panelButtons.Dock = DockStyle.Fill;
        panelButtons.FlowDirection = FlowDirection.RightToLeft;
        btnSave.Name = "btnSave";
        btnSave.Size = new Size(88, 32);
        btnSave.Text = "儲存";
        btnSave.UseVisualStyleBackColor = true;
        btnSave.Click += btnSave_Click;
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(88, 32);
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = true;
        AcceptButton = btnSave;
        CancelButton = btnCancel;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(740, 530);
        Controls.Add(layoutRoot);
        MinimumSize = new Size(740, 530);
        Name = "HostSettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Host 設定";
        layoutRoot.ResumeLayout(false);
        layoutRoot.PerformLayout();
        layoutFields.ResumeLayout(false);
        layoutFields.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numHeartbeatTimeout).EndInit();
        panelButtons.ResumeLayout(false);
        ResumeLayout(false);
    }
}
#nullable restore
