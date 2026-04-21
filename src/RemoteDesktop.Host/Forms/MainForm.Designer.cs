#nullable disable
namespace RemoteDesktop.Host.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutRoot;
    private Panel panelHeader;
    private Label lblTitle;
    private Button btnAudit;
    private Button btnUsers;
    private Button btnSettings;
    private Button btnDeviceDetails;
    private Button btnRevokeDevice;
    private Button btnApproveDevice;
    private Button btnOpenViewer;
    private Button btnRefresh;
    private TableLayoutPanel layoutSummary;
    private Label lblConsoleNameCaption;
    private Label lblConsoleNameValue;
    private Label lblServerUrlCaption;
    private Label lblServerUrlValue;
    private Label lblHealthUrlCaption;
    private Label lblHealthUrlValue;
    private Label lblSignedInUserCaption;
    private Label lblSignedInUserValue;
    private Label lblOnlineCountCaption;
    private Label lblOnlineCountValue;
    private Label lblTotalCountCaption;
    private Label lblTotalCountValue;
    private Label lblLastRefreshCaption;
    private Label lblLastRefreshValue;
    private SplitContainer splitMain;
    private DataGridView gridDevices;
    private DataGridView gridLogs;
    private Label lblDevicesTitle;
    private Label lblLogsTitle;
    private Panel panelStatus;
    private Label lblStatusCaption;
    private Label lblStatusValue;

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
        panelHeader = new Panel();
        btnAudit = new Button();
        btnUsers = new Button();
        btnSettings = new Button();
        btnDeviceDetails = new Button();
        btnRevokeDevice = new Button();
        btnApproveDevice = new Button();
        btnOpenViewer = new Button();
        btnRefresh = new Button();
        lblTitle = new Label();
        layoutSummary = new TableLayoutPanel();
        lblConsoleNameCaption = new Label();
        lblConsoleNameValue = new Label();
        lblServerUrlCaption = new Label();
        lblServerUrlValue = new Label();
        lblHealthUrlCaption = new Label();
        lblHealthUrlValue = new Label();
        lblSignedInUserCaption = new Label();
        lblSignedInUserValue = new Label();
        lblOnlineCountCaption = new Label();
        lblOnlineCountValue = new Label();
        lblTotalCountCaption = new Label();
        lblTotalCountValue = new Label();
        lblLastRefreshCaption = new Label();
        lblLastRefreshValue = new Label();
        splitMain = new SplitContainer();
        gridDevices = new DataGridView();
        gridLogs = new DataGridView();
        lblDevicesTitle = new Label();
        lblLogsTitle = new Label();
        panelStatus = new Panel();
        lblStatusCaption = new Label();
        lblStatusValue = new Label();
        layoutRoot.SuspendLayout();
        panelHeader.SuspendLayout();
        layoutSummary.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
        splitMain.Panel1.SuspendLayout();
        splitMain.Panel2.SuspendLayout();
        splitMain.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridDevices).BeginInit();
        ((System.ComponentModel.ISupportInitialize)gridLogs).BeginInit();
        panelStatus.SuspendLayout();
        SuspendLayout();
        // layoutRoot
        layoutRoot.ColumnCount = 1;
        layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutRoot.Controls.Add(panelHeader, 0, 0);
        layoutRoot.Controls.Add(layoutSummary, 0, 1);
        layoutRoot.Controls.Add(splitMain, 0, 2);
        layoutRoot.Controls.Add(panelStatus, 0, 3);
        layoutRoot.Dock = DockStyle.Fill;
        layoutRoot.Location = new Point(0, 0);
        layoutRoot.Name = "layoutRoot";
        layoutRoot.RowCount = 4;
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 126F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 176F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layoutRoot.Size = new Size(1500, 861);
        // panelHeader
        panelHeader.Controls.Add(btnAudit);
        panelHeader.Controls.Add(btnUsers);
        panelHeader.Controls.Add(btnSettings);
        panelHeader.Controls.Add(btnDeviceDetails);
        panelHeader.Controls.Add(btnRevokeDevice);
        panelHeader.Controls.Add(btnApproveDevice);
        panelHeader.Controls.Add(btnOpenViewer);
        panelHeader.Controls.Add(btnRefresh);
        panelHeader.Controls.Add(lblTitle);
        panelHeader.Dock = DockStyle.Fill;
        panelHeader.Location = new Point(3, 3);
        panelHeader.Name = "panelHeader";
        panelHeader.Size = new Size(1494, 120);
        // btnAudit
        btnAudit.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAudit.Location = new Point(651, 20);
        btnAudit.Name = "btnAudit";
        btnAudit.Size = new Size(96, 46);
        btnAudit.TabIndex = 0;
        btnAudit.Text = "Audit";
        btnAudit.UseVisualStyleBackColor = true;
        btnAudit.Click += btnAudit_Click;
        // btnUsers
        btnUsers.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnUsers.Location = new Point(753, 20);
        btnUsers.Name = "btnUsers";
        btnUsers.Size = new Size(96, 46);
        btnUsers.TabIndex = 1;
        btnUsers.Text = "Users";
        btnUsers.UseVisualStyleBackColor = true;
        btnUsers.Click += btnUsers_Click;
        // btnSettings
        btnSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSettings.Location = new Point(855, 20);
        btnSettings.Name = "btnSettings";
        btnSettings.Size = new Size(96, 46);
        btnSettings.TabIndex = 2;
        btnSettings.Text = "Settings";
        btnSettings.UseVisualStyleBackColor = true;
        btnSettings.Click += btnSettings_Click;
        // btnDeviceDetails
        btnDeviceDetails.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnDeviceDetails.Location = new Point(945, 20);
        btnDeviceDetails.Name = "btnDeviceDetails";
        btnDeviceDetails.Size = new Size(116, 46);
        btnDeviceDetails.TabIndex = 3;
        btnDeviceDetails.Text = "Device Details";
        btnDeviceDetails.UseVisualStyleBackColor = true;
        btnDeviceDetails.Click += btnDeviceDetails_Click;
        // btnRevokeDevice
        btnRevokeDevice.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRevokeDevice.Location = new Point(1061, 20);
        btnRevokeDevice.Name = "btnRevokeDevice";
        btnRevokeDevice.Size = new Size(96, 46);
        btnRevokeDevice.TabIndex = 4;
        btnRevokeDevice.Text = "Revoke";
        btnRevokeDevice.UseVisualStyleBackColor = true;
        btnRevokeDevice.Click += btnRevokeDevice_Click;
        // btnApproveDevice
        btnApproveDevice.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnApproveDevice.Location = new Point(1157, 20);
        btnApproveDevice.Name = "btnApproveDevice";
        btnApproveDevice.Size = new Size(96, 46);
        btnApproveDevice.TabIndex = 5;
        btnApproveDevice.Text = "Approve";
        btnApproveDevice.UseVisualStyleBackColor = true;
        btnApproveDevice.Click += btnApproveDevice_Click;
        // btnOpenViewer
        btnOpenViewer.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnOpenViewer.Enabled = false;
        btnOpenViewer.Location = new Point(1253, 20);
        btnOpenViewer.Name = "btnOpenViewer";
        btnOpenViewer.Size = new Size(110, 46);
        btnOpenViewer.TabIndex = 6;
        btnOpenViewer.Text = "Open Viewer";
        btnOpenViewer.UseVisualStyleBackColor = true;
        btnOpenViewer.Click += btnOpenViewer_Click;
        // btnRefresh
        btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRefresh.Location = new Point(1369, 20);
        btnRefresh.Name = "btnRefresh";
        btnRefresh.Size = new Size(104, 46);
        btnRefresh.TabIndex = 7;
        btnRefresh.Text = "Refresh";
        btnRefresh.UseVisualStyleBackColor = true;
        btnRefresh.Click += btnRefresh_Click;
        // lblTitle
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft JhengHei UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.Location = new Point(16, 12);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(362, 60);
        lblTitle.TabIndex = 7;
        lblTitle.Text = "RemoteDesktop Windows Console";
        // layoutSummary
        layoutSummary.ColumnCount = 4;
        layoutSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
        layoutSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        layoutSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
        layoutSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        layoutSummary.Controls.Add(lblConsoleNameCaption, 0, 0);
        layoutSummary.Controls.Add(lblConsoleNameValue, 1, 0);
        layoutSummary.Controls.Add(lblServerUrlCaption, 2, 0);
        layoutSummary.Controls.Add(lblServerUrlValue, 3, 0);
        layoutSummary.Controls.Add(lblHealthUrlCaption, 0, 1);
        layoutSummary.Controls.Add(lblHealthUrlValue, 1, 1);
        layoutSummary.Controls.Add(lblSignedInUserCaption, 2, 1);
        layoutSummary.Controls.Add(lblSignedInUserValue, 3, 1);
        layoutSummary.Controls.Add(lblOnlineCountCaption, 0, 2);
        layoutSummary.Controls.Add(lblOnlineCountValue, 1, 2);
        layoutSummary.Controls.Add(lblTotalCountCaption, 2, 2);
        layoutSummary.Controls.Add(lblTotalCountValue, 3, 2);
        layoutSummary.Controls.Add(lblLastRefreshCaption, 0, 3);
        layoutSummary.Controls.Add(lblLastRefreshValue, 1, 3);
        layoutSummary.Dock = DockStyle.Fill;
        layoutSummary.Location = new Point(3, 129);
        layoutSummary.Name = "layoutSummary";
        layoutSummary.RowCount = 4;
        layoutSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layoutSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layoutSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layoutSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layoutSummary.Size = new Size(1494, 170);
        // lblConsoleNameCaption
        lblConsoleNameCaption.AutoSize = true;
        lblConsoleNameCaption.Name = "lblConsoleNameCaption";
        lblConsoleNameCaption.Size = new Size(48, 15);
        lblConsoleNameCaption.TabIndex = 0;
        lblConsoleNameCaption.Text = "Console";
        // lblConsoleNameValue
        lblConsoleNameValue.AutoSize = true;
        lblConsoleNameValue.Name = "lblConsoleNameValue";
        lblConsoleNameValue.Size = new Size(12, 15);
        lblConsoleNameValue.TabIndex = 1;
        lblConsoleNameValue.Text = "-";
        // lblServerUrlCaption
        lblServerUrlCaption.AutoSize = true;
        lblServerUrlCaption.Name = "lblServerUrlCaption";
        lblServerUrlCaption.Size = new Size(59, 15);
        lblServerUrlCaption.TabIndex = 2;
        lblServerUrlCaption.Text = "Agent URL";
        // lblServerUrlValue
        lblServerUrlValue.AutoSize = true;
        lblServerUrlValue.Name = "lblServerUrlValue";
        lblServerUrlValue.Size = new Size(12, 15);
        lblServerUrlValue.TabIndex = 3;
        lblServerUrlValue.Text = "-";
        // lblHealthUrlCaption
        lblHealthUrlCaption.AutoSize = true;
        lblHealthUrlCaption.Name = "lblHealthUrlCaption";
        lblHealthUrlCaption.Size = new Size(59, 15);
        lblHealthUrlCaption.TabIndex = 4;
        lblHealthUrlCaption.Text = "Health URL";
        // lblHealthUrlValue
        lblHealthUrlValue.AutoSize = true;
        lblHealthUrlValue.Name = "lblHealthUrlValue";
        lblHealthUrlValue.Size = new Size(12, 15);
        lblHealthUrlValue.TabIndex = 5;
        lblHealthUrlValue.Text = "-";
        // lblSignedInUserCaption
        lblSignedInUserCaption.AutoSize = true;
        lblSignedInUserCaption.Name = "lblSignedInUserCaption";
        lblSignedInUserCaption.Size = new Size(79, 15);
        lblSignedInUserCaption.TabIndex = 6;
        lblSignedInUserCaption.Text = "Signed in user";
        // lblSignedInUserValue
        lblSignedInUserValue.AutoSize = true;
        lblSignedInUserValue.Name = "lblSignedInUserValue";
        lblSignedInUserValue.Size = new Size(12, 15);
        lblSignedInUserValue.TabIndex = 7;
        lblSignedInUserValue.Text = "-";
        // lblOnlineCountCaption
        lblOnlineCountCaption.AutoSize = true;
        lblOnlineCountCaption.Name = "lblOnlineCountCaption";
        lblOnlineCountCaption.Size = new Size(86, 15);
        lblOnlineCountCaption.TabIndex = 8;
        lblOnlineCountCaption.Text = "Online devices";
        // lblOnlineCountValue
        lblOnlineCountValue.AutoSize = true;
        lblOnlineCountValue.Name = "lblOnlineCountValue";
        lblOnlineCountValue.Size = new Size(13, 15);
        lblOnlineCountValue.TabIndex = 9;
        lblOnlineCountValue.Text = "0";
        // lblTotalCountCaption
        lblTotalCountCaption.AutoSize = true;
        lblTotalCountCaption.Name = "lblTotalCountCaption";
        lblTotalCountCaption.Size = new Size(76, 15);
        lblTotalCountCaption.TabIndex = 10;
        lblTotalCountCaption.Text = "Total devices";
        // lblTotalCountValue
        lblTotalCountValue.AutoSize = true;
        lblTotalCountValue.Name = "lblTotalCountValue";
        lblTotalCountValue.Size = new Size(13, 15);
        lblTotalCountValue.TabIndex = 11;
        lblTotalCountValue.Text = "0";
        // lblLastRefreshCaption
        lblLastRefreshCaption.AutoSize = true;
        lblLastRefreshCaption.Name = "lblLastRefreshCaption";
        lblLastRefreshCaption.Size = new Size(68, 15);
        lblLastRefreshCaption.TabIndex = 12;
        lblLastRefreshCaption.Text = "Last refresh";
        // lblLastRefreshValue
        lblLastRefreshValue.AutoSize = true;
        lblLastRefreshValue.Name = "lblLastRefreshValue";
        lblLastRefreshValue.Size = new Size(12, 15);
        lblLastRefreshValue.TabIndex = 13;
        lblLastRefreshValue.Text = "-";
        // splitMain
        splitMain.Dock = DockStyle.Fill;
        splitMain.Location = new Point(3, 263);
        splitMain.Name = "splitMain";
        splitMain.Orientation = Orientation.Horizontal;
        // splitMain.Panel1
        splitMain.Panel1.Controls.Add(gridDevices);
        splitMain.Panel1.Controls.Add(lblDevicesTitle);
        // splitMain.Panel2
        splitMain.Panel2.Controls.Add(gridLogs);
        splitMain.Panel2.Controls.Add(lblLogsTitle);
        splitMain.Size = new Size(1378, 633);
        splitMain.SplitterDistance = 315;
        splitMain.TabIndex = 2;
        // gridDevices
        gridDevices.AllowUserToAddRows = false;
        gridDevices.AllowUserToDeleteRows = false;
        gridDevices.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        gridDevices.AutoGenerateColumns = false;
        gridDevices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridDevices.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StatusText", HeaderText = "Status", FillWeight = 70F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AccessText", HeaderText = "Access", FillWeight = 90F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DeviceId", HeaderText = "Device ID", FillWeight = 110F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DeviceName", HeaderText = "Device name", FillWeight = 120F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "HostName", HeaderText = "Host name", FillWeight = 110F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Resolution", HeaderText = "Resolution", FillWeight = 90F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AgentVersion", HeaderText = "Agent version", FillWeight = 80F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "HardwareSummary", HeaderText = "Hardware", FillWeight = 150F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OsSummary", HeaderText = "OS", FillWeight = 130F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OfficeSummary", HeaderText = "Office", FillWeight = 110F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastUpdateSummary", HeaderText = "Last update", FillWeight = 125F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastSeenAt", HeaderText = "Last seen", FillWeight = 110F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastConnectedAt", HeaderText = "Last connected", FillWeight = 110F });
        gridDevices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastDisconnectedAt", HeaderText = "Last disconnected", FillWeight = 110F });
        gridDevices.Location = new Point(10, 45);
        gridDevices.MultiSelect = false;
        gridDevices.Name = "gridDevices";
        gridDevices.ReadOnly = true;
        gridDevices.SelectionMode = DataGridViewSelectionMode.CellSelect;
        gridDevices.Size = new Size(1359, 258);
        gridDevices.TabIndex = 1;
        gridDevices.CellDoubleClick += gridDevices_CellDoubleClick;
        gridDevices.SelectionChanged += gridDevices_SelectionChanged;
        // gridLogs
        gridLogs.AllowUserToAddRows = false;
        gridLogs.AllowUserToDeleteRows = false;
        gridLogs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        gridLogs.AutoGenerateColumns = false;
        gridLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridLogs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DeviceId", HeaderText = "Device ID", FillWeight = 95F });
        gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DeviceName", HeaderText = "Device name", FillWeight = 110F });
        gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "HostName", HeaderText = "Host name", FillWeight = 110F });
        gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ConnectedAt", HeaderText = "Connected at", FillWeight = 115F });
        gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastSeenAt", HeaderText = "Last seen", FillWeight = 115F });
        gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DisconnectedAt", HeaderText = "Disconnected at", FillWeight = 115F });
        gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DisconnectReason", HeaderText = "Disconnect reason", FillWeight = 120F });
        gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OnlineSeconds", HeaderText = "Online seconds", FillWeight = 80F });
        gridLogs.Location = new Point(10, 37);
        gridLogs.MultiSelect = false;
        gridLogs.Name = "gridLogs";
        gridLogs.ReadOnly = true;
        gridLogs.SelectionMode = DataGridViewSelectionMode.CellSelect;
        gridLogs.Size = new Size(1359, 263);
        gridLogs.TabIndex = 1;
        // lblDevicesTitle
        lblDevicesTitle.AutoSize = true;
        lblDevicesTitle.Font = new Font("Microsoft JhengHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblDevicesTitle.Location = new Point(10, 12);
        lblDevicesTitle.Name = "lblDevicesTitle";
        lblDevicesTitle.Size = new Size(135, 19);
        lblDevicesTitle.TabIndex = 0;
        lblDevicesTitle.Text = "Connected devices";
        // lblLogsTitle
        lblLogsTitle.AutoSize = true;
        lblLogsTitle.Font = new Font("Microsoft JhengHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblLogsTitle.Location = new Point(10, 9);
        lblLogsTitle.Name = "lblLogsTitle";
        lblLogsTitle.Size = new Size(100, 19);
        lblLogsTitle.TabIndex = 0;
        lblLogsTitle.Text = "Presence logs";
        // panelStatus
        panelStatus.Controls.Add(lblStatusValue);
        panelStatus.Controls.Add(lblStatusCaption);
        panelStatus.Dock = DockStyle.Fill;
        panelStatus.Location = new Point(3, 810);
        panelStatus.Name = "panelStatus";
        panelStatus.Size = new Size(1378, 48);
        // lblStatusCaption
        lblStatusCaption.AutoSize = true;
        lblStatusCaption.Location = new Point(16, 4);
        lblStatusCaption.Name = "lblStatusCaption";
        lblStatusCaption.Size = new Size(42, 15);
        lblStatusCaption.TabIndex = 0;
        lblStatusCaption.Text = "Status:";
        // lblStatusValue
        lblStatusValue.AutoSize = true;
        lblStatusValue.Location = new Point(116, 4);
        lblStatusValue.Name = "lblStatusValue";
        lblStatusValue.Size = new Size(38, 15);
        lblStatusValue.TabIndex = 1;
        lblStatusValue.Text = "Ready";
        // MainForm
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1500, 861);
        Controls.Add(layoutRoot);
        MinimumSize = new Size(1180, 760);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RemoteDesktop Windows Console";
        layoutRoot.ResumeLayout(false);
        panelHeader.ResumeLayout(false);
        panelHeader.PerformLayout();
        layoutSummary.ResumeLayout(false);
        layoutSummary.PerformLayout();
        splitMain.Panel1.ResumeLayout(false);
        splitMain.Panel1.PerformLayout();
        splitMain.Panel2.ResumeLayout(false);
        splitMain.Panel2.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
        splitMain.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)gridDevices).EndInit();
        ((System.ComponentModel.ISupportInitialize)gridLogs).EndInit();
        panelStatus.ResumeLayout(false);
        panelStatus.PerformLayout();
        ResumeLayout(false);
    }
}
#nullable restore
