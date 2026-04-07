#nullable disable
namespace RemoteDesktop.Agent.Forms;

partial class AgentMainForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutRoot;
    private Label lblTitle;
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
        layoutSummary.SuspendLayout();
        SuspendLayout();
        layoutRoot.ColumnCount = 1;
        layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutRoot.Controls.Add(lblTitle, 0, 0);
        layoutRoot.Controls.Add(layoutSummary, 0, 1);
        layoutRoot.Controls.Add(lblLastErrorCaption, 0, 2);
        layoutRoot.Controls.Add(txtLastError, 0, 3);
        layoutRoot.Controls.Add(lblEventsCaption, 0, 4);
        layoutRoot.Controls.Add(listEvents, 0, 5);
        layoutRoot.Dock = DockStyle.Fill;
        layoutRoot.Location = new Point(0, 0);
        layoutRoot.Name = "layoutRoot";
        layoutRoot.RowCount = 6;
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.Size = new Size(784, 461);
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft JhengHei UI", 16F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.Location = new Point(16, 14);
        lblTitle.Margin = new Padding(16, 14, 3, 0);
        lblTitle.Text = "RemoteDesktop Agent";
        layoutSummary.ColumnCount = 2;
        layoutSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        layoutSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78F));
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
        layoutSummary.Location = new Point(12, 60);
        layoutSummary.Margin = new Padding(12, 6, 12, 6);
        layoutSummary.RowCount = 6;
        layoutSummary.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6F));
        layoutSummary.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6F));
        layoutSummary.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6F));
        layoutSummary.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6F));
        layoutSummary.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6F));
        layoutSummary.RowStyles.Add(new RowStyle(SizeType.Percent, 17F));
        lblServerUrlCaption.AutoSize = true;
        lblServerUrlCaption.Text = "控制端 URL";
        lblServerUrlValue.AutoSize = true;
        lblServerUrlValue.Text = "-";
        lblDeviceIdCaption.AutoSize = true;
        lblDeviceIdCaption.Text = "DeviceId";
        lblDeviceIdValue.AutoSize = true;
        lblDeviceIdValue.Text = "-";
        lblDeviceNameCaption.AutoSize = true;
        lblDeviceNameCaption.Text = "裝置名稱";
        lblDeviceNameValue.AutoSize = true;
        lblDeviceNameValue.Text = "-";
        lblStatusCaption.AutoSize = true;
        lblStatusCaption.Text = "目前狀態";
        lblStatusValue.AutoSize = true;
        lblStatusValue.Text = "-";
        lblLastConnectedCaption.AutoSize = true;
        lblLastConnectedCaption.Text = "最近連線";
        lblLastConnectedValue.AutoSize = true;
        lblLastConnectedValue.Text = "-";
        lblLastFrameCaption.AutoSize = true;
        lblLastFrameCaption.Text = "最近送圖";
        lblLastFrameValue.AutoSize = true;
        lblLastFrameValue.Text = "-";
        lblLastErrorCaption.AutoSize = true;
        lblLastErrorCaption.Margin = new Padding(16, 0, 3, 0);
        lblLastErrorCaption.Text = "最近錯誤";
        txtLastError.Dock = DockStyle.Fill;
        txtLastError.Location = new Point(16, 231);
        txtLastError.Margin = new Padding(16, 3, 16, 3);
        txtLastError.Multiline = true;
        txtLastError.ReadOnly = true;
        lblEventsCaption.AutoSize = true;
        lblEventsCaption.Margin = new Padding(16, 0, 3, 0);
        lblEventsCaption.Text = "最近事件";
        listEvents.Dock = DockStyle.Fill;
        listEvents.FormattingEnabled = true;
        listEvents.ItemHeight = 15;
        listEvents.Location = new Point(16, 313);
        listEvents.Margin = new Padding(16, 3, 16, 16);
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(784, 461);
        Controls.Add(layoutRoot);
        MinimumSize = new Size(680, 420);
        Name = "AgentMainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RemoteDesktop Agent";
        layoutRoot.ResumeLayout(false);
        layoutRoot.PerformLayout();
        layoutSummary.ResumeLayout(false);
        layoutSummary.PerformLayout();
        ResumeLayout(false);
    }
}
#nullable restore
