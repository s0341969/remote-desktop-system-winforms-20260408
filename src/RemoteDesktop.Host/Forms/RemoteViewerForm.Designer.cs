#nullable disable
namespace RemoteDesktop.Host.Forms;

partial class RemoteViewerForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutRoot;
    private Panel panelTop;
    private Panel panelViewer;
    private Label lblDeviceCaption;
    private Label lblDeviceValue;
    private Label lblHostCaption;
    private Label lblHostValue;
    private Label lblResolutionCaption;
    private Label lblResolutionValue;
    private Label lblStatusCaption;
    private Label lblStatusValue;
    private Label lblClipboardCaption;
    private Label lblClipboardValue;
    private Label lblTransferCaption;
    private Label lblTransferValue;
    private Label lblTransferPathValue;
    private Label lblZoomCaption;
    private ProgressBar progressFileTransfer;
    private ComboBox cboZoom;
    private Button btnOpenTransferFolder;
    private Button btnGetClipboard;
    private Button btnSendClipboard;
    private Button btnUploadFile;
    private Button btnFullscreen;
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
        btnFullscreen = new Button();
        cboZoom = new ComboBox();
        lblZoomCaption = new Label();
        btnUploadFile = new Button();
        btnGetClipboard = new Button();
        btnSendClipboard = new Button();
        btnOpenTransferFolder = new Button();
        progressFileTransfer = new ProgressBar();
        lblTransferPathValue = new Label();
        lblTransferValue = new Label();
        lblTransferCaption = new Label();
        lblClipboardValue = new Label();
        lblClipboardCaption = new Label();
        lblStatusValue = new Label();
        lblStatusCaption = new Label();
        lblResolutionValue = new Label();
        lblResolutionCaption = new Label();
        lblHostValue = new Label();
        lblHostCaption = new Label();
        lblDeviceValue = new Label();
        lblDeviceCaption = new Label();
        panelViewer = new Panel();
        pictureStream = new PictureBox();
        layoutRoot.SuspendLayout();
        panelTop.SuspendLayout();
        panelViewer.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)pictureStream).BeginInit();
        SuspendLayout();
        // 
        // layoutRoot
        // 
        layoutRoot.ColumnCount = 1;
        layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutRoot.Controls.Add(panelTop, 0, 0);
        layoutRoot.Controls.Add(panelViewer, 0, 1);
        layoutRoot.Dock = DockStyle.Fill;
        layoutRoot.Location = new Point(0, 0);
        layoutRoot.Name = "layoutRoot";
        layoutRoot.RowCount = 2;
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 272F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.Size = new Size(1420, 860);
        layoutRoot.TabIndex = 0;
        // 
        // panelTop
        // 
        panelTop.Controls.Add(btnDisconnect);
        panelTop.Controls.Add(btnFocusRemote);
        panelTop.Controls.Add(btnFullscreen);
        panelTop.Controls.Add(cboZoom);
        panelTop.Controls.Add(lblZoomCaption);
        panelTop.Controls.Add(btnUploadFile);
        panelTop.Controls.Add(btnGetClipboard);
        panelTop.Controls.Add(btnSendClipboard);
        panelTop.Controls.Add(btnOpenTransferFolder);
        panelTop.Controls.Add(progressFileTransfer);
        panelTop.Controls.Add(lblTransferPathValue);
        panelTop.Controls.Add(lblTransferValue);
        panelTop.Controls.Add(lblTransferCaption);
        panelTop.Controls.Add(lblClipboardValue);
        panelTop.Controls.Add(lblClipboardCaption);
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
        panelTop.Size = new Size(1414, 266);
        panelTop.TabIndex = 0;
        // 
        // btnDisconnect
        // 
        btnDisconnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnDisconnect.Location = new Point(1300, 18);
        btnDisconnect.Name = "btnDisconnect";
        btnDisconnect.Size = new Size(99, 42);
        btnDisconnect.TabIndex = 8;
        btnDisconnect.Text = "Disconnect";
        btnDisconnect.UseVisualStyleBackColor = true;
        btnDisconnect.Click += btnDisconnect_Click;
        // 
        // btnFocusRemote
        // 
        btnFocusRemote.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnFocusRemote.Location = new Point(1198, 18);
        btnFocusRemote.Name = "btnFocusRemote";
        btnFocusRemote.Size = new Size(96, 42);
        btnFocusRemote.TabIndex = 7;
        btnFocusRemote.Text = "Focus";
        btnFocusRemote.UseVisualStyleBackColor = true;
        btnFocusRemote.Click += btnFocusRemote_Click;
        // 
        // btnFullscreen
        // 
        btnFullscreen.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnFullscreen.Location = new Point(1080, 18);
        btnFullscreen.Name = "btnFullscreen";
        btnFullscreen.Size = new Size(112, 42);
        btnFullscreen.TabIndex = 6;
        btnFullscreen.Text = "Fullscreen";
        btnFullscreen.UseVisualStyleBackColor = true;
        btnFullscreen.Click += btnFullscreen_Click;
        // 
        // cboZoom
        // 
        cboZoom.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cboZoom.DropDownStyle = ComboBoxStyle.DropDownList;
        cboZoom.FormattingEnabled = true;
        cboZoom.Location = new Point(986, 27);
        cboZoom.Name = "cboZoom";
        cboZoom.Size = new Size(88, 23);
        cboZoom.TabIndex = 5;
        cboZoom.SelectedIndexChanged += cboZoom_SelectedIndexChanged;
        // 
        // lblZoomCaption
        // 
        lblZoomCaption.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblZoomCaption.AutoSize = true;
        lblZoomCaption.Location = new Point(942, 31);
        lblZoomCaption.Name = "lblZoomCaption";
        lblZoomCaption.Size = new Size(38, 15);
        lblZoomCaption.TabIndex = 4;
        lblZoomCaption.Text = "Zoom";
        // 
        // btnUploadFile
        // 
        btnUploadFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnUploadFile.Location = new Point(828, 18);
        btnUploadFile.Name = "btnUploadFile";
        btnUploadFile.Size = new Size(102, 42);
        btnUploadFile.TabIndex = 3;
        btnUploadFile.Text = "Upload File";
        btnUploadFile.UseVisualStyleBackColor = true;
        btnUploadFile.Click += btnUploadFile_Click;
        // 
        // btnGetClipboard
        // 
        btnGetClipboard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnGetClipboard.Location = new Point(720, 18);
        btnGetClipboard.Name = "btnGetClipboard";
        btnGetClipboard.Size = new Size(102, 42);
        btnGetClipboard.TabIndex = 2;
        btnGetClipboard.Text = "Get Clipboard";
        btnGetClipboard.UseVisualStyleBackColor = true;
        btnGetClipboard.Click += btnGetClipboard_Click;
        // 
        // btnSendClipboard
        // 
        btnSendClipboard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSendClipboard.Location = new Point(584, 18);
        btnSendClipboard.Name = "btnSendClipboard";
        btnSendClipboard.Size = new Size(130, 42);
        btnSendClipboard.TabIndex = 1;
        btnSendClipboard.Text = "Send Clipboard";
        btnSendClipboard.UseVisualStyleBackColor = true;
        btnSendClipboard.Click += btnSendClipboard_Click;
        // 
        // btnOpenTransferFolder
        // 
        btnOpenTransferFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnOpenTransferFolder.Location = new Point(468, 18);
        btnOpenTransferFolder.Name = "btnOpenTransferFolder";
        btnOpenTransferFolder.Size = new Size(110, 42);
        btnOpenTransferFolder.TabIndex = 0;
        btnOpenTransferFolder.Text = "Open Folder";
        btnOpenTransferFolder.UseVisualStyleBackColor = true;
        btnOpenTransferFolder.Click += btnOpenTransferFolder_Click;
        // 
        // progressFileTransfer
        // 
        progressFileTransfer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        progressFileTransfer.Location = new Point(140, 228);
        progressFileTransfer.Maximum = 100;
        progressFileTransfer.Name = "progressFileTransfer";
        progressFileTransfer.Size = new Size(1259, 14);
        progressFileTransfer.Style = ProgressBarStyle.Continuous;
        progressFileTransfer.TabIndex = 14;
        // 
        // lblTransferPathValue
        // 
        lblTransferPathValue.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblTransferPathValue.AutoEllipsis = true;
        lblTransferPathValue.Location = new Point(140, 182);
        lblTransferPathValue.Name = "lblTransferPathValue";
        lblTransferPathValue.Size = new Size(1259, 34);
        lblTransferPathValue.TabIndex = 13;
        lblTransferPathValue.Text = "-";
        // 
        // lblTransferValue
        // 
        lblTransferValue.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblTransferValue.AutoEllipsis = true;
        lblTransferValue.Location = new Point(140, 146);
        lblTransferValue.Name = "lblTransferValue";
        lblTransferValue.Size = new Size(1259, 30);
        lblTransferValue.TabIndex = 12;
        lblTransferValue.Text = "-";
        // 
        // lblTransferCaption
        // 
        lblTransferCaption.AutoSize = false;
        lblTransferCaption.Location = new Point(18, 146);
        lblTransferCaption.Name = "lblTransferCaption";
        lblTransferCaption.Size = new Size(108, 70);
        lblTransferCaption.TabIndex = 11;
        lblTransferCaption.Text = "Transfer";
        // 
        // lblClipboardValue
        // 
        lblClipboardValue.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblClipboardValue.AutoEllipsis = true;
        lblClipboardValue.Location = new Point(140, 98);
        lblClipboardValue.Name = "lblClipboardValue";
        lblClipboardValue.Size = new Size(1259, 38);
        lblClipboardValue.TabIndex = 10;
        lblClipboardValue.Text = "-";
        // 
        // lblClipboardCaption
        // 
        lblClipboardCaption.AutoSize = false;
        lblClipboardCaption.Location = new Point(18, 98);
        lblClipboardCaption.Name = "lblClipboardCaption";
        lblClipboardCaption.Size = new Size(108, 38);
        lblClipboardCaption.TabIndex = 9;
        lblClipboardCaption.Text = "Clipboard";
        // 
        // lblStatusValue
        // 
        lblStatusValue.AutoSize = true;
        lblStatusValue.Location = new Point(482, 52);
        lblStatusValue.Name = "lblStatusValue";
        lblStatusValue.Size = new Size(12, 15);
        lblStatusValue.TabIndex = 8;
        lblStatusValue.Text = "-";
        // 
        // lblStatusCaption
        // 
        lblStatusCaption.AutoSize = false;
        lblStatusCaption.Location = new Point(360, 52);
        lblStatusCaption.Name = "lblStatusCaption";
        lblStatusCaption.Size = new Size(108, 32);
        lblStatusCaption.TabIndex = 7;
        lblStatusCaption.Text = "Status";
        // 
        // lblResolutionValue
        // 
        lblResolutionValue.AutoSize = true;
        lblResolutionValue.Location = new Point(482, 16);
        lblResolutionValue.Name = "lblResolutionValue";
        lblResolutionValue.Size = new Size(12, 15);
        lblResolutionValue.TabIndex = 6;
        lblResolutionValue.Text = "-";
        // 
        // lblResolutionCaption
        // 
        lblResolutionCaption.AutoSize = false;
        lblResolutionCaption.Location = new Point(360, 16);
        lblResolutionCaption.Name = "lblResolutionCaption";
        lblResolutionCaption.Size = new Size(108, 32);
        lblResolutionCaption.TabIndex = 5;
        lblResolutionCaption.Text = "Resolution";
        // 
        // lblHostValue
        // 
        lblHostValue.AutoSize = true;
        lblHostValue.Location = new Point(140, 52);
        lblHostValue.Name = "lblHostValue";
        lblHostValue.Size = new Size(12, 15);
        lblHostValue.TabIndex = 4;
        lblHostValue.Text = "-";
        // 
        // lblHostCaption
        // 
        lblHostCaption.AutoSize = false;
        lblHostCaption.Location = new Point(18, 52);
        lblHostCaption.Name = "lblHostCaption";
        lblHostCaption.Size = new Size(108, 32);
        lblHostCaption.TabIndex = 3;
        lblHostCaption.Text = "Host";
        // 
        // lblDeviceValue
        // 
        lblDeviceValue.AutoSize = true;
        lblDeviceValue.Location = new Point(140, 16);
        lblDeviceValue.Name = "lblDeviceValue";
        lblDeviceValue.Size = new Size(12, 15);
        lblDeviceValue.TabIndex = 2;
        lblDeviceValue.Text = "-";
        // 
        // lblDeviceCaption
        // 
        lblDeviceCaption.AutoSize = false;
        lblDeviceCaption.Location = new Point(18, 16);
        lblDeviceCaption.Name = "lblDeviceCaption";
        lblDeviceCaption.Size = new Size(108, 32);
        lblDeviceCaption.TabIndex = 1;
        lblDeviceCaption.Text = "Device";
        // 
        // panelViewer
        // 
        panelViewer.AutoScroll = true;
        panelViewer.BackColor = Color.Black;
        panelViewer.Controls.Add(pictureStream);
        panelViewer.Dock = DockStyle.Fill;
        panelViewer.Location = new Point(3, 275);
        panelViewer.Name = "panelViewer";
        panelViewer.Size = new Size(1414, 582);
        panelViewer.TabIndex = 1;
        panelViewer.Resize += panelViewer_Resize;
        // 
        // pictureStream
        // 
        pictureStream.BackColor = Color.Black;
        pictureStream.Location = new Point(0, 0);
        pictureStream.Name = "pictureStream";
        pictureStream.Size = new Size(1414, 606);
        pictureStream.SizeMode = PictureBoxSizeMode.Zoom;
        pictureStream.TabIndex = 0;
        pictureStream.TabStop = true;
        pictureStream.MouseDown += pictureStream_MouseDown;
        pictureStream.MouseMove += pictureStream_MouseMove;
        pictureStream.MouseUp += pictureStream_MouseUp;
        pictureStream.MouseWheel += pictureStream_MouseWheel;
        pictureStream.DoubleClick += pictureStream_DoubleClick;
        // 
        // RemoteViewerForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1420, 860);
        Controls.Add(layoutRoot);
        MinimumSize = new Size(1040, 720);
        Name = "RemoteViewerForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Remote Viewer";
        KeyDown += RemoteViewerForm_KeyDown;
        KeyPress += RemoteViewerForm_KeyPress;
        KeyUp += RemoteViewerForm_KeyUp;
        layoutRoot.ResumeLayout(false);
        panelTop.ResumeLayout(false);
        panelTop.PerformLayout();
        panelViewer.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)pictureStream).EndInit();
        ResumeLayout(false);
    }
}
#nullable restore
