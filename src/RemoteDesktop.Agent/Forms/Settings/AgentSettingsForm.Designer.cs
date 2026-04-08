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
        layoutRoot.ColumnCount = 1;
        layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutRoot.Controls.Add(lblTitle, 0, 0);
        layoutRoot.Controls.Add(layoutFields, 0, 1);
        layoutRoot.Controls.Add(lblStatus, 0, 2);
        layoutRoot.Controls.Add(panelButtons, 0, 3);
        layoutRoot.Dock = DockStyle.Fill;
        layoutRoot.Padding = new Padding(16);
        layoutRoot.RowCount = 4;
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        layoutRoot.Size = new Size(640, 440);
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft JhengHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.Text = "Agent 設定";
        layoutFields.ColumnCount = 2;
        layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
        layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "控制端 URL", Anchor = AnchorStyles.Left }, 0, 0);
        layoutFields.Controls.Add(txtServerUrl, 1, 0);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "DeviceId", Anchor = AnchorStyles.Left }, 0, 1);
        layoutFields.Controls.Add(txtDeviceId, 1, 1);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "裝置名稱", Anchor = AnchorStyles.Left }, 0, 2);
        layoutFields.Controls.Add(txtDeviceName, 1, 2);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "共享金鑰", Anchor = AnchorStyles.Left }, 0, 3);
        layoutFields.Controls.Add(txtSharedAccessKey, 1, 3);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "畫面 FPS", Anchor = AnchorStyles.Left }, 0, 4);
        layoutFields.Controls.Add(numCaptureFps, 1, 4);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "JPEG 品質", Anchor = AnchorStyles.Left }, 0, 5);
        layoutFields.Controls.Add(numJpegQuality, 1, 5);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "最大寬度", Anchor = AnchorStyles.Left }, 0, 6);
        layoutFields.Controls.Add(numMaxFrameWidth, 1, 6);
        layoutFields.Controls.Add(new Label { AutoSize = true, Text = "重連秒數", Anchor = AnchorStyles.Left }, 0, 7);
        layoutFields.Controls.Add(numReconnectDelay, 1, 7);
        layoutFields.Dock = DockStyle.Fill;
        for (var i = 0; i < 8; i++)
        {
            layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        }
        txtServerUrl.Dock = DockStyle.Fill;
        txtServerUrl.Name = "txtServerUrl";
        txtDeviceId.Dock = DockStyle.Fill;
        txtDeviceId.Name = "txtDeviceId";
        txtDeviceName.Dock = DockStyle.Fill;
        txtDeviceName.Name = "txtDeviceName";
        txtSharedAccessKey.Dock = DockStyle.Fill;
        txtSharedAccessKey.Name = "txtSharedAccessKey";
        txtSharedAccessKey.PasswordChar = '●';
        numCaptureFps.Minimum = 1;
        numCaptureFps.Maximum = 24;
        numCaptureFps.Name = "numCaptureFps";
        numJpegQuality.Minimum = 30;
        numJpegQuality.Maximum = 90;
        numJpegQuality.Name = "numJpegQuality";
        numMaxFrameWidth.Minimum = 640;
        numMaxFrameWidth.Maximum = 3840;
        numMaxFrameWidth.Increment = 80;
        numMaxFrameWidth.Name = "numMaxFrameWidth";
        numReconnectDelay.Minimum = 1;
        numReconnectDelay.Maximum = 60;
        numReconnectDelay.Name = "numReconnectDelay";
        lblStatus.AutoSize = true;
        lblStatus.ForeColor = Color.DimGray;
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
        ClientSize = new Size(640, 440);
        Controls.Add(layoutRoot);
        MinimumSize = new Size(640, 440);
        Name = "AgentSettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Agent 設定";
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
