using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace RemoteDesktop.Host.Forms;

public partial class RemoteViewerForm : Form
{
    private const int MaxClipboardCharacters = 32_768;
    // Keep each Base64 payload well below the LOH threshold to avoid large transient string allocations.
    private const int UploadChunkBytes = 16 * 1024;
    private static readonly JsonSerializerOptions TraceJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<Keys, string> KeyCodeMap = new Dictionary<Keys, string>
    {
        [Keys.Enter] = "Enter",
        [Keys.Tab] = "Tab",
        [Keys.Escape] = "Escape",
        [Keys.Space] = "Space",
        [Keys.Back] = "Backspace",
        [Keys.Left] = "ArrowLeft",
        [Keys.Right] = "ArrowRight",
        [Keys.Up] = "ArrowUp",
        [Keys.Down] = "ArrowDown",
        [Keys.Home] = "Home",
        [Keys.End] = "End",
        [Keys.PageUp] = "PageUp",
        [Keys.PageDown] = "PageDown",
        [Keys.Insert] = "Insert",
        [Keys.Delete] = "Delete",
        [Keys.F1] = "F1",
        [Keys.F2] = "F2",
        [Keys.F3] = "F3",
        [Keys.F4] = "F4",
        [Keys.F5] = "F5",
        [Keys.F6] = "F6",
        [Keys.F7] = "F7",
        [Keys.F8] = "F8",
        [Keys.F9] = "F9",
        [Keys.F10] = "F10",
        [Keys.F11] = "F11",
        [Keys.F12] = "F12",
        [Keys.OemSemicolon] = "Semicolon",
        [Keys.Oemplus] = "Equal",
        [Keys.Oemcomma] = "Comma",
        [Keys.OemMinus] = "Minus",
        [Keys.OemPeriod] = "Period",
        [Keys.OemQuestion] = "Slash",
        [Keys.Oemtilde] = "Backquote",
        [Keys.OemOpenBrackets] = "BracketLeft",
        [Keys.OemPipe] = "Backslash",
        [Keys.OemCloseBrackets] = "BracketRight",
        [Keys.OemQuotes] = "Quote",
        [Keys.ShiftKey] = "ShiftLeft",
        [Keys.ControlKey] = "ControlLeft",
        [Keys.Menu] = "AltLeft"
    };

    private DeviceRecord? _device;
    private AuthenticatedUserSession? _viewer;
    private IRemoteViewerSessionBroker? _viewerSessionBroker;
    private bool _sessionCanControl;
    private string? _controllerDisplayName;
    private Size _frameSize = Size.Empty;
    private bool _attached;
    private long _lastMoveAt;
    private bool _commandFailed;
    private bool _observeOnlyNoticeShown;
    private string? _activeUploadId;
    private string? _activeDownloadId;
    private string? _lastUploadedFilePath;
    private string? _lastDownloadFilePath;
    private FileTransferTraceService? _fileTransferTraceService;
    private TaskCompletionSource<AgentClipboardMessage>? _clipboardSyncSignal;
    private TaskCompletionSource<AgentFileTransferStatusMessage>? _uploadStartSignal;
    private TaskCompletionSource<AgentFileTransferStatusMessage>? _uploadCompletionSignal;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AgentFileTransferStatusMessage>> _browseDirectorySignals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AgentFileTransferStatusMessage>> _moveEntrySignals = new(StringComparer.OrdinalIgnoreCase);
    private FileStream? _downloadStream;
    private string? _downloadTempPath;
    private string? _downloadTargetPath;
    private bool _transferPanelVisible;
    private const int TopPanelCollapsedHeight = 152;
    private const int TopPanelExpandedHeight = 272;
    private static readonly (string Key, double? Factor)[] ZoomPresets =
    {
        ("fit", null),
        ("50", 0.5d),
        ("75", 0.75d),
        ("100", 1.0d),
        ("125", 1.25d),
        ("150", 1.5d),
        ("200", 2.0d)
    };

    private bool _fitToWindow = true;
    private double _manualZoomFactor = 1d;
    private bool _suppressZoomSelectionChanged;
    private bool _isFullscreen;
    private FormBorderStyle _restoreBorderStyle;
    private FormWindowState _restoreWindowState;
    private Rectangle _restoreBounds;
    private bool _restoreTopMost;

    public RemoteViewerForm()
    {
        InitializeComponent();
        InitializeUiText();
        KeyPreview = true;
    }

    public void Bind(DeviceRecord device, AuthenticatedUserSession viewer, IRemoteViewerSessionBroker viewerSessionBroker, FileTransferTraceService? fileTransferTraceService = null)
    {
        _device = device;
        _viewer = viewer;
        _viewerSessionBroker = viewerSessionBroker;
        _fileTransferTraceService = fileTransferTraceService;
        _sessionCanControl = viewer.CanControlRemote;
        _controllerDisplayName = viewer.DisplayName;
        var baseWindowTitle = _sessionCanControl
            ? $"{device.DeviceName} - {HostUiText.Window("遠端檢視", "Remote Viewer")}"
            : $"{device.DeviceName} - {HostUiText.Window("遠端檢視（僅觀看）", "Remote Viewer (Observe Only)")}";
        Text = AppBuildInfo.AppendToWindowTitle(baseWindowTitle);
        lblDeviceValue.Text = $"{device.DeviceName} ({device.DeviceId})";
        lblHostValue.Text = device.HostName;
        lblResolutionValue.Text = $"{device.ScreenWidth} x {device.ScreenHeight}";
        btnSendClipboard.Enabled = _sessionCanControl;
        btnGetClipboard.Enabled = _sessionCanControl;
        btnUploadFile.Enabled = _sessionCanControl;
        btnDownloadFile.Enabled = _sessionCanControl;
        btnOpenTransferFolder.Enabled = false;
        SyncActionMenuState();
        LogTransferTrace("host-viewer-bound", "Remote viewer form bound to device.", new
        {
            deviceId = device.DeviceId,
            deviceName = device.DeviceName,
            hostName = device.HostName
        });
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_viewerSessionBroker is null || _device is null || _viewer is null)
        {
            Close();
            return;
        }

        lblStatusValue.Text = HostUiText.Bi("連線中...", "Connecting...");
        var attached = await _viewerSessionBroker.AttachViewerAsync(
            _device.DeviceId,
            _viewer,
            PublishFrameAsync,
            PublishStatusAsync,
            PublishClipboardAsync,
            PublishSessionStateAsync,
            CancellationToken.None);

        if (!attached.Attached)
        {
            var message = string.IsNullOrWhiteSpace(attached.Message)
                ? (_device.IsAuthorized
                    ? HostUiText.Bi("所選裝置目前離線，或已被其他 Viewer 使用中。", "The selected device is offline or already in use by another viewer.")
                    : HostUiText.Bi("所選裝置仍在等待核准，請先核准無人值守存取後再開啟 Viewer。", "The selected device is waiting for approval. Approve unattended access before opening a viewer session."))
                : attached.Message;
            MessageBox.Show(message, HostUiText.Window("遠端檢視", "Remote Viewer"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
            return;
        }

        _attached = true;
        ApplySessionState(new RemoteViewerSessionState(attached.CanControl, attached.ControllerDisplayName, attached.Message), showNotification: attached.CanControl != _viewer.CanControlRemote);
        SetTransferPanelVisible(false);
        ConfigureZoomOptions();
        SyncActionMenuState();
        ApplyPictureLayout();
        pictureStream.Focus();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_activeUploadId))
        {
            await TryAbortUploadAsync(_activeUploadId);
        }

        if (_downloadStream is not null)
        {
            await _downloadStream.DisposeAsync();
            _downloadStream = null;
        }

        TryDeleteFile(_downloadTempPath);

        if (_attached && _viewerSessionBroker is not null && _device is not null)
        {
            await _viewerSessionBroker.DetachViewerAsync(_device.DeviceId, CancellationToken.None);
            _attached = false;
        }

        if (_viewerSessionBroker is not null)
        {
            await _viewerSessionBroker.DisposeAsync();
            _viewerSessionBroker = null;
        }

        if (pictureStream.Image is not null)
        {
            pictureStream.Image.Dispose();
            pictureStream.Image = null;
        }

