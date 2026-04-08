#nullable disable
namespace RemoteDesktop.Agent.Forms.Settings;

partial class AgentSettingsForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutRoot;
    private Label lblTitle;
    private TableLayoutPanel layoutFields;
    private TextBox txtServerUrl;
    private TextBox txtDeviceId;
    private TextBox txtDeviceName;
    private TextBox txtSharedAccessKey;
    private TextBox txtFileTransferDirectory;
    private NumericUpDown numCaptureFps;
    private NumericUpDown numJpegQuality;
    private NumericUpDown numMaxFrameWidth;
    private NumericUpDown numReconnectDelay;
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
        txtServerUrl = new TextBox();
        txtDeviceId = new TextBox();
        txtDeviceName = new TextBox();
        txtSharedAccessKey = new TextBox();
        txtFileTransferDirectory = new TextBox();
        numCaptureFps = new NumericUpDown();
        numJpegQuality = new NumericUpDown();
        numMaxFrameWidth = new NumericUpDown();
        numReconnectDelay = new NumericUpDown();
        panelButtons = new FlowLayoutPanel();
        btnSave = new Button();
        btnCancel = new Button();
        lblStatus = new Label();
        layoutRoot.SuspendLayout();
        layoutFields.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numCaptureFps).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numJpegQuality).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numMaxFrameWidth).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numReconnectDelay).BeginInit();
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
        layoutRoot.Padding = new Padding(16);
        layoutRoot.RowCount = 4;
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        layoutRoot.Size = new Size(700, 620);
        // lblTitle
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft JhengHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.Location = new Point(19, 16);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(166, 52);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Agent 設定\r\nAgent Settings";
        // layoutFields
        layoutFields.ColumnCount = 2;
        layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "Server URL\r\nServer URL", Anchor = AnchorStyles.Left }, 0, 0);
        layoutFields.Controls.Add(txtServerUrl, 1, 0);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "裝置 ID\r\nDevice ID", Anchor = AnchorStyles.Left }, 0, 1);
        layoutFields.Controls.Add(txtDeviceId, 1, 1);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "裝置名稱\r\nDevice name", Anchor = AnchorStyles.Left }, 0, 2);
        layoutFields.Controls.Add(txtDeviceName, 1, 2);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "共享存取金鑰\r\nShared access key", Anchor = AnchorStyles.Left }, 0, 3);
        layoutFields.Controls.Add(txtSharedAccessKey, 1, 3);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "檔案接收資料夾\r\nFile transfer folder", Anchor = AnchorStyles.Left }, 0, 4);
        layoutFields.Controls.Add(txtFileTransferDirectory, 1, 4);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "擷取 FPS\r\nCapture FPS", Anchor = AnchorStyles.Left }, 0, 5);
        layoutFields.Controls.Add(numCaptureFps, 1, 5);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "JPEG 品質\r\nJPEG quality", Anchor = AnchorStyles.Left }, 0, 6);
        layoutFields.Controls.Add(numJpegQuality, 1, 6);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "最大畫面寬度\r\nMax frame width", Anchor = AnchorStyles.Left }, 0, 7);
        layoutFields.Controls.Add(numMaxFrameWidth, 1, 7);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "重連延遲秒數\r\nReconnect delay", Anchor = AnchorStyles.Left }, 0, 8);
        layoutFields.Controls.Add(numReconnectDelay, 1, 8);
        layoutFields.Dock = DockStyle.Fill;
        layoutFields.Location = new Point(19, 79);
        layoutFields.RowCount = 9;
        for (var i = 0; i < 9; i++)
        {
            layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        }
        layoutFields.Size = new Size(662, 421);
        // txtServerUrl
        txtServerUrl.Dock = DockStyle.Fill;
        txtServerUrl.Name = "txtServerUrl";
        // txtDeviceId
        txtDeviceId.Dock = DockStyle.Fill;
        txtDeviceId.Name = "txtDeviceId";
        // txtDeviceName
        txtDeviceName.Dock = DockStyle.Fill;
        txtDeviceName.Name = "txtDeviceName";
        // txtSharedAccessKey
        txtSharedAccessKey.Dock = DockStyle.Fill;
        txtSharedAccessKey.Name = "txtSharedAccessKey";
        txtSharedAccessKey.PasswordChar = '*';
        // txtFileTransferDirectory
        txtFileTransferDirectory.Dock = DockStyle.Fill;
        txtFileTransferDirectory.Name = "txtFileTransferDirectory";
        // numCaptureFps
        numCaptureFps.Maximum = 24;
        numCaptureFps.Minimum = 1;
        numCaptureFps.Name = "numCaptureFps";
        numCaptureFps.Value = 1;
        // numJpegQuality
        numJpegQuality.Maximum = 90;
        numJpegQuality.Minimum = 30;
        numJpegQuality.Name = "numJpegQuality";
        numJpegQuality.Value = 30;
        // numMaxFrameWidth
        numMaxFrameWidth.Increment = 80;
        numMaxFrameWidth.Maximum = 3840;
        numMaxFrameWidth.Minimum = 640;
        numMaxFrameWidth.Name = "numMaxFrameWidth";
        numMaxFrameWidth.Value = 640;
        // numReconnectDelay
        numReconnectDelay.Maximum = 60;
        numReconnectDelay.Minimum = 1;
        numReconnectDelay.Name = "numReconnectDelay";
        numReconnectDelay.Value = 1;
        // panelButtons
        panelButtons.Controls.Add(btnSave);
        panelButtons.Controls.Add(btnCancel);
        panelButtons.Dock = DockStyle.Fill;
        panelButtons.FlowDirection = FlowDirection.RightToLeft;
        panelButtons.Location = new Point(19, 547);
        panelButtons.Name = "panelButtons";
        panelButtons.Size = new Size(662, 54);
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
        lblStatus.Size = new Size(12, 15);
        lblStatus.Text = "-";
        // AgentSettingsForm
        AcceptButton = btnSave;
        CancelButton = btnCancel;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(700, 620);
        Controls.Add(layoutRoot);
        MinimumSize = new Size(700, 620);
        Name = "AgentSettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Agent 設定 / Agent Settings";
        layoutRoot.ResumeLayout(false);
        layoutRoot.PerformLayout();
        layoutFields.ResumeLayout(false);
        layoutFields.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numCaptureFps).EndInit();
        ((System.ComponentModel.ISupportInitialize)numJpegQuality).EndInit();
        ((System.ComponentModel.ISupportInitialize)numMaxFrameWidth).EndInit();
        ((System.ComponentModel.ISupportInitialize)numReconnectDelay).EndInit();
        panelButtons.ResumeLayout(false);
        ResumeLayout(false);
    }
}
#nullable restore
