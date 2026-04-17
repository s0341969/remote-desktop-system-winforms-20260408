#nullable disable
namespace RemoteDesktop.Host.Forms;

partial class RemoteViewerForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutRoot;
    private Panel panelTop;
    private Panel panelViewer;
    private Button btnActions;
    private ContextMenuStrip menuActions;
    private ToolStripMenuItem menuOpenTransferFolder;
    private ToolStripMenuItem menuSendClipboard;
    private ToolStripMenuItem menuGetClipboard;
    private ToolStripMenuItem menuUploadFile;
    private ToolStripMenuItem menuDownloadFile;
    private ToolStripMenuItem menuTakeControl;
    private ToolStripMenuItem menuSecureAttention;
    private ToolStripMenuItem menuFullscreen;
    private ToolStripMenuItem menuFocusRemote;
    private ToolStripMenuItem menuDisconnect;
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
    private Button btnDownloadFile;
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
        components = new System.ComponentModel.Container();
        layoutRoot = new TableLayoutPanel();
        panelTop = new Panel();
        btnActions = new Button();
        menuActions = new ContextMenuStrip(components);
        menuOpenTransferFolder = new ToolStripMenuItem();
        menuSendClipboard = new ToolStripMenuItem();
        menuGetClipboard = new ToolStripMenuItem();
        menuUploadFile = new ToolStripMenuItem();
        menuDownloadFile = new ToolStripMenuItem();
        menuTakeControl = new ToolStripMenuItem();
        menuSecureAttention = new ToolStripMenuItem();
        menuFullscreen = new ToolStripMenuItem();
        menuFocusRemote = new ToolStripMenuItem();
        menuDisconnect = new ToolStripMenuItem();
        btnDisconnect = new Button();
        btnFocusRemote = new Button();
        btnFullscreen = new Button();
        cboZoom = new ComboBox();
        lblZoomCaption = new Label();
        btnUploadFile = new Button();
        btnDownloadFile = new Button();
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
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 152F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.Size = new Size(1420, 860);
        layoutRoot.TabIndex = 0;
        // 
        // panelTop
        // 
        panelTop.Controls.Add(btnActions);
        panelTop.Controls.Add(btnDisconnect);
        panelTop.Controls.Add(btnFocusRemote);
        panelTop.Controls.Add(btnFullscreen);
        panelTop.Controls.Add(cboZoom);
        panelTop.Controls.Add(lblZoomCaption);
        panelTop.Controls.Add(btnUploadFile);
        panelTop.Controls.Add(btnDownloadFile);
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
        panelTop.Size = new Size(1414, 146);
        panelTop.TabIndex = 0;
        // 
        // btnActions
        // 
        btnActions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnActions.Location = new Point(1305, 18);
        btnActions.Name = "btnActions";
        btnActions.Size = new Size(94, 42);
        btnActions.TabIndex = 9;
        btnActions.Text = "Actions";
        btnActions.UseVisualStyleBackColor = true;
        btnActions.Click += btnActions_Click;
        // 
        // menuActions
        // 
        menuActions.Items.AddRange(new ToolStripItem[] { menuOpenTransferFolder, menuSendClipboard, menuGetClipboard, menuUploadFile, menuDownloadFile, menuTakeControl, menuSecureAttention, menuFullscreen, menuFocusRemote, menuDisconnect });
        menuActions.Name = "menuActions";
        menuActions.Size = new Size(208, 246);
        // 
        // menuOpenTransferFolder
        // 
        menuOpenTransferFolder.Name = "menuOpenTransferFolder";
        menuOpenTransferFolder.Size = new Size(180, 22);
        menuOpenTransferFolder.Text = "Open Folder";
        menuOpenTransferFolder.Click += menuOpenTransferFolder_Click;
        // 
        // menuSendClipboard
        // 
        menuSendClipboard.Name = "menuSendClipboard";
        menuSendClipboard.Size = new Size(180, 22);
        menuSendClipboard.Text = "Send Clipboard";
        menuSendClipboard.Click += menuSendClipboard_Click;
        // 
        // menuGetClipboard
        // 
        menuGetClipboard.Name = "menuGetClipboard";
        menuGetClipboard.Size = new Size(180, 22);
        menuGetClipboard.Text = "Get Clipboard";
        menuGetClipboard.Click += menuGetClipboard_Click;
        // 
        // menuUploadFile
        // 
        menuUploadFile.Name = "menuUploadFile";
        menuUploadFile.Size = new Size(180, 22);
        menuUploadFile.Text = "Upload File";
        menuUploadFile.Click += menuUploadFile_Click;
        // 
        // menuDownloadFile
        // 
        menuDownloadFile.Name = "menuDownloadFile";
        menuDownloadFile.Size = new Size(180, 22);
        menuDownloadFile.Text = "Download File";
        menuDownloadFile.Click += menuDownloadFile_Click;
        // 
        // menuTakeControl
        // 
        menuTakeControl.Name = "menuTakeControl";
        menuTakeControl.Size = new Size(180, 22);
        menuTakeControl.Text = "Take Control";
        menuTakeControl.Click += menuTakeControl_Click;
        // 
        // menuSecureAttention
        // 
        menuSecureAttention.Name = "menuSecureAttention";
        menuSecureAttention.Size = new Size(207, 22);
        menuSecureAttention.Text = "Switch to Sign-in";
        menuSecureAttention.Click += menuSecureAttention_Click;
        // 
        // menuFullscreen
        // 
        menuFullscreen.Name = "menuFullscreen";
        menuFullscreen.Size = new Size(207, 22);
        menuFullscreen.Text = "Fullscreen";
        menuFullscreen.Click += menuFullscreen_Click;
        // 
        // menuFocusRemote
        // 
        menuFocusRemote.Name = "menuFocusRemote";
        menuFocusRemote.Size = new Size(207, 22);
        menuFocusRemote.Text = "Focus";
        menuFocusRemote.Click += btnFocusRemote_Click;
        // 
        // menuDisconnect
        // 
        menuDisconnect.Name = "menuDisconnect";
        menuDisconnect.Size = new Size(207, 22);
        menuDisconnect.Text = "Disconnect";
        menuDisconnect.Click += btnDisconnect_Click;
        // 
        // btnDisconnect
        // 
        btnDisconnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnDisconnect.Location = new Point(1305, 66);
        btnDisconnect.Name = "btnDisconnect";
        btnDisconnect.Size = new Size(87, 42);
        btnDisconnect.TabIndex = 8;
        btnDisconnect.Text = "Disconnect";
        btnDisconnect.UseVisualStyleBackColor = true;
        btnDisconnect.Visible = false;
        btnDisconnect.Click += btnDisconnect_Click;
        // 
        // btnFocusRemote
        // 
        btnFocusRemote.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnFocusRemote.Location = new Point(1211, 66);
        btnFocusRemote.Name = "btnFocusRemote";
        btnFocusRemote.Size = new Size(88, 42);
        btnFocusRemote.TabIndex = 7;
        btnFocusRemote.Text = "Focus";
        btnFocusRemote.UseVisualStyleBackColor = true;
        btnFocusRemote.Visible = false;
        btnFocusRemote.Click += btnFocusRemote_Click;
        // 
        // btnFullscreen
        // 
        btnFullscreen.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnFullscreen.Location = new Point(1103, 66);
        btnFullscreen.Name = "btnFullscreen";
        btnFullscreen.Size = new Size(102, 42);
        btnFullscreen.TabIndex = 6;
        btnFullscreen.Text = "Fullscreen";
        btnFullscreen.UseVisualStyleBackColor = true;
        btnFullscreen.Visible = false;
        btnFullscreen.Click += btnFullscreen_Click;
        // 
        // cboZoom
        // 
        cboZoom.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cboZoom.DropDownStyle = ComboBoxStyle.DropDownList;
        cboZoom.FormattingEnabled = true;
        cboZoom.Location = new Point(1016, 27);
        cboZoom.Name = "cboZoom";
        cboZoom.Size = new Size(88, 23);
        cboZoom.TabIndex = 5;
        cboZoom.SelectedIndexChanged += cboZoom_SelectedIndexChanged;
        // 
        // lblZoomCaption
        // 
        lblZoomCaption.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblZoomCaption.AutoSize = true;
        lblZoomCaption.Location = new Point(972, 31);
        lblZoomCaption.Name = "lblZoomCaption";
        lblZoomCaption.Size = new Size(38, 15);
        lblZoomCaption.TabIndex = 4;
        lblZoomCaption.Text = "Zoom";
        // 
        // btnUploadFile
        // 
        btnUploadFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnUploadFile.Location = new Point(803, 66);
        btnUploadFile.Name = "btnUploadFile";
        btnUploadFile.Size = new Size(94, 42);
        btnUploadFile.TabIndex = 3;
        btnUploadFile.Text = "Upload File";
        btnUploadFile.UseVisualStyleBackColor = true;
        btnUploadFile.Visible = false;
        btnUploadFile.Click += btnUploadFile_Click;
        // 
        // btnDownloadFile
        // 
        btnDownloadFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnDownloadFile.Location = new Point(903, 66);
        btnDownloadFile.Name = "btnDownloadFile";
        btnDownloadFile.Size = new Size(100, 42);
        btnDownloadFile.TabIndex = 4;
        btnDownloadFile.Text = "Download File";
        btnDownloadFile.UseVisualStyleBackColor = true;
        btnDownloadFile.Visible = false;
        btnDownloadFile.Click += btnDownloadFile_Click;
        // 
        // btnGetClipboard
        // 
        btnGetClipboard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnGetClipboard.Location = new Point(699, 66);
        btnGetClipboard.Name = "btnGetClipboard";
        btnGetClipboard.Size = new Size(98, 42);
        btnGetClipboard.TabIndex = 2;
        btnGetClipboard.Text = "Get Clipboard";
        btnGetClipboard.UseVisualStyleBackColor = true;
        btnGetClipboard.Visible = false;
        btnGetClipboard.Click += btnGetClipboard_Click;
        // 
        // btnSendClipboard
        // 
        btnSendClipboard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSendClipboard.Location = new Point(559, 66);
        btnSendClipboard.Name = "btnSendClipboard";
        btnSendClipboard.Size = new Size(134, 42);
        btnSendClipboard.TabIndex = 1;
        btnSendClipboard.Text = "Send Clipboard";
        btnSendClipboard.UseVisualStyleBackColor = true;
        btnSendClipboard.Visible = false;
        btnSendClipboard.Click += btnSendClipboard_Click;
        // 
        // btnOpenTransferFolder
        // 
        btnOpenTransferFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnOpenTransferFolder.Location = new Point(1148, 66);
        btnOpenTransferFolder.Name = "btnOpenTransferFolder";
        btnOpenTransferFolder.Size = new Size(144, 42);
        btnOpenTransferFolder.TabIndex = 0;
        btnOpenTransferFolder.Text = "Open Folder";
        btnOpenTransferFolder.UseVisualStyleBackColor = true;
        btnOpenTransferFolder.Visible = false;
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
        panelViewer.Location = new Point(3, 155);
        panelViewer.Name = "panelViewer";
        panelViewer.Size = new Size(1414, 702);
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
