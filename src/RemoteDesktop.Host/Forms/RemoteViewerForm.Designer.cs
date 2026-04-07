#nullable disable
namespace RemoteDesktop.Host.Forms;

partial class RemoteViewerForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutRoot;
    private Panel panelTop;
    private Label lblDeviceCaption;
    private Label lblDeviceValue;
    private Label lblHostCaption;
    private Label lblHostValue;
    private Label lblResolutionCaption;
    private Label lblResolutionValue;
    private Label lblStatusCaption;
    private Label lblStatusValue;
    private Button btnFocusRemote;
    private Button btnDisconnect;
    private PictureBox pictureStream;

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
        panelTop = new Panel();
        btnDisconnect = new Button();
        btnFocusRemote = new Button();
        lblStatusValue = new Label();
        lblStatusCaption = new Label();
        lblResolutionValue = new Label();
        lblResolutionCaption = new Label();
        lblHostValue = new Label();
        lblHostCaption = new Label();
        lblDeviceValue = new Label();
        lblDeviceCaption = new Label();
        pictureStream = new PictureBox();
        layoutRoot.SuspendLayout();
        panelTop.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)pictureStream).BeginInit();
        SuspendLayout();
        layoutRoot.ColumnCount = 1;
        layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutRoot.Controls.Add(panelTop, 0, 0);
        layoutRoot.Controls.Add(pictureStream, 0, 1);
        layoutRoot.Dock = DockStyle.Fill;
        layoutRoot.Location = new Point(0, 0);
        layoutRoot.Name = "layoutRoot";
        layoutRoot.RowCount = 2;
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.Size = new Size(1264, 761);
        panelTop.Controls.Add(btnDisconnect);
        panelTop.Controls.Add(btnFocusRemote);
        panelTop.Controls.Add(lblStatusValue);
        panelTop.Controls.Add(lblStatusCaption);
        panelTop.Controls.Add(lblResolutionValue);
        panelTop.Controls.Add(lblResolutionCaption);
        panelTop.Controls.Add(lblHostValue);
        panelTop.Controls.Add(lblHostCaption);
        panelTop.Controls.Add(lblDeviceValue);
        panelTop.Controls.Add(lblDeviceCaption);
        panelTop.Dock = DockStyle.Fill;
        panelTop.Location = new Point(3, 3);
        panelTop.Name = "panelTop";
        panelTop.Size = new Size(1258, 84);
        btnDisconnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnDisconnect.Location = new Point(1156, 21);
        btnDisconnect.Name = "btnDisconnect";
        btnDisconnect.Size = new Size(87, 34);
        btnDisconnect.Text = "關閉檢視";
        btnDisconnect.UseVisualStyleBackColor = true;
        btnDisconnect.Click += btnDisconnect_Click;
        btnFocusRemote.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnFocusRemote.Location = new Point(1050, 21);
        btnFocusRemote.Name = "btnFocusRemote";
        btnFocusRemote.Size = new Size(100, 34);
        btnFocusRemote.Text = "聚焦遠端畫面";
        btnFocusRemote.UseVisualStyleBackColor = true;
        btnFocusRemote.Click += btnFocusRemote_Click;
        lblDeviceCaption.AutoSize = true;
        lblDeviceCaption.Location = new Point(18, 16);
        lblDeviceCaption.Text = "裝置";
        lblDeviceValue.AutoSize = true;
        lblDeviceValue.Location = new Point(78, 16);
        lblDeviceValue.Text = "-";
        lblHostCaption.AutoSize = true;
        lblHostCaption.Location = new Point(18, 40);
        lblHostCaption.Text = "主機";
        lblHostValue.AutoSize = true;
        lblHostValue.Location = new Point(78, 40);
        lblHostValue.Text = "-";
        lblResolutionCaption.AutoSize = true;
        lblResolutionCaption.Location = new Point(18, 63);
        lblResolutionCaption.Text = "解析度";
        lblResolutionValue.AutoSize = true;
        lblResolutionValue.Location = new Point(78, 63);
        lblResolutionValue.Text = "-";
        lblStatusCaption.AutoSize = true;
        lblStatusCaption.Location = new Point(405, 16);
        lblStatusCaption.Text = "狀態";
        lblStatusValue.AutoSize = true;
        lblStatusValue.Location = new Point(457, 16);
        lblStatusValue.Text = "待命";
        pictureStream.BackColor = Color.Black;
        pictureStream.Dock = DockStyle.Fill;
        pictureStream.Location = new Point(3, 93);
        pictureStream.Name = "pictureStream";
        pictureStream.Size = new Size(1258, 665);
        pictureStream.SizeMode = PictureBoxSizeMode.Zoom;
        pictureStream.TabStop = true;
        pictureStream.MouseDown += pictureStream_MouseDown;
        pictureStream.MouseMove += pictureStream_MouseMove;
        pictureStream.MouseUp += pictureStream_MouseUp;
        pictureStream.MouseWheel += pictureStream_MouseWheel;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1264, 761);
        Controls.Add(layoutRoot);
        MinimumSize = new Size(900, 600);
        Name = "RemoteViewerForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "遠端檢視";
        KeyDown += RemoteViewerForm_KeyDown;
        KeyPress += RemoteViewerForm_KeyPress;
        KeyUp += RemoteViewerForm_KeyUp;
        layoutRoot.ResumeLayout(false);
        panelTop.ResumeLayout(false);
        panelTop.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)pictureStream).EndInit();
        ResumeLayout(false);
    }
}
#nullable restore
