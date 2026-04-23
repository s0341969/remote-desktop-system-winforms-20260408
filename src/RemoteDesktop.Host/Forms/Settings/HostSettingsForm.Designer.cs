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
    private TextBox txtCentralServerUrl;
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
        txtCentralServerUrl = new TextBox();
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
        // layoutRoot
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
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        layoutRoot.Size = new Size(760, 620);
        // lblTitle
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft JhengHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.Location = new Point(19, 16);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(153, 52);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Host 設定\r\nHost Settings";
        // layoutFields
        layoutFields.ColumnCount = 2;
        layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
        layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "資料庫模式\r\nDatabase mode", Anchor = AnchorStyles.Left }, 0, 0);
        layoutFields.Controls.Add(chkEnableDatabase, 1, 0);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "MSSQL 連線字串\r\nMSSQL connection string", Anchor = AnchorStyles.Left }, 0, 1);
        layoutFields.Controls.Add(txtConnectionString, 1, 1);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "Server URL\r\nServer URL", Anchor = AnchorStyles.Left }, 0, 2);
        layoutFields.Controls.Add(txtServerUrl, 1, 2);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "中央 Server URL\r\nCentral server URL", Anchor = AnchorStyles.Left }, 0, 3);
        layoutFields.Controls.Add(txtCentralServerUrl, 1, 3);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "主控台名稱\r\nConsole name", Anchor = AnchorStyles.Left }, 0, 4);
        layoutFields.Controls.Add(txtConsoleName, 1, 4);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "管理員帳號\r\nAdmin user name", Anchor = AnchorStyles.Left }, 0, 5);
        layoutFields.Controls.Add(txtAdminUserName, 1, 5);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "管理員密碼\r\nAdmin password", Anchor = AnchorStyles.Left }, 0, 6);
        layoutFields.Controls.Add(txtAdminPassword, 1, 6);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "共享存取金鑰\r\nShared access key", Anchor = AnchorStyles.Left }, 0, 7);
        layoutFields.Controls.Add(txtSharedAccessKey, 1, 7);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "心跳逾時秒數\r\nHeartbeat timeout seconds", Anchor = AnchorStyles.Left }, 0, 8);
        layoutFields.Controls.Add(numHeartbeatTimeout, 1, 8);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "HTTPS 重新導向\r\nHTTPS redirect", Anchor = AnchorStyles.Left }, 0, 9);
        layoutFields.Controls.Add(chkRequireHttpsRedirect, 1, 9);
        layoutFields.Dock = DockStyle.Fill;
        layoutFields.Location = new Point(19, 79);
        layoutFields.Name = "layoutFields";
        layoutFields.RowCount = 10;
        for (var i = 0; i < 10; i++)
        {
            layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        }
        layoutFields.Size = new Size(722, 421);
        // chkEnableDatabase
        chkEnableDatabase.AutoSize = true;
        chkEnableDatabase.Name = "chkEnableDatabase";
        chkEnableDatabase.Text = "啟用 MSSQL 以保存裝置與在線資料\r\nEnable MSSQL persistence for device and presence data";
        chkEnableDatabase.CheckedChanged += chkEnableDatabase_CheckedChanged;
        // txtConnectionString
        txtConnectionString.Dock = DockStyle.Fill;
        txtConnectionString.Name = "txtConnectionString";
        // txtServerUrl
        txtServerUrl.Dock = DockStyle.Fill;
        txtServerUrl.Name = "txtServerUrl";
        // txtCentralServerUrl
        txtCentralServerUrl.Dock = DockStyle.Fill;
        txtCentralServerUrl.Name = "txtCentralServerUrl";
        // txtConsoleName
        txtConsoleName.Dock = DockStyle.Fill;
        txtConsoleName.Name = "txtConsoleName";
        // txtAdminUserName
        txtAdminUserName.Dock = DockStyle.Fill;
        txtAdminUserName.Name = "txtAdminUserName";
        // txtAdminPassword
        txtAdminPassword.Dock = DockStyle.Fill;
        txtAdminPassword.Name = "txtAdminPassword";
        txtAdminPassword.PasswordChar = '*';
        // txtSharedAccessKey
        txtSharedAccessKey.Dock = DockStyle.Fill;
        txtSharedAccessKey.Name = "txtSharedAccessKey";
        txtSharedAccessKey.PasswordChar = '*';
        // numHeartbeatTimeout
        numHeartbeatTimeout.Maximum = 600;
        numHeartbeatTimeout.Minimum = 60;
        numHeartbeatTimeout.Name = "numHeartbeatTimeout";
        numHeartbeatTimeout.Size = new Size(130, 23);
        numHeartbeatTimeout.Value = 180;
        // chkRequireHttpsRedirect
        chkRequireHttpsRedirect.AutoSize = true;
        chkRequireHttpsRedirect.Name = "chkRequireHttpsRedirect";
        chkRequireHttpsRedirect.Text = "啟用\r\nEnabled";
        // panelButtons
        panelButtons.Controls.Add(btnSave);
        panelButtons.Controls.Add(btnCancel);
        panelButtons.Dock = DockStyle.Fill;
        panelButtons.FlowDirection = FlowDirection.RightToLeft;
        panelButtons.Location = new Point(19, 547);
        panelButtons.Name = "panelButtons";
        panelButtons.Size = new Size(722, 54);
        // btnSave
        btnSave.Name = "btnSave";
        btnSave.Size = new Size(96, 44);
        btnSave.Text = "儲存\r\nSave";
        btnSave.UseVisualStyleBackColor = true;
        btnSave.Click += btnSave_Click;
        // btnCancel
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(96, 44);
        btnCancel.Text = "取消\r\nCancel";
        btnCancel.UseVisualStyleBackColor = true;
        // lblStatus
        lblStatus.AutoSize = true;
        lblStatus.ForeColor = Color.DimGray;
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(12, 15);
        lblStatus.TabIndex = 3;
        lblStatus.Text = "-";
        // HostSettingsForm
        AcceptButton = btnSave;
        CancelButton = btnCancel;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(760, 620);
        Controls.Add(layoutRoot);
        MinimumSize = new Size(760, 620);
        Name = "HostSettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Host 設定 / Host Settings";
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