        base.OnFormClosing(e);
    }

    private Task PublishFrameAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (IsDisposed)
        {
            return Task.CompletedTask;
        }

        var frameCopy = payload.ToArray();
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => ApplyFrame(frameCopy)));
            }
            catch (InvalidOperationException)
            {
                return Task.CompletedTask;
            }
        }
        else
        {
            ApplyFrame(frameCopy);
        }

        return Task.CompletedTask;
    }

    private Task PublishStatusAsync(AgentFileTransferStatusMessage status, CancellationToken cancellationToken)
    {
        if (IsDisposed)
        {
            return Task.CompletedTask;
        }

        if (string.Equals(status.Direction, "download", StringComparison.OrdinalIgnoreCase)
            && string.Equals(status.Status, "chunk", StringComparison.OrdinalIgnoreCase))
        {
            ReceiveDownloadChunk(status);
            return Task.CompletedTask;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => ApplyTransferStatus(status)));
            }
            catch (InvalidOperationException)
            {
                return Task.CompletedTask;
            }
        }
        else
        {
            ApplyTransferStatus(status);
        }

        return Task.CompletedTask;
    }

    private Task PublishClipboardAsync(AgentClipboardMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed)
        {
            return Task.CompletedTask;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => ApplyClipboardMessage(message)));
            }
            catch (InvalidOperationException)
            {
                return Task.CompletedTask;
            }
        }
        else
        {
            ApplyClipboardMessage(message);
        }

        return Task.CompletedTask;
    }

    private Task PublishSessionStateAsync(RemoteViewerSessionState state, CancellationToken cancellationToken)
    {
        return UpdateUiAsync(() => ApplySessionState(state, showNotification: true));
    }

    private void ApplySessionState(RemoteViewerSessionState state, bool showNotification)
    {
        var previousCanControl = _sessionCanControl;
        _sessionCanControl = state.CanControl;
        _controllerDisplayName = state.ControllerDisplayName;

        var baseWindowTitle = _sessionCanControl
            ? $"{_device?.DeviceName} - {HostUiText.Window("遠端檢視", "Remote Viewer")}"
            : $"{_device?.DeviceName} - {HostUiText.Window("遠端檢視（僅觀看）", "Remote Viewer (Observe Only)")}";
        Text = AppBuildInfo.AppendToWindowTitle(baseWindowTitle);

        btnSendClipboard.Enabled = _sessionCanControl;
        btnGetClipboard.Enabled = _sessionCanControl;
        btnUploadFile.Enabled = _sessionCanControl;
        btnDownloadFile.Enabled = _sessionCanControl;

        lblStatusValue.Text = _sessionCanControl
            ? HostUiText.Bi("已連線（可控制）", "Connected (control)")
            : HostUiText.Bi("已連線（僅觀看）", "Connected (observe only)");
        lblTransferValue.Text = _sessionCanControl
            ? HostUiText.Bi("可開始上傳或下載檔案。", "Ready to upload or download files.")
            : HostUiText.Bi("僅觀看模式無法傳送檔案。", "Observe-only mode cannot transfer files.");
        lblClipboardValue.Text = _sessionCanControl
            ? HostUiText.Bi("剪貼簿同步已就緒。", "Clipboard sync is ready.")
            : HostUiText.Bi("僅觀看模式無法同步剪貼簿。", "Observe-only mode cannot sync clipboard.");

        SyncActionMenuState();

        if (showNotification && !string.IsNullOrWhiteSpace(state.Message))
        {
            MessageBox.Show(state.Message, HostUiText.Window("遠端檢視", "Remote Viewer"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else if (!previousCanControl && _sessionCanControl && !string.IsNullOrWhiteSpace(state.Message))
        {
            lblStatusValue.Text = HostUiText.Bi("已連線（可控制）", "Connected (control)");
        }
    }

    private void ApplyFrame(byte[] payload)
    {
        try
        {
            using var stream = new MemoryStream(payload);
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            var bitmap = new Bitmap(source);
            var frameSizeChanged = _frameSize != bitmap.Size;
            _frameSize = bitmap.Size;

            var previous = pictureStream.Image;
            pictureStream.Image = bitmap;
            previous?.Dispose();

            lblStatusValue.Text = HostUiText.Bi($"串流中 {_frameSize.Width} x {_frameSize.Height}", $"Streaming {_frameSize.Width} x {_frameSize.Height}");
            if (_fitToWindow || frameSizeChanged)
            {
                ApplyPictureLayout();
            }
        }
        catch (Exception exception)
        {
            lblStatusValue.Text = HostUiText.Bi($"畫面解碼失敗：{exception.Message}", $"Frame decode failed: {exception.Message}");
        }
    }

    protected void ApplyTransferStatus(AgentFileTransferStatusMessage status)
    {
        LogTransferTrace("host-status-received", $"Received Agent transfer status '{status.Status}'.", new
        {
            deviceId = _device?.DeviceId,
            uploadId = status.UploadId,
            direction = status.Direction,
            status = status.Status,
            fileName = status.FileName,
            storedFileName = status.StoredFileName,
            storedFilePath = status.StoredFilePath,
            bytesTransferred = status.BytesTransferred,
            fileSize = status.FileSize
        });

        if (string.Equals(status.Direction, "browse", StringComparison.OrdinalIgnoreCase))
        {
            HandleBrowseStatus(status);
            return;
        }

        if (string.Equals(status.Direction, "move", StringComparison.OrdinalIgnoreCase))
        {
            HandleMoveStatus(status);
            return;
        }

        if (string.Equals(status.Direction, "download", StringComparison.OrdinalIgnoreCase))
        {
            HandleDownloadStatus(status);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_activeUploadId) && !string.Equals(_activeUploadId, status.UploadId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetTransferPanelVisible(true);

        switch (status.Status)
        {
            case "started":
                _uploadStartSignal?.TrySetResult(status);
                progressFileTransfer.Value = 0;
                lblTransferValue.Text = status.Message;
                lblTransferPathValue.Text = HostUiText.Bi("目的地：等待 Agent 回報儲存位置。", "Destination: waiting for Agent to report the destination path.");
                _lastUploadedFilePath = null;
                btnDownloadFile.Enabled = false;
                btnOpenTransferFolder.Enabled = false;
                break;
            case "progress":
                UpdateTransferProgress(status.BytesTransferred, status.FileSize);
                lblTransferValue.Text = HostUiText.Bi(
                    $"{status.StoredFileName}：{FormatBytes(status.BytesTransferred)} / {FormatBytes(status.FileSize)}",
                    $"{status.StoredFileName}: {FormatBytes(status.BytesTransferred)} / {FormatBytes(status.FileSize)}");
                lblTransferPathValue.Text = string.IsNullOrWhiteSpace(status.StoredFilePath)
                    ? HostUiText.Bi("目的地：等待 Agent 建立檔案。", "Destination: waiting for Agent to create the file.")
                    : HostUiText.Bi($"目的地：{status.StoredFilePath}", $"Destination: {status.StoredFilePath}");
                break;
            case "completed":
                _uploadCompletionSignal?.TrySetResult(status);
                UpdateTransferProgress(status.FileSize, status.FileSize);
                btnUploadFile.Enabled = _sessionCanControl;
                btnDownloadFile.Enabled = _sessionCanControl;
                _lastUploadedFilePath = string.IsNullOrWhiteSpace(status.StoredFilePath) ? null : status.StoredFilePath;
                btnOpenTransferFolder.Enabled = !string.IsNullOrWhiteSpace(_lastUploadedFilePath);
                lblTransferValue.Text = status.Message;
                lblTransferPathValue.Text = string.IsNullOrWhiteSpace(_lastUploadedFilePath)
                    ? HostUiText.Bi("目的地：Agent 未回報完整路徑。", "Destination: Agent did not report a full path.")
                    : HostUiText.Bi($"目的地：{_lastUploadedFilePath}", $"Destination: {_lastUploadedFilePath}");
                _activeUploadId = null;
                break;
            case "failed":
                _uploadStartSignal?.TrySetResult(status);
                _uploadCompletionSignal?.TrySetResult(status);
                progressFileTransfer.Value = 0;
                lblTransferValue.Text = status.Message;
                lblTransferPathValue.Text = HostUiText.Bi("目的地：未建立。", "Destination: not created.");
                btnUploadFile.Enabled = _sessionCanControl;
                btnDownloadFile.Enabled = _sessionCanControl;
                _lastUploadedFilePath = null;
                btnOpenTransferFolder.Enabled = false;
                _activeUploadId = null;
                MessageBox.Show(status.Message, HostUiText.Window("檔案傳輸", "File Transfer"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
        }
    }

    private void HandleBrowseStatus(AgentFileTransferStatusMessage status)
    {
        if (string.IsNullOrWhiteSpace(status.UploadId))
        {
            return;
        }

        if (_browseDirectorySignals.TryRemove(status.UploadId, out var signal))
        {
            signal.TrySetResult(status);
        }
    }

    private void HandleMoveStatus(AgentFileTransferStatusMessage status)
    {
        if (string.IsNullOrWhiteSpace(status.UploadId))
        {
            return;
        }

        if (_moveEntrySignals.TryRemove(status.UploadId, out var signal))
        {
            signal.TrySetResult(status);
        }
    }

    private void HandleDownloadStatus(AgentFileTransferStatusMessage status)
    {
        if (!string.IsNullOrWhiteSpace(_activeDownloadId) && !string.Equals(_activeDownloadId, status.UploadId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetTransferPanelVisible(true);

        switch (status.Status)
        {
            case "started":
                progressFileTransfer.Value = 0;
                lblTransferValue.Text = status.Message;
                lblTransferPathValue.Text = string.IsNullOrWhiteSpace(_downloadTargetPath)
                    ? HostUiText.Bi("目的地：等待本機儲存位置。", "Destination: waiting for the local save path.")
                    : HostUiText.Bi($"目的地：{_downloadTargetPath}", $"Destination: {_downloadTargetPath}");
                btnUploadFile.Enabled = false;
                btnDownloadFile.Enabled = false;
                btnOpenTransferFolder.Enabled = false;
                break;
            case "chunk":
                break;
            case "progress":
                UpdateTransferProgress(status.BytesTransferred, status.FileSize);
                lblTransferValue.Text = status.Message;
                break;
            case "completed":
                FinalizeDownload(status);
                break;
            case "failed":
                FailDownload(status.Message);
                break;
        }
    }

    private void ApplyClipboardMessage(AgentClipboardMessage message)
    {
        _clipboardSyncSignal?.TrySetResult(message);

        if (string.Equals(message.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            lblClipboardValue.Text = message.Message;
            MessageBox.Show(message.Message, HostUiText.Window("剪貼簿同步", "Clipboard Sync"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.Equals(message.Operation, "get", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                WriteLocalClipboardText(message.Text);

                lblClipboardValue.Text = message.Truncated
                    ? HostUiText.Bi("已將遠端剪貼簿複製到本機（已截斷）。", "Remote clipboard copied locally (truncated).")
                    : HostUiText.Bi("已將遠端剪貼簿複製到本機。", "Remote clipboard copied locally.");
            }
            catch (Exception exception)
            {
                lblClipboardValue.Text = HostUiText.Bi($"剪貼簿更新失敗：{exception.Message}", $"Clipboard update failed: {exception.Message}");
                MessageBox.Show(
                    HostUiText.Bi($"已收到遠端剪貼簿，但無法複製到本機：{exception.Message}", $"Remote clipboard was received but could not be copied locally: {exception.Message}"),
                    HostUiText.Window("剪貼簿同步", "Clipboard Sync"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        lblClipboardValue.Text = message.Message;
    }

    private void btnUploadFile_Click(object sender, EventArgs e)
    {
        LogTransferTrace("host-upload-clicked", "Upload button was clicked.", new
        {
            deviceId = _device?.DeviceId,
            viewer = _viewer?.UserName
        });
        TryWriteFallbackTransferTrace("host-upload-click-dispatch", "Dispatching upload selection back onto the UI message loop.", new
        {
            deviceId = _device?.DeviceId,
            viewer = _viewer?.UserName,
            invokeRequired = InvokeRequired,
            uiThreadId = Environment.CurrentManagedThreadId
        });

        BeginInvoke(new Action(() =>
        {
            TryWriteFallbackTransferTrace("host-upload-dispatched", "Upload selection was dispatched from the click handler.", new
            {
                deviceId = _device?.DeviceId,
                viewer = _viewer?.UserName,
                uiThreadId = Environment.CurrentManagedThreadId
            });
            HandleUploadSelection();
        }));
    }

    protected void HandleUploadSelection()
    {
        try
        {
            TryWriteFallbackTransferTrace("host-upload-selection-entered", "Entered HandleUploadSelection.", new
            {
                deviceId = _device?.DeviceId,
                viewer = _viewer?.UserName,
                uiThreadId = Environment.CurrentManagedThreadId
            });
            LogTransferTrace("host-upload-permission-check", "Checking upload permission.", new
            {
                deviceId = _device?.DeviceId,
                viewer = _viewer?.UserName,
                canControlRemote = _sessionCanControl
            });

            if (!_sessionCanControl)
            {
                lblStatusValue.Text = HostUiText.Bi("僅觀看工作階段", "Observe-only session");
                lblTransferValue.Text = HostUiText.Bi("此帳號沒有傳送檔案的權限。", "This account does not have permission to transfer files.");
                LogTransferTrace("host-upload-blocked", "Upload request was blocked by permission check.", new
                {
                    deviceId = _device?.DeviceId,
                    viewer = _viewer?.UserName
                });
                return;
            }

            TryWriteFallbackTransferTrace("host-upload-dialog-open-fallback", "Opening file selection dialog via fallback marker.", new
            {
                deviceId = _device?.DeviceId,
                viewer = _viewer?.UserName,
                uiThreadId = Environment.CurrentManagedThreadId
            });
            LogTransferTrace("host-upload-dialog-open", "Opening file selection dialog.", new
            {
                deviceId = _device?.DeviceId,
                viewer = _viewer?.UserName
            });
            var filePath = SelectUploadFilePath(this);
            TryWriteFallbackTransferTrace("host-upload-dialog-closed-fallback", "File selection dialog returned via fallback marker.", new
            {
                deviceId = _device?.DeviceId,
                viewer = _viewer?.UserName,
                hasSelection = !string.IsNullOrWhiteSpace(filePath),
                filePath,
                uiThreadId = Environment.CurrentManagedThreadId
            });
            LogTransferTrace("host-upload-dialog-closed", "File selection dialog returned.", new
            {
                deviceId = _device?.DeviceId,
                viewer = _viewer?.UserName,
                hasSelection = !string.IsNullOrWhiteSpace(filePath),
                filePath
            });
            if (string.IsNullOrWhiteSpace(filePath))
            {
                LogTransferTrace("host-upload-cancelled", "Upload file selection was cancelled.", new
                {
                    deviceId = _device?.DeviceId
                });
                return;
            }

            LogTransferTrace("host-upload-selected", "Selected a local file for upload.", new
            {
                deviceId = _device?.DeviceId,
                filePath
            });
            _ = UploadFileWithGuardAsync(filePath);
        }
        catch (Exception exception)
        {
            LogTransferTrace("host-upload-selection-failed", "Upload selection flow failed before transfer started.", new
            {
                deviceId = _device?.DeviceId,
                viewer = _viewer?.UserName,
                exception = exception.ToString()
            });
            lblTransferValue.Text = HostUiText.Bi($"上傳初始化失敗：{exception.Message}", $"Upload initialization failed: {exception.Message}");
            lblTransferPathValue.Text = HostUiText.Bi("目的地：未建立。", "Destination: not created.");
            MessageBox.Show(
                HostUiText.Bi($"開啟檔案上傳流程失敗：{exception.Message}", $"Failed to open the upload flow: {exception.Message}"),
                HostUiText.Window("檔案傳輸", "File Transfer"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task UploadFileWithGuardAsync(string filePath)
    {
        try
        {
            await UploadFileAsync(filePath);
        }
        catch (Exception exception)
        {
            btnUploadFile.Enabled = _sessionCanControl;
            progressFileTransfer.Value = 0;
            lblTransferValue.Text = HostUiText.Bi($"上傳失敗：{exception.Message}", $"Upload failed: {exception.Message}");
            lblTransferPathValue.Text = HostUiText.Bi("目的地：未建立。", "Destination: not created.");
            MessageBox.Show(
                HostUiText.Bi($"檔案上傳流程發生未預期錯誤：{exception.Message}", $"The upload flow failed with an unexpected error: {exception.Message}"),
                HostUiText.Window("檔案傳輸", "File Transfer"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void btnDownloadFile_Click(object sender, EventArgs e)
    {
        LogTransferTrace("host-download-clicked", "Download button was clicked.", new
        {
            deviceId = _device?.DeviceId,
            viewer = _viewer?.UserName
        });

        BeginInvoke(new Action(async () =>
        {
            await HandleDownloadSelectionSafeAsync();
        }));
    }

    private void btnActions_Click(object sender, EventArgs e)
    {
        SyncActionMenuState();
        menuActions.Show(btnActions, new Point(0, btnActions.Height));
    }

    private void menuOpenTransferFolder_Click(object sender, EventArgs e)
    {
        HandleOpenTransferFolder();
    }

    private void menuSendClipboard_Click(object sender, EventArgs e)
    {
        btnSendClipboard_Click(sender, e);
    }

    private void menuGetClipboard_Click(object sender, EventArgs e)
    {
        btnGetClipboard_Click(sender, e);
    }

    private void menuUploadFile_Click(object sender, EventArgs e)
    {
        btnUploadFile_Click(sender, e);
    }

    private void menuDownloadFile_Click(object sender, EventArgs e)
    {
        btnDownloadFile_Click(sender, e);
    }

    private async void menuSecureAttention_Click(object sender, EventArgs e)
    {
        await HandleSecureAttentionAsync();
    }

    private void menuFullscreen_Click(object sender, EventArgs e)
    {
        btnFullscreen_Click(sender, e);
    }

    private async Task HandleDownloadSelectionSafeAsync()
    {
        try
        {
            await HandleDownloadSelectionAsync();
        }
        catch (Exception exception)
        {
            FailDownload(HostUiText.Bi($"下載初始化失敗：{exception.Message}", $"Download initialization failed: {exception.Message}"), showDialog: true);
        }
    }

    protected virtual async Task HandleDownloadSelectionAsync()
    {
        if (!EnsureInteractivePermission(HostUiText.Bi("此帳號沒有下載檔案的權限。", "This account does not have permission to download files.")))
        {
            return;
        }

        var suggestedRemotePath = !string.IsNullOrWhiteSpace(_lastUploadedFilePath)
            ? _lastUploadedFilePath
            : _lastDownloadFilePath;
        var remotePath = await SelectDownloadSourcePathAsync(this, suggestedRemotePath);
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return;
        }

        var localPath = SelectDownloadSavePath(this, Path.GetFileName(remotePath));
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return;
        }

        await StartDownloadAsync(remotePath, localPath);
    }

    private async Task HandleSecureAttentionAsync()
    {
        if (!EnsureInteractivePermission(HostUiText.Bi("此帳號沒有切換遠端登入畫面的權限。", "This account does not have permission to switch the remote device to the sign-in screen.")))
        {
            return;
        }

        var result = MessageBox.Show(
            HostUiText.Bi(
                "這會將遠端電腦切換到登入/鎖定畫面。Windows 不允許一般桌面程式直接模擬標準 Ctrl + Alt + Del，因此系統會改用安全的鎖定工作站方式處理。要繼續嗎？",
                "This will switch the remote computer to the sign-in or lock screen. Windows does not allow a normal desktop app to inject a standard Ctrl+Alt+Del sequence directly, so the system will use a secure workstation lock instead. Continue?"),
            HostUiText.Window("切換登入畫面", "Switch to Sign-in Screen"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        await SendViewerCommandCoreAsync(new ViewerCommandMessage
        {
            Type = "secure-attention"
        }, CancellationToken.None);

        lblStatusValue.Text = HostUiText.Bi("已要求遠端電腦切換到登入畫面。", "Requested the remote computer to switch to the sign-in screen.");
    }

    private async void btnSendClipboard_Click(object sender, EventArgs e)
    {
        await HandleSendClipboardSafeAsync();
    }

    private async Task HandleSendClipboardSafeAsync()
    {
        if (!EnsureInteractivePermission(HostUiText.Bi("此帳號可開啟 Viewer，但沒有同步剪貼簿的權限。", "This account can open viewer sessions but does not have permission to sync clipboard text.")))
        {
            return;
        }

        string text;
        try
        {
            text = ReadLocalClipboardText();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show(HostUiText.Bi("本機剪貼簿沒有純文字內容。", "Local clipboard does not contain plain text."), HostUiText.Window("剪貼簿同步", "Clipboard Sync"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(HostUiText.Bi($"讀取本機剪貼簿失敗：{exception.Message}", $"Failed to read local clipboard text: {exception.Message}"), HostUiText.Window("剪貼簿同步", "Clipboard Sync"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (text.Length > MaxClipboardCharacters)
        {
            MessageBox.Show(
                HostUiText.Bi($"剪貼簿文字過大，最多只支援 {MaxClipboardCharacters:N0} 個字元。", $"Clipboard text is too large. The maximum supported length is {MaxClipboardCharacters:N0} characters."),
                HostUiText.Window("剪貼簿同步", "Clipboard Sync"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _clipboardSyncSignal = new TaskCompletionSource<AgentClipboardMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        lblClipboardValue.Text = HostUiText.Bi("正在將本機剪貼簿傳送到遠端裝置...", "Sending local clipboard to the remote device...");
        btnSendClipboard.Enabled = false;
        btnGetClipboard.Enabled = false;

        try
        {
            await SendCommandAsync(new ViewerCommandMessage
            {
                Type = "clipboard-set",
                ClipboardText = text
            });

            var response = await _clipboardSyncSignal.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (string.Equals(response.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(response.Message);
            }
        }
        catch (Exception exception)
        {
            lblClipboardValue.Text = HostUiText.Bi($"剪貼簿同步失敗：{exception.Message}", $"Clipboard sync failed: {exception.Message}");
            MessageBox.Show(HostUiText.Bi($"同步剪貼簿失敗：{exception.Message}", $"Failed to sync clipboard text: {exception.Message}"), HostUiText.Window("剪貼簿同步", "Clipboard Sync"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _clipboardSyncSignal = null;
            btnSendClipboard.Enabled = _sessionCanControl;
            btnGetClipboard.Enabled = _sessionCanControl;
        }
    }

    private async void btnGetClipboard_Click(object sender, EventArgs e)
    {
        await HandleGetClipboardSafeAsync();
    }

    private async Task HandleGetClipboardSafeAsync()
    {
        if (!EnsureInteractivePermission(HostUiText.Bi("此帳號可開啟 Viewer，但沒有同步剪貼簿的權限。", "This account can open viewer sessions but does not have permission to sync clipboard text.")))
        {
            return;
        }

        _clipboardSyncSignal = new TaskCompletionSource<AgentClipboardMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        lblClipboardValue.Text = HostUiText.Bi("正在要求遠端剪貼簿內容...", "Requesting remote clipboard text...");
        btnSendClipboard.Enabled = false;
        btnGetClipboard.Enabled = false;

        try
        {
            await SendCommandAsync(new ViewerCommandMessage
            {
                Type = "clipboard-get"
            });

            var response = await _clipboardSyncSignal.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (string.Equals(response.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(response.Message);
            }
        }
        catch (Exception exception)
        {
            lblClipboardValue.Text = HostUiText.Bi($"剪貼簿同步失敗：{exception.Message}", $"Clipboard sync failed: {exception.Message}");
            MessageBox.Show(HostUiText.Bi($"取得遠端剪貼簿失敗：{exception.Message}", $"Failed to retrieve remote clipboard text: {exception.Message}"), HostUiText.Window("剪貼簿同步", "Clipboard Sync"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _clipboardSyncSignal = null;
            btnSendClipboard.Enabled = _sessionCanControl;
            btnGetClipboard.Enabled = _sessionCanControl;
        }
    }

    protected virtual async Task UploadFileAsync(string filePath)
    {
        if (_device is null)
        {
            return;
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            MessageBox.Show(HostUiText.Bi("所選檔案已不存在。", "The selected file no longer exists."), HostUiText.Window("檔案傳輸", "File Transfer"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var uploadId = Guid.NewGuid().ToString("N");
        _activeUploadId = uploadId;
        _uploadStartSignal = new TaskCompletionSource<AgentFileTransferStatusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _uploadCompletionSignal = new TaskCompletionSource<AgentFileTransferStatusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _lastUploadedFilePath = null;
        btnOpenTransferFolder.Enabled = false;
        btnUploadFile.Enabled = false;
        btnDownloadFile.Enabled = false;
        progressFileTransfer.Value = 0;
        SetTransferPanelVisible(true);
        lblTransferValue.Text = HostUiText.Bi($"正在上傳 {fileInfo.Name}...", $"Uploading {fileInfo.Name}...");
        lblTransferPathValue.Text = HostUiText.Bi("目的地：等待 Agent 建立檔案。", "Destination: waiting for Agent to create the file.");
        LogTransferTrace("host-upload-started", "Started Host-side upload workflow.", new
        {
            deviceId = _device?.DeviceId,
            uploadId,
            fileName = fileInfo.FullName,
            fileSize = fileInfo.Length
        });

        try
        {
            await UploadFileCoreAsync(fileInfo, uploadId);

            var completionTimeout = TimeSpan.FromSeconds(Math.Clamp(30 + (int)Math.Ceiling(fileInfo.Length / (1024d * 1024d * 4d)), 30, 180));
            var completionStatus = await _uploadCompletionSignal!.Task.WaitAsync(completionTimeout);
            if (string.Equals(completionStatus.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(completionStatus.Message);
            }

            LogTransferTrace("host-upload-completed", "Host-side upload workflow completed.", new
            {
                deviceId = _device?.DeviceId,
                uploadId,
                fileName = fileInfo.FullName,
                storedFilePath = completionStatus.StoredFilePath
            });
        }
        catch (Exception exception)
        {
            LogTransferTrace("host-upload-failed", $"Host-side upload workflow failed: {exception.Message}", new
            {
                deviceId = _device?.DeviceId,
                uploadId,
                fileName = fileInfo.FullName,
                exception = exception.ToString()
            });
            var shouldShowDialog = !string.IsNullOrWhiteSpace(_activeUploadId);
            await TryAbortUploadAsync(uploadId);
            _activeUploadId = null;
            btnUploadFile.Enabled = _sessionCanControl;
            btnDownloadFile.Enabled = _sessionCanControl;
            progressFileTransfer.Value = 0;
            lblTransferValue.Text = HostUiText.Bi($"上傳失敗：{exception.Message}", $"Upload failed: {exception.Message}");
            lblTransferPathValue.Text = HostUiText.Bi("目的地：未建立。", "Destination: not created.");
            if (shouldShowDialog)
            {
                MessageBox.Show(HostUiText.Bi($"檔案上傳失敗：{exception.Message}", $"Failed to upload file: {exception.Message}"), HostUiText.Window("檔案傳輸", "File Transfer"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _uploadStartSignal = null;
            _uploadCompletionSignal = null;
        }
    }

    protected virtual async Task UploadFileCoreAsync(FileInfo fileInfo, string uploadId)
    {
        await SendViewerCommandCoreAsync(new ViewerCommandMessage
        {
            Type = "file-upload-start",
            UploadId = uploadId,
            FileName = fileInfo.Name,
            FileSize = fileInfo.Length
        }, CancellationToken.None).ConfigureAwait(false);
        LogTransferTrace("host-upload-command-start", "Sent file-upload-start command to Agent.", new
        {
            deviceId = _device?.DeviceId,
            uploadId,
            fileName = fileInfo.FullName,
            fileSize = fileInfo.Length
        });

        var startStatus = await _uploadStartSignal!.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        if (string.Equals(startStatus.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(startStatus.Message);
        }

        await using var stream = fileInfo.OpenRead();
        var buffer = ArrayPool<byte>.Shared.Rent(UploadChunkBytes);
        try
        {
            var sequenceNumber = 0;
            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, UploadChunkBytes)).ConfigureAwait(false);
                if (bytesRead <= 0)
                {
                    break;
                }

                await SendViewerCommandCoreAsync(new ViewerCommandMessage
                {
                    Type = "file-upload-chunk",
                    UploadId = uploadId,
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    SequenceNumber = sequenceNumber++,
                    ChunkBase64 = Convert.ToBase64String(buffer, 0, bytesRead)
                }, CancellationToken.None).ConfigureAwait(false);

                if (sequenceNumber == 1 || sequenceNumber % 32 == 0)
                {
                    LogTransferTrace("host-upload-command-chunk", "Sent upload chunk to Agent.", new
                    {
                        deviceId = _device?.DeviceId,
                        uploadId,
                        sequenceNumber,
                        bytesRead,
                        fileSize = fileInfo.Length
                    });
                }

                if (sequenceNumber % 8 == 0)
                {
                    await Task.Delay(1).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        await SendViewerCommandCoreAsync(new ViewerCommandMessage
        {
            Type = "file-upload-complete",
            UploadId = uploadId,
            FileName = fileInfo.Name,
            FileSize = fileInfo.Length
        }, CancellationToken.None).ConfigureAwait(false);
        LogTransferTrace("host-upload-command-complete", "Sent file-upload-complete command to Agent.", new
        {
            deviceId = _device?.DeviceId,
            uploadId,
            fileName = fileInfo.FullName,
            fileSize = fileInfo.Length
        });

        await UpdateUiAsync(() =>
        {
            lblTransferValue.Text = HostUiText.Bi($"等待 Agent 完成 {fileInfo.Name} 的檔案寫入...", $"Waiting for Agent to finalize {fileInfo.Name}...");
            lblTransferPathValue.Text = HostUiText.Bi("目的地：檔案已建立，等待完成寫入。", "Destination: file created, waiting for finalization.");
        }).ConfigureAwait(false);
    }

    protected virtual async Task StartDownloadAsync(string remotePath, string localPath)
    {
        if (_device is null)
        {
            return;
        }

        var transferId = Guid.NewGuid().ToString("N");
        _activeDownloadId = transferId;
        _lastDownloadFilePath = null;
        _downloadTargetPath = localPath;
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        _downloadTempPath = CreateDownloadTempPath(localPath);
        _downloadStream = new FileStream(_downloadTempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);

        SetTransferPanelVisible(true);
        progressFileTransfer.Value = 0;
        lblTransferValue.Text = HostUiText.Bi($"正在要求下載 {Path.GetFileName(remotePath)}...", $"Requesting download for {Path.GetFileName(remotePath)}...");
        lblTransferPathValue.Text = HostUiText.Bi($"來源：{remotePath}  目的地：{localPath}", $"Source: {remotePath}  Destination: {localPath}");
        btnUploadFile.Enabled = false;
        btnDownloadFile.Enabled = false;
        btnOpenTransferFolder.Enabled = false;

        LogTransferTrace("host-download-started", "Started Host-side download workflow.", new
        {
            deviceId = _device.DeviceId,
            transferId,
            remotePath,
            localPath
        });

        try
        {
            await ExecuteDownloadAsync(transferId, remotePath);
        }
        catch (Exception exception)
        {
            FailDownload(HostUiText.Bi($"檔案下載失敗：{exception.Message}", $"File download failed: {exception.Message}"), showDialog: true);
        }
    }

    protected virtual Task ExecuteDownloadAsync(string transferId, string remotePath)
    {
        return SendViewerCommandCoreAsync(new ViewerCommandMessage
        {
            Type = "file-download-start",
            UploadId = transferId,
            RemotePath = remotePath
        }, CancellationToken.None);
    }

    private async Task<RemoteDirectoryListingResult> BrowseRemoteDirectoryAsync(string? directoryPath, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var signal = new TaskCompletionSource<AgentFileTransferStatusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_browseDirectorySignals.TryAdd(requestId, signal))
        {
            throw new InvalidOperationException(HostUiText.Bi("無法建立遠端檔案總管要求。", "Could not create the remote file browser request."));
        }

        try
        {
            await SendViewerCommandCoreAsync(new ViewerCommandMessage
            {
                Type = "file-browser-list",
                UploadId = requestId,
                DirectoryPath = directoryPath
            }, cancellationToken);

            var status = await signal.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            if (!string.Equals(status.Status, "listed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(status.Message);
            }

            return new RemoteDirectoryListingResult
            {
                DirectoryPath = status.DirectoryPath,
                ParentDirectoryPath = status.ParentDirectoryPath,
                CanNavigateUp = status.CanNavigateUp,
                EntriesTruncated = status.EntriesTruncated,
                Message = status.Message,
                RootPaths = status.RootPaths,
                Entries = status.Entries
            };
        }
        finally
        {
            _browseDirectorySignals.TryRemove(requestId, out _);
        }
    }

    private async Task<RemoteMoveResult> MoveRemoteEntryAsync(string sourcePath, string destinationDirectoryPath, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var signal = new TaskCompletionSource<AgentFileTransferStatusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_moveEntrySignals.TryAdd(requestId, signal))
        {
            throw new InvalidOperationException(HostUiText.Bi("無法建立遠端搬移要求。", "Could not create the remote move request."));
        }

        try
        {
            await SendViewerCommandCoreAsync(new ViewerCommandMessage
            {
                Type = "file-move",
                UploadId = requestId,
                RemotePath = sourcePath,
                DestinationPath = destinationDirectoryPath
            }, cancellationToken);

            var status = await signal.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            if (!string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(status.Message);
            }

            return new RemoteMoveResult
            {
                DestinationPath = status.StoredFilePath,
                Message = status.Message
            };
        }
        finally
        {
            _moveEntrySignals.TryRemove(requestId, out _);
        }
    }

    private async Task TryAbortUploadAsync(string uploadId)
    {
        try
        {
            LogTransferTrace("host-upload-command-abort", "Sent file-upload-abort command to Agent.", new
            {
                deviceId = _device?.DeviceId,
                uploadId
            });
            await SendCommandAsync(new ViewerCommandMessage
            {
                Type = "file-upload-abort",
                UploadId = uploadId
            }, showFailureDialog: false);
        }
        catch
        {
        }
    }

    private async void pictureStream_MouseDown(object sender, MouseEventArgs e)
    {
        if (!TryGetRelativePoint(e.Location, out var x, out var y))
        {
            return;
        }

        pictureStream.Focus();
        await SendCommandAsync(new ViewerCommandMessage
        {
            Type = "mousedown",
            X = x,
            Y = y,
            Button = TranslateMouseButton(e.Button)
        });
    }

    private async void pictureStream_MouseUp(object sender, MouseEventArgs e)
    {
        if (!TryGetRelativePoint(e.Location, out var x, out var y))
        {
            x = 0.5;
            y = 0.5;
        }

        await SendCommandAsync(new ViewerCommandMessage
        {
            Type = "mouseup",
            X = x,
            Y = y,
            Button = TranslateMouseButton(e.Button)
        });
    }

    private async void pictureStream_MouseMove(object sender, MouseEventArgs e)
    {
        var now = Environment.TickCount64;
        if (now - _lastMoveAt < 33)
        {
            return;
        }

        if (!TryGetRelativePoint(e.Location, out var x, out var y))
        {
            return;
        }

        _lastMoveAt = now;
        await SendCommandAsync(new ViewerCommandMessage
        {
            Type = "move",
            X = x,
            Y = y
        });
    }

    private async void pictureStream_MouseWheel(object sender, MouseEventArgs e)
    {
        if (!TryGetRelativePoint(e.Location, out var x, out var y))
        {
            x = 0.5;
            y = 0.5;
        }

        await SendCommandAsync(new ViewerCommandMessage
        {
            Type = "wheel",
            X = x,
            Y = y,
            DeltaY = e.Delta
        });
    }

    private async void RemoteViewerForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F11)
        {
            e.SuppressKeyPress = true;
            ToggleFullscreen();
            return;
        }

        if (e.KeyCode == Keys.Escape && _isFullscreen)
        {
            e.SuppressKeyPress = true;
            ToggleFullscreen(forceExit: true);
            return;
        }

        if (IsPlainPrintableKey(e))
        {
            return;
        }

        if (!TryTranslateKeyCode(e.KeyCode, out var code))
        {
            return;
        }

        e.SuppressKeyPress = true;
        await SendCommandAsync(new ViewerCommandMessage
        {
            Type = "keydown",
            Code = code,
            Key = e.KeyCode.ToString()
        });
    }

    private async void RemoteViewerForm_KeyUp(object sender, KeyEventArgs e)
    {
        if (IsPlainPrintableKey(e))
        {
            return;
        }

        if (!TryTranslateKeyCode(e.KeyCode, out var code))
        {
            return;
        }

        e.SuppressKeyPress = true;
        await SendCommandAsync(new ViewerCommandMessage
        {
            Type = "keyup",
            Code = code,
            Key = e.KeyCode.ToString()
        });
    }

    private async void RemoteViewerForm_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar))
        {
            return;
        }

        await SendCommandAsync(new ViewerCommandMessage
        {
            Type = "text",
            Key = e.KeyChar.ToString()
        });
    }

    private void ConfigureZoomOptions()
    {
        if (cboZoom.Items.Count == ZoomPresets.Length)
        {
            UpdateZoomSelection();
            return;
        }

        _suppressZoomSelectionChanged = true;
        try
        {
            cboZoom.Items.Clear();
            foreach (var preset in ZoomPresets)
            {
                cboZoom.Items.Add(FormatZoomPreset(preset.Factor));
            }

            cboZoom.SelectedIndex = 0;
        }
        finally
        {
            _suppressZoomSelectionChanged = false;
        }
    }

    private void ReceiveDownloadChunk(AgentFileTransferStatusMessage status)
    {
        if (_downloadStream is null || string.IsNullOrWhiteSpace(status.ChunkBase64))
        {
            return;
        }

        try
        {
            var chunk = Convert.FromBase64String(status.ChunkBase64);
            _downloadStream.Write(chunk, 0, chunk.Length);
        }
        catch (Exception exception)
        {
            FailDownload(HostUiText.Bi($"寫入下載檔案失敗：{exception.Message}", $"Failed to write the downloaded file: {exception.Message}"));
        }
    }

    private void FinalizeDownload(AgentFileTransferStatusMessage status)
    {
        try
        {
            _downloadStream?.Flush();
            _downloadStream?.Dispose();
            _downloadStream = null;

            if (string.IsNullOrWhiteSpace(_downloadTargetPath) || string.IsNullOrWhiteSpace(_downloadTempPath))
            {
                throw new InvalidOperationException(HostUiText.Bi("本機下載目的地不存在。", "The local download target was not prepared."));
            }

            var resolvedTargetPath = ResolveDownloadTargetPath(_downloadTargetPath);
            File.Move(_downloadTempPath, resolvedTargetPath, overwrite: false);
            _lastDownloadFilePath = resolvedTargetPath;
            _downloadTargetPath = resolvedTargetPath;
            _activeDownloadId = null;
            btnUploadFile.Enabled = _sessionCanControl;
            btnDownloadFile.Enabled = _sessionCanControl;
            btnOpenTransferFolder.Enabled = true;
            UpdateTransferProgress(status.FileSize, status.FileSize);
            lblTransferValue.Text = status.Message;
            lblTransferPathValue.Text = HostUiText.Bi($"已下載到：{_downloadTargetPath}", $"Downloaded to: {_downloadTargetPath}");
        }
        catch (Exception exception)
        {
            FailDownload(HostUiText.Bi($"無法完成本機下載：{exception.Message}", $"Could not finalize the local download: {exception.Message}"), showDialog: true);
        }
    }

    private void FailDownload(string message, bool showDialog = false)
    {
        try
        {
            _downloadStream?.Dispose();
        }
        catch
        {
        }

        _downloadStream = null;
        TryDeleteFile(_downloadTempPath);
        _downloadTempPath = null;
        _downloadTargetPath = null;
        _activeDownloadId = null;
        _lastDownloadFilePath = null;
        btnUploadFile.Enabled = _sessionCanControl;
        btnDownloadFile.Enabled = _sessionCanControl;
        btnOpenTransferFolder.Enabled = false;
        progressFileTransfer.Value = 0;
        SetTransferPanelVisible(true);
        lblTransferValue.Text = message;
        lblTransferPathValue.Text = HostUiText.Bi("目的地：未建立。", "Destination: not created.");

        if (showDialog)
        {
            MessageBox.Show(message, HostUiText.Window("檔案傳輸", "File Transfer"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void cboZoom_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_suppressZoomSelectionChanged)
        {
            return;
        }

        if (cboZoom.SelectedIndex < 0 || cboZoom.SelectedIndex >= ZoomPresets.Length)
        {
            return;
        }

        var preset = ZoomPresets[cboZoom.SelectedIndex];
        if (preset.Factor is null)
        {
            _fitToWindow = true;
        }
        else
        {
            _fitToWindow = false;
            _manualZoomFactor = preset.Factor.Value;
        }

        ApplyPictureLayout();
    }

    private void panelViewer_Resize(object sender, EventArgs e)
    {
        ApplyPictureLayout();
    }

    private void pictureStream_DoubleClick(object sender, EventArgs e)
    {
        ToggleFullscreen();
    }

    private void btnFullscreen_Click(object sender, EventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen(bool forceExit = false)
    {
        if (forceExit && !_isFullscreen)
        {
            return;
        }

        if (!_isFullscreen)
        {
            _restoreBorderStyle = FormBorderStyle;
            _restoreWindowState = WindowState;
            _restoreBounds = Bounds;
            _restoreTopMost = TopMost;

            _isFullscreen = true;
            panelTop.Visible = false;
            layoutRoot.RowStyles[0].Height = 0;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            TopMost = true;
        }
        else
        {
            _isFullscreen = false;
            TopMost = _restoreTopMost;
            FormBorderStyle = _restoreBorderStyle;
            panelTop.Visible = true;
            layoutRoot.RowStyles[0].Height = _transferPanelVisible ? TopPanelExpandedHeight : TopPanelCollapsedHeight;
            Bounds = _restoreBounds;
            WindowState = _restoreWindowState;
        }

        btnFullscreen.Text = _isFullscreen
            ? HostUiText.Bi("離開全螢幕", "Exit Fullscreen")
            : HostUiText.Bi("全螢幕", "Fullscreen");
        ApplyPictureLayout();
        pictureStream.Focus();
    }

    private void UpdateZoomSelection()
    {
        if (cboZoom.Items.Count == 0)
        {
            return;
        }

        var selectedKey = _fitToWindow ? "fit" : ((int)Math.Round(_manualZoomFactor * 100d)).ToString();
        var selectedIndex = -1;
        for (var index = 0; index < ZoomPresets.Length; index++)
        {
            if (ZoomPresets[index].Key == selectedKey)
            {
                selectedIndex = index;
                break;
            }
        }

        if (selectedIndex < 0)
        {
            return;
        }

        if (cboZoom.SelectedIndex == selectedIndex || cboZoom.DroppedDown)
        {
            return;
        }

        _suppressZoomSelectionChanged = true;
        try
        {
            cboZoom.SelectedIndex = selectedIndex;
        }
        finally
        {
            _suppressZoomSelectionChanged = false;
        }
    }

    private void ApplyPictureLayout()
    {
        if (_fitToWindow)
        {
            panelViewer.AutoScroll = false;
            panelViewer.AutoScrollMinSize = Size.Empty;
            pictureStream.Dock = DockStyle.Fill;
            pictureStream.SizeMode = PictureBoxSizeMode.Zoom;
            pictureStream.Location = Point.Empty;
            pictureStream.Size = panelViewer.ClientSize;
            UpdateZoomSelection();
            return;
        }

        if (_frameSize.Width <= 0 || _frameSize.Height <= 0)
        {
            return;
        }

        panelViewer.AutoScroll = true;
        pictureStream.Dock = DockStyle.None;
        pictureStream.SizeMode = PictureBoxSizeMode.StretchImage;

        var scaledWidth = Math.Max(1, (int)Math.Round(_frameSize.Width * _manualZoomFactor));
        var scaledHeight = Math.Max(1, (int)Math.Round(_frameSize.Height * _manualZoomFactor));
        pictureStream.Size = new Size(scaledWidth, scaledHeight);

        if (scaledWidth <= panelViewer.ClientSize.Width && scaledHeight <= panelViewer.ClientSize.Height)
        {
            panelViewer.AutoScrollMinSize = Size.Empty;
            pictureStream.Location = new Point(
                Math.Max(0, (panelViewer.ClientSize.Width - scaledWidth) / 2),
                Math.Max(0, (panelViewer.ClientSize.Height - scaledHeight) / 2));
        }
        else
        {
            panelViewer.AutoScrollMinSize = pictureStream.Size;
            pictureStream.Location = Point.Empty;
        }

        UpdateZoomSelection();
    }

    private static string FormatZoomPreset(double? factor)
    {
        return factor is null ? HostUiText.Bi("符合視窗", "Fit to Window") : $"{Math.Round(factor.Value * 100d):0}%";
    }

    private void btnFocusRemote_Click(object sender, EventArgs e)
    {
        panelViewer.Focus();
        pictureStream.Focus();
    }

    private async void btnDisconnect_Click(object sender, EventArgs e)
    {
        if (_viewerSessionBroker is not null && _device is not null)
        {
            await _viewerSessionBroker.DetachViewerAsync(_device.DeviceId, CancellationToken.None);
            _attached = false;
        }

        Close();
    }

    private bool TryGetRelativePoint(Point location, out double x, out double y)
    {
        x = 0d;
        y = 0d;

        if (_frameSize.Width <= 0 || _frameSize.Height <= 0 || pictureStream.ClientSize.Width <= 0 || pictureStream.ClientSize.Height <= 0)
        {
            return false;
        }

        var ratio = Math.Min(
            pictureStream.ClientSize.Width / (double)_frameSize.Width,
            pictureStream.ClientSize.Height / (double)_frameSize.Height);

        var renderedWidth = _frameSize.Width * ratio;
        var renderedHeight = _frameSize.Height * ratio;
        var left = (pictureStream.ClientSize.Width - renderedWidth) / 2d;
        var top = (pictureStream.ClientSize.Height - renderedHeight) / 2d;

        if (location.X < left || location.Y < top || location.X > left + renderedWidth || location.Y > top + renderedHeight)
        {
            return false;
        }

        x = (location.X - left) / renderedWidth;
        y = (location.Y - top) / renderedHeight;
        return true;
    }

    private async Task SendCommandAsync(ViewerCommandMessage command, bool showFailureDialog = true)
    {
        if (!_attached || _viewerSessionBroker is null || _device is null)
        {
            return;
        }

        if (!EnsureInteractivePermission(HostUiText.Bi("此帳號可開啟 Viewer，但沒有傳送遠端控制或檔案的權限。", "This account can open viewer sessions but does not have permission to send remote control input or transfer files.")))
        {
            return;
        }

        try
        {
            await _viewerSessionBroker.ForwardViewerCommandAsync(_device.DeviceId, command, CancellationToken.None);
            _commandFailed = false;
        }
        catch (Exception exception)
        {
            lblStatusValue.Text = HostUiText.Bi("指令傳送失敗", "Command delivery failed");
            if (_commandFailed || !showFailureDialog)
            {
                return;
            }

            _commandFailed = true;
            MessageBox.Show(
                HostUiText.Bi($"傳送遠端控制指令失敗：{exception.Message}", $"Failed to send remote control command: {exception.Message}"),
                HostUiText.Window("遠端檢視", "Remote Viewer"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private Task SendViewerCommandCoreAsync(ViewerCommandMessage command, CancellationToken cancellationToken)
    {
        if (!_attached || _viewerSessionBroker is null || _device is null)
        {
            throw new InvalidOperationException(HostUiText.Bi("Viewer 尚未連線到遠端裝置。", "The viewer is not connected to a remote device."));
        }

        return _viewerSessionBroker.ForwardViewerCommandAsync(_device.DeviceId, command, cancellationToken);
    }

    protected virtual string? SelectUploadFilePath(IWin32Window owner)
    {
        string? selectedFilePath = null;
        Exception? selectionException = null;
        var ownerHandle = ResolveOwnerHandle(owner);
        using var completed = new ManualResetEventSlim(false);

        var dialogThread = new Thread(() =>
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Title = HostUiText.Window("選擇要上傳的檔案", "Select a file to upload"),
                    CheckFileExists = true,
                    Multiselect = false,
                    RestoreDirectory = true,
                    AutoUpgradeEnabled = true
                };

                var result = ownerHandle != IntPtr.Zero
                    ? dialog.ShowDialog(new DialogOwnerWindow(ownerHandle))
                    : dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    selectedFilePath = dialog.FileName;
                }
            }
            catch (Exception exception)
            {
                selectionException = exception;
            }
            finally
            {
                completed.Set();
            }
        })
        {
            IsBackground = true,
            Name = "RemoteViewerUploadFileDialog"
        };

        dialogThread.SetApartmentState(ApartmentState.STA);
        dialogThread.Start();
        completed.Wait();
        dialogThread.Join();

        if (selectionException is not null)
        {
            throw new InvalidOperationException(HostUiText.Bi($"開啟檔案選擇器失敗：{selectionException.Message}", $"Failed to open the file picker: {selectionException.Message}"), selectionException);
        }

        return selectedFilePath;
    }

    protected virtual Task<string?> SelectDownloadSourcePathAsync(IWin32Window owner, string? suggestedRemotePath)
    {
        using var dialog = new RemoteFileBrowserForm(suggestedRemotePath, BrowseRemoteDirectoryAsync, MoveRemoteEntryAsync);
        return Task.FromResult(dialog.ShowDialog(owner) == DialogResult.OK
            ? dialog.SelectedFilePath
            : null);
    }

    protected virtual string? SelectDownloadSavePath(IWin32Window owner, string suggestedFileName)
    {
        if (Application.MessageLoop && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            using var dialog = BuildDownloadSaveDialog(suggestedFileName);
            var result = owner is not null
                ? dialog.ShowDialog(owner)
                : dialog.ShowDialog();
            return result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName)
                ? dialog.FileName
                : null;
        }

        string? selectedFilePath = null;
        Exception? selectionException = null;
        using var completed = new ManualResetEventSlim(false);

        var dialogThread = new Thread(() =>
        {
            try
            {
                using var dialog = BuildDownloadSaveDialog(suggestedFileName);
                var result = dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    selectedFilePath = dialog.FileName;
                }
            }
            catch (Exception exception)
            {
                selectionException = exception;
            }
            finally
            {
                completed.Set();
            }
        })
        {
            IsBackground = true,
            Name = "RemoteViewerDownloadSaveDialog"
        };

        dialogThread.SetApartmentState(ApartmentState.STA);
        dialogThread.Start();
        completed.Wait();
        dialogThread.Join();

        if (selectionException is not null)
        {
            throw new InvalidOperationException(HostUiText.Bi($"開啟下載目的地選擇器失敗：{selectionException.Message}", $"Failed to open the save dialog: {selectionException.Message}"), selectionException);
        }

        return selectedFilePath;
    }

    private static SaveFileDialog BuildDownloadSaveDialog(string suggestedFileName)
    {
        return new SaveFileDialog
        {
            Title = HostUiText.Window("選擇下載目的地", "Choose where to save the download"),
            FileName = string.IsNullOrWhiteSpace(suggestedFileName) ? "download.bin" : suggestedFileName,
            RestoreDirectory = true,
            AddExtension = true,
            OverwritePrompt = true,
            AutoUpgradeEnabled = true
        };
    }

    private static IntPtr ResolveOwnerHandle(IWin32Window owner)
    {
        try
        {
            return owner?.Handle ?? IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private sealed class DialogOwnerWindow : IWin32Window
    {
        public DialogOwnerWindow(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }

    protected virtual void OpenTransferFolder(string directoryPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{directoryPath}\"",
            UseShellExecute = true
        });
    }

    private void btnOpenTransferFolder_Click(object sender, EventArgs e)
    {
        HandleOpenTransferFolder();
    }

    private async void menuTakeControl_Click(object sender, EventArgs e)
    {
        await HandleTakeControlAsync();
    }

    private async Task HandleTakeControlAsync()
    {
        if (_viewerSessionBroker is null || _device is null || _viewer is null)
        {
            return;
        }

        if (_sessionCanControl)
        {
            lblStatusValue.Text = HostUiText.Bi("目前已擁有控制權。", "This viewer already has control.");
            return;
        }

        if (!_viewer.CanControlRemote)
        {
            MessageBox.Show(
                HostUiText.Bi("此帳號沒有接管遠端控制權的權限。", "This account is not allowed to take remote control."),
                HostUiText.Window("遠端檢視", "Remote Viewer"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var prompt = string.IsNullOrWhiteSpace(_controllerDisplayName)
            ? HostUiText.Bi("要接管這台裝置的控制權嗎？", "Do you want to take control of this device?")
            : HostUiText.Bi($"目前由「{_controllerDisplayName}」控制，要強制接管嗎？", $"This device is currently controlled by '{_controllerDisplayName}'. Do you want to force takeover?");
        var result = MessageBox.Show(
            prompt,
            HostUiText.Window("接管控制權", "Take Control"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        lblStatusValue.Text = HostUiText.Bi("正在請求控制權...", "Requesting control...");
        var state = await _viewerSessionBroker.RequestControlAsync(_device.DeviceId, forceTakeover: true, CancellationToken.None);
        if (state.CanControl != _sessionCanControl)
        {
            ApplySessionState(state, showNotification: !string.IsNullOrWhiteSpace(state.Message));
        }
        else if (!string.IsNullOrWhiteSpace(state.Message))
        {
            lblStatusValue.Text = state.Message;
        }
    }

    protected void HandleOpenTransferFolder()
    {
        var filePath = _lastDownloadFilePath ?? _lastUploadedFilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            MessageBox.Show(
                HostUiText.Bi("目前沒有可開啟的上傳目的地。", "There is no upload destination to open."),
                HostUiText.Window("檔案傳輸", "File Transfer"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            MessageBox.Show(
                HostUiText.Bi($"找不到上傳資料夾：{filePath}", $"Upload folder was not found: {filePath}"),
                HostUiText.Window("檔案傳輸", "File Transfer"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            OpenTransferFolder(directoryPath);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                HostUiText.Bi($"無法開啟資料夾：{exception.Message}", $"Failed to open folder: {exception.Message}"),
                HostUiText.Window("檔案傳輸", "File Transfer"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SetTransferPanelVisible(bool visible)
    {
        _transferPanelVisible = visible;
        lblTransferCaption.Visible = visible;
        lblTransferValue.Visible = visible;
        lblTransferPathValue.Visible = visible;
        progressFileTransfer.Visible = visible;
        btnOpenTransferFolder.Visible = false;

        if (!_isFullscreen)
        {
            layoutRoot.RowStyles[0].Height = visible ? TopPanelExpandedHeight : TopPanelCollapsedHeight;
        }

        SyncActionMenuState();
    }

    private void SyncActionMenuState()
    {
        menuOpenTransferFolder.Enabled = btnOpenTransferFolder.Enabled;
        menuSendClipboard.Enabled = btnSendClipboard.Enabled;
        menuGetClipboard.Enabled = btnGetClipboard.Enabled;
        menuUploadFile.Enabled = btnUploadFile.Enabled;
        menuDownloadFile.Enabled = btnDownloadFile.Enabled;
        menuTakeControl.Visible = _viewer?.CanControlRemote == true;
        menuTakeControl.Enabled = _viewer?.CanControlRemote == true && !_sessionCanControl;
        menuSecureAttention.Visible = true;
        menuSecureAttention.Enabled = _sessionCanControl;
        menuFocusRemote.Enabled = true;
        menuDisconnect.Enabled = true;
        menuFullscreen.Text = _isFullscreen
            ? HostUiText.Bi("離開全螢幕", "Exit Fullscreen")
            : HostUiText.Bi("全螢幕", "Fullscreen");
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static string CreateDownloadTempPath(string localPath)
    {
        var directory = Path.GetDirectoryName(localPath)!;
        var baseName = Path.GetFileName(localPath);
        return Path.Combine(directory, $".{baseName}.{Guid.NewGuid():N}.downloading");
    }

    private static string ResolveDownloadTargetPath(string requestedPath)
    {
        if (!File.Exists(requestedPath))
        {
            return requestedPath;
        }

        if (TryDeleteFileIfAvailable(requestedPath))
        {
            return requestedPath;
        }

        var directory = Path.GetDirectoryName(requestedPath)!;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(requestedPath);
        var extension = Path.GetExtension(requestedPath);
        for (var index = 1; index <= 99; index++)
        {
            var candidatePath = Path.Combine(directory, $"{fileNameWithoutExtension} ({index}){extension}");
            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }

            if (TryDeleteFileIfAvailable(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new IOException(HostUiText.Bi(
            "本機下載目的地與自動遞補檔名都被占用，無法完成下載。請關閉占用中的檔案後再試一次。",
            "The local download target and fallback file names are all in use. Close the files in use and try again."));
    }

    private static bool TryDeleteFileIfAvailable(string path)
    {
        try
        {
            File.Delete(path);
            return !File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private void LogTransferTrace(string eventName, string message, object? data = null)
    {
        var service = _fileTransferTraceService;
        if (service is not null)
        {
            _ = service.WriteAsync(eventName, message, data);
            return;
        }

        TryWriteFallbackTransferTrace(eventName, message, data);
    }

    private static void TryWriteFallbackTransferTrace(string eventName, string message, object? data)
    {
        try
        {
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "host-file-transfer.ndjson");
            var entry = new
            {
                occurredAt = DateTimeOffset.Now,
                eventName,
                message,
                data
            };

            var json = JsonSerializer.Serialize(entry, TraceJsonOptions) + Environment.NewLine;
            File.AppendAllText(logPath, json);
        }
        catch
        {
        }
    }

    private static string ReadLocalClipboardText()
    {
        return RunStaClipboard(() =>
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        });
    }

    private static void WriteLocalClipboardText(string? text)
    {
        RunStaClipboard(() =>
        {
            if (string.IsNullOrEmpty(text))
            {
                Clipboard.Clear();
            }
            else
            {
                Clipboard.SetText(text);
            }

            return 0;
        });
    }

    private static T RunStaClipboard<T>(Func<T> action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return action();
        }

        T? result = default;
        Exception? captured = null;
        using var completed = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        completed.Wait();

        if (captured is not null)
        {
            throw captured;
        }

        return result!;
    }

    private Task UpdateUiAsync(Action action)
    {
        if (IsDisposed || Disposing)
        {
            return Task.CompletedTask;
        }

        if (!InvokeRequired)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || Disposing)
                {
                    completion.TrySetResult();
                    return;
                }

                try
                {
                    action();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }));
        }
        catch (InvalidOperationException)
        {
            completion.TrySetResult();
        }

        return completion.Task;
    }

    private bool EnsureInteractivePermission(string message)
    {
        if (_sessionCanControl)
        {
            return true;
        }

        lblStatusValue.Text = HostUiText.Bi("僅觀看工作階段", "Observe-only session");
        if (_observeOnlyNoticeShown)
        {
            return false;
        }

        _observeOnlyNoticeShown = true;
        MessageBox.Show(message, HostUiText.Window("權限限制", "Permission"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private void UpdateTransferProgress(long bytesTransferred, long fileSize)
    {
        if (fileSize <= 0)
        {
            progressFileTransfer.Value = 0;
            return;
        }

        var percent = (int)Math.Round(Math.Clamp(bytesTransferred / (double)fileSize, 0d, 1d) * 100d);
        progressFileTransfer.Value = Math.Clamp(percent, progressFileTransfer.Minimum, progressFileTransfer.Maximum);
    }

    private void InitializeUiText()
    {
        Text = AppBuildInfo.AppendToWindowTitle(HostUiText.Window("遠端檢視", "Remote Viewer"));
        lblDeviceCaption.Text = HostUiText.Bi("裝置", "Device");
        lblHostCaption.Text = HostUiText.Bi("主機", "Host");
        lblResolutionCaption.Text = HostUiText.Bi("解析度", "Resolution");
        lblStatusCaption.Text = HostUiText.Bi("狀態", "Status");
        lblStatusValue.Text = HostUiText.Bi("閒置", "Idle");
        lblClipboardCaption.Text = HostUiText.Bi("剪貼簿", "Clipboard");
        lblClipboardValue.Text = HostUiText.Bi("尚未執行剪貼簿操作。", "No clipboard action yet.");
        lblTransferCaption.Text = HostUiText.Bi("傳輸", "Transfer");
        lblTransferValue.Text = HostUiText.Bi("目前沒有進行中的傳輸。", "No active transfer.");
        lblTransferPathValue.Text = HostUiText.Bi("目的地：尚未傳送檔案。", "Destination: no file transferred yet.");
        lblZoomCaption.Text = HostUiText.Bi("縮放", "Zoom");
        HostUiText.ApplyButton(btnOpenTransferFolder, "開啟資料夾", "Open Folder");
        HostUiText.ApplyButton(btnDownloadFile, "下載檔案", "Download File");
        HostUiText.ApplyButton(btnSendClipboard, "送出剪貼簿", "Send Clipboard");
        HostUiText.ApplyButton(btnGetClipboard, "取得剪貼簿", "Get Clipboard");
        HostUiText.ApplyButton(btnUploadFile, "上傳檔案", "Upload File");
        HostUiText.ApplyButton(btnFullscreen, "全螢幕", "Fullscreen");
        HostUiText.ApplyButton(btnFocusRemote, "聚焦 Viewer", "Focus Viewer");
        HostUiText.ApplyButton(btnDisconnect, "中斷連線", "Disconnect");
        HostUiText.ApplyButton(btnActions, "功能 v", "Actions v");
        menuOpenTransferFolder.Text = HostUiText.Bi("開啟資料夾", "Open Folder");
        menuDownloadFile.Text = HostUiText.Bi("下載檔案", "Download File");
        menuSendClipboard.Text = HostUiText.Bi("送出剪貼簿", "Send Clipboard");
        menuGetClipboard.Text = HostUiText.Bi("取得剪貼簿", "Get Clipboard");
        menuUploadFile.Text = HostUiText.Bi("上傳檔案", "Upload File");
        menuTakeControl.Text = HostUiText.Bi("取得控制權", "Take Control");
        menuSecureAttention.Text = HostUiText.Bi("切換登入畫面", "Switch to Sign-in");
        menuFocusRemote.Text = HostUiText.Bi("聚焦 Viewer", "Focus Viewer");
        menuDisconnect.Text = HostUiText.Bi("中斷連線", "Disconnect");
        menuFullscreen.Text = HostUiText.Bi("全螢幕", "Fullscreen");
        SetTransferPanelVisible(false);
    }

    private static string FormatBytes(long bytes)
    {
        var value = bytes;
        var units = new[] { "B", "KB", "MB", "GB" };
        var unitIndex = 0;
        double display = value;
        while (display >= 1024d && unitIndex < units.Length - 1)
        {
            display /= 1024d;
            unitIndex++;
        }

        return $"{display:0.##} {units[unitIndex]}";
    }

    private static string TranslateMouseButton(MouseButtons button)
    {
        return button switch
        {
            MouseButtons.Right => "right",
            MouseButtons.Middle => "middle",
            _ => "left"
        };
    }

    private static bool TryTranslateKeyCode(Keys key, out string code)
    {
        if (KeyCodeMap.TryGetValue(key, out code!))
        {
            return true;
        }

        if (key is >= Keys.A and <= Keys.Z)
        {
            code = $"Key{key}";
            return true;
        }

        if (key is >= Keys.D0 and <= Keys.D9)
        {
            code = $"Digit{(int)(key - Keys.D0)}";
            return true;
        }

        code = string.Empty;
        return false;
    }

    private static bool IsPlainPrintableKey(KeyEventArgs e)
    {
        var key = e.KeyCode;
        var printable = (key is >= Keys.A and <= Keys.Z) || (key is >= Keys.D0 and <= Keys.D9);
        return printable && !e.Control && !e.Alt;
    }

    private sealed class DownloadRequestForm : Form
    {
        private readonly TextBox _txtRemotePath;

        public DownloadRequestForm(string? suggestedRemotePath)
        {
            Text = HostUiText.Window("下載檔案", "Download File");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(580, 154);

            var lblPrompt = new Label
            {
                AutoSize = false,
                Location = new Point(16, 16),
                Size = new Size(548, 40),
                Text = HostUiText.Bi("輸入 Agent 端檔案路徑，可使用完整路徑或傳輸資料夾內的相對檔名。", "Enter the Agent file path. You can use a full path or a relative name inside the transfer folder.")
            };

            _txtRemotePath = new TextBox
            {
                Location = new Point(16, 64),
                Size = new Size(548, 23),
                Text = suggestedRemotePath ?? string.Empty
            };

            var btnOk = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(378, 108),
                Size = new Size(90, 30),
                Text = HostUiText.Bi("確定", "OK")
            };

            var btnCancel = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(474, 108),
                Size = new Size(90, 30),
                Text = HostUiText.Bi("取消", "Cancel")
            };

            Controls.Add(lblPrompt);
            Controls.Add(_txtRemotePath);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        public string RemotePath => _txtRemotePath.Text.Trim();
    }
}






















