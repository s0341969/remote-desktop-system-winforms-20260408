#nullable disable
namespace RemoteDesktop.Agent.Forms;

partial class AgentMainForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutRoot;
    private Panel panelHeader;
    private Label lblTitle;
    private Button btnSettings;
    private TableLayoutPanel layoutSummary;
    private Label lblServerUrlCaption;
    private Label lblServerUrlValue;
    private Label lblDeviceIdCaption;
    private Label lblDeviceIdValue;
    private Label lblDeviceNameCaption;
    private Label lblDeviceNameValue;
    private Label lblStatusCaption;
    private Label lblStatusValue;
    private Label lblLastConnectedCaption;
    private Label lblLastConnectedValue;
    private Label lblLastFrameCaption;
    private Label lblLastFrameValue;
    private Label lblLastErrorCaption;
    private TextBox txtLastError;
    private Label lblEventsCaption;
    private ListBox listEvents;

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
        btnSettings = new Button();
        lblTitle = new Label();
        layoutSummary = new TableLayoutPanel();
        lblServerUrlCaption = new Label();
        lblServerUrlValue = new Label();
        lblDeviceIdCaption = new Label();
        lblDeviceIdValue = new Label();
        lblDeviceNameCaption = new Label();
        lblDeviceNameValue = new Label();
        lblStatusCaption = new Label();
        lblStatusValue = new Label();
        lblLastConnectedCaption = new Label();
        lblLastConnectedValue = new Label();
        lblLastFrameCaption = new Label();
        lblLastFrameValue = new Label();
        lblLastErrorCaption = new Label();
        txtLastError = new TextBox();
        lblEventsCaption = new Label();
        listEvents = new ListBox();
        layoutRoot.SuspendLayout();
        panelHeader.SuspendLayout();
        layoutSummary.SuspendLayout();
        SuspendLayout();
        // layoutRoot
        layoutRoot.ColumnCount = 1;
        layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutRoot.Controls.Add(panelHeader, 0, 0);
        layoutRoot.Controls.Add(layoutSummary, 0, 1);
        layoutRoot.Controls.Add(lblLastErrorCaption, 0, 2);
        layoutRoot.Controls.Add(txtLastError, 0, 3);
        layoutRoot.Controls.Add(lblEventsCaption, 0, 4);
        layoutRoot.Controls.Add(listEvents, 0, 5);
        layoutRoot.Dock = DockStyle.Fill;
        layoutRoot.Location = new Point(0, 0);
        layoutRoot.Name = "layoutRoot";
        layoutRoot.RowCount = 6;
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 234F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.Size = new Size(840, 560);
        // panelHeader
        panelHeader.Controls.Add(btnSettings);
        panelHeader.Controls.Add(lblTitle);
        panelHeader.Dock = DockStyle.Fill;
        panelHeader.Name = "panelHeader";
        // btnSettings
        btnSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSettings.Location = new Point(724, 18);
        btnSettings.Name = "btnSettings";
        btnSettings.Size = new Size(100, 46);
        btnSettings.Text = "設定\r\nSettings";
        btnSettings.UseVisualStyleBackColor = true;
        btnSettings.Click += btnSettings_Click;
        // lblTitle
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft JhengHei UI", 16F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.Location = new Point(16, 14);
        lblTitle.Margin = new Padding(16, 14, 3, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(270, 56);
        lblTitle.Text = "遠端桌面 Agent\r\nRemoteDesktop Agent";
        // layoutSummary
        layoutSummary.ColumnCount = 2;
        layoutSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
        layoutSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 74F));
        layoutSummary.Controls.Add(lblServerUrlCaption, 0, 0);
        layoutSummary.Controls.Add(lblServerUrlValue, 1, 0);
        layoutSummary.Controls.Add(lblDeviceIdCaption, 0, 1);
        layoutSummary.Controls.Add(lblDeviceIdValue, 1, 1);
        layoutSummary.Controls.Add(lblDeviceNameCaption, 0, 2);
        layoutSummary.Controls.Add(lblDeviceNameValue, 1, 2);
        layoutSummary.Controls.Add(lblStatusCaption, 0, 3);
        layoutSummary.Controls.Add(lblStatusValue, 1, 3);
        layoutSummary.Controls.Add(lblLastConnectedCaption, 0, 4);
        layoutSummary.Controls.Add(lblLastConnectedValue, 1, 4);
        layoutSummary.Controls.Add(lblLastFrameCaption, 0, 5);
        layoutSummary.Controls.Add(lblLastFrameValue, 1, 5);
        layoutSummary.Dock = DockStyle.Fill;
        layoutSummary.Location = new Point(12, 88);
        layoutSummary.Margin = new Padding(12, 6, 12, 6);
        layoutSummary.Name = "layoutSummary";
        layoutSummary.RowCount = 6;
        for (var i = 0; i < 6; i++)
        {
            layoutSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        }
        layoutSummary.Size = new Size(816, 222);
        // labels
        lblServerUrlCaption.AutoSize = true;
        lblServerUrlCaption.Name = "lblServerUrlCaption";
        lblServerUrlCaption.Text = "Server 位址\r\nServer URL";
        lblServerUrlValue.AutoSize = true;
        lblServerUrlValue.Name = "lblServerUrlValue";
        lblServerUrlValue.Text = "-";
        lblDeviceIdCaption.AutoSize = true;
        lblDeviceIdCaption.Name = "lblDeviceIdCaption";
        lblDeviceIdCaption.Text = "裝置 ID\r\nDevice ID";
        lblDeviceIdValue.AutoSize = true;
        lblDeviceIdValue.Name = "lblDeviceIdValue";
        lblDeviceIdValue.Text = "-";
        lblDeviceNameCaption.AutoSize = true;
        lblDeviceNameCaption.Name = "lblDeviceNameCaption";
        lblDeviceNameCaption.Text = "裝置名稱\r\nDevice name";
        lblDeviceNameValue.AutoSize = true;
        lblDeviceNameValue.Name = "lblDeviceNameValue";
        lblDeviceNameValue.Text = "-";
        lblStatusCaption.AutoSize = true;
        lblStatusCaption.Name = "lblStatusCaption";
        lblStatusCaption.Text = "狀態\r\nStatus";
        lblStatusValue.AutoSize = true;
        lblStatusValue.Name = "lblStatusValue";
        lblStatusValue.Text = "-";
        lblLastConnectedCaption.AutoSize = true;
        lblLastConnectedCaption.Name = "lblLastConnectedCaption";
        lblLastConnectedCaption.Text = "最後連線\r\nLast connected";
        lblLastConnectedValue.AutoSize = true;
        lblLastConnectedValue.Name = "lblLastConnectedValue";
        lblLastConnectedValue.Text = "-";
        lblLastFrameCaption.AutoSize = true;
        lblLastFrameCaption.Name = "lblLastFrameCaption";
        lblLastFrameCaption.Text = "最後送出畫面\r\nLast frame sent";
        lblLastFrameValue.AutoSize = true;
        lblLastFrameValue.Name = "lblLastFrameValue";
        lblLastFrameValue.Text = "-";
        // last error
        lblLastErrorCaption.AutoSize = true;
        lblLastErrorCaption.Margin = new Padding(16, 0, 3, 0);
        lblLastErrorCaption.Name = "lblLastErrorCaption";
        lblLastErrorCaption.Text = "最近錯誤\r\nLast error";
        txtLastError.Dock = DockStyle.Fill;
        txtLastError.Location = new Point(16, 357);
        txtLastError.Margin = new Padding(16, 3, 16, 3);
        txtLastError.Multiline = true;
        txtLastError.Name = "txtLastError";
        txtLastError.ReadOnly = true;
        // events
        lblEventsCaption.AutoSize = true;
        lblEventsCaption.Margin = new Padding(16, 0, 3, 0);
        lblEventsCaption.Name = "lblEventsCaption";
        lblEventsCaption.Text = "最近事件\r\nRecent events";
        listEvents.Dock = DockStyle.Fill;
        listEvents.FormattingEnabled = true;
        listEvents.ItemHeight = 15;
        listEvents.Location = new Point(16, 465);
        listEvents.Margin = new Padding(16, 3, 16, 16);
        listEvents.Name = "listEvents";
        // AgentMainForm
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(840, 560);
        Controls.Add(layoutRoot);
        MinimumSize = new Size(760, 520);
        Name = "AgentMainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "遠端桌面 Agent / RemoteDesktop Agent";
        layoutRoot.ResumeLayout(false);
        layoutRoot.PerformLayout();
        panelHeader.ResumeLayout(false);
        panelHeader.PerformLayout();
        layoutSummary.ResumeLayout(false);
        layoutSummary.PerformLayout();
        ResumeLayout(false);
    }
}
#nullable restore
