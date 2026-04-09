using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services;
using System.Buffers;

namespace RemoteDesktop.Host.Forms;

public partial class RemoteViewerForm : Form
{
    private const int MaxClipboardCharacters = 32_768;
    private const int UploadChunkBytes = 48 * 1024;
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
    private DeviceBroker? _deviceBroker;
    private Size _frameSize = Size.Empty;
    private bool _attached;
    private long _lastMoveAt;
    private bool _commandFailed;
    private bool _observeOnlyNoticeShown;
    private string? _activeUploadId;
    private TaskCompletionSource<AgentClipboardMessage>? _clipboardSyncSignal;
    private TaskCompletionSource<AgentFileTransferStatusMessage>? _uploadStartSignal;
    private TaskCompletionSource<AgentFileTransferStatusMessage>? _uploadCompletionSignal;

    public RemoteViewerForm()
    {
        InitializeComponent();
        InitializeUiText();
        KeyPreview = true;
    }

    public void Bind(DeviceRecord device, AuthenticatedUserSession viewer, DeviceBroker deviceBroker)
    {
        _device = device;
        _viewer = viewer;
        _deviceBroker = deviceBroker;
        Text = viewer.CanControlRemote
            ? $"{device.DeviceName} - {HostUiText.Window("遠端檢視", "Remote Viewer")}"
            : $"{device.DeviceName} - {HostUiText.Window("遠端檢視（僅觀看）", "Remote Viewer (Observe Only)")}";
        lblDeviceValue.Text = $"{device.DeviceName} ({device.DeviceId})";
        lblHostValue.Text = device.HostName;
        lblResolutionValue.Text = $"{device.ScreenWidth} x {device.ScreenHeight}";
        btnSendClipboard.Enabled = viewer.CanControlRemote;
        btnGetClipboard.Enabled = viewer.CanControlRemote;
        btnUploadFile.Enabled = viewer.CanControlRemote;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_deviceBroker is null || _device is null || _viewer is null)
        {
            Close();
            return;
        }

        lblStatusValue.Text = HostUiText.Bi("連線中...", "Connecting...");
        var attached = await _deviceBroker.AttachViewerAsync(
            _device.DeviceId,
            _viewer.DisplayName,
            PublishFrameAsync,
            PublishStatusAsync,
            PublishClipboardAsync,
            CancellationToken.None);

        if (!attached)
        {
            var message = _device.IsAuthorized
                ? HostUiText.Bi("所選裝置目前離線，或已被其他 Viewer 使用中。", "The selected device is offline or already in use by another viewer.")
                : HostUiText.Bi("所選裝置仍在等待核准，請先核准無人值守存取後再開啟 Viewer。", "The selected device is waiting for approval. Approve unattended access before opening a viewer session.");
            MessageBox.Show(message, HostUiText.Window("遠端檢視", "Remote Viewer"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
            return;
        }

        _attached = true;
        lblStatusValue.Text = _viewer.CanControlRemote
            ? HostUiText.Bi("已連線", "Connected")
            : HostUiText.Bi("已連線（僅觀看）", "Connected (observe only)");
        lblTransferValue.Text = _viewer.CanControlRemote
            ? HostUiText.Bi("可開始上傳檔案。", "Ready to upload files.")
            : HostUiText.Bi("僅觀看模式無法傳送檔案。", "Observe-only mode cannot transfer files.");
        lblClipboardValue.Text = _viewer.CanControlRemote
            ? HostUiText.Bi("剪貼簿同步已就緒。", "Clipboard sync is ready.")
            : HostUiText.Bi("僅觀看模式無法同步剪貼簿。", "Observe-only mode cannot sync clipboard.");
        pictureStream.Focus();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_activeUploadId))
        {
            await TryAbortUploadAsync(_activeUploadId);
        }

        if (_attached && _deviceBroker is not null && _device is not null)
        {
            await _deviceBroker.DetachViewerAsync(_device.DeviceId);
            _attached = false;
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

    private void ApplyFrame(byte[] payload)
    {
        try
        {
            using var stream = new MemoryStream(payload);
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            var bitmap = new Bitmap(source);
            _frameSize = bitmap.Size;

            var previous = pictureStream.Image;
            pictureStream.Image = bitmap;
            previous?.Dispose();

            lblStatusValue.Text = HostUiText.Bi($"串流中 {_frameSize.Width} x {_frameSize.Height}", $"Streaming {_frameSize.Width} x {_frameSize.Height}");
        }
        catch (Exception exception)
        {
            lblStatusValue.Text = HostUiText.Bi($"畫面解碼失敗：{exception.Message}", $"Frame decode failed: {exception.Message}");
        }
    }

    private void ApplyTransferStatus(AgentFileTransferStatusMessage status)
    {
        if (!string.IsNullOrWhiteSpace(_activeUploadId) && !string.Equals(_activeUploadId, status.UploadId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        switch (status.Status)
        {
            case "started":
                _uploadStartSignal?.TrySetResult(status);
                progressFileTransfer.Value = 0;
                lblTransferValue.Text = status.Message;
                break;
            case "progress":
                UpdateTransferProgress(status.BytesTransferred, status.FileSize);
                lblTransferValue.Text = HostUiText.Bi(
                    $"{status.StoredFileName}：{FormatBytes(status.BytesTransferred)} / {FormatBytes(status.FileSize)}",
                    $"{status.StoredFileName}: {FormatBytes(status.BytesTransferred)} / {FormatBytes(status.FileSize)}");
                break;
            case "completed":
                _uploadCompletionSignal?.TrySetResult(status);
                UpdateTransferProgress(status.FileSize, status.FileSize);
                lblTransferValue.Text = status.Message;
                btnUploadFile.Enabled = _viewer?.CanControlRemote == true;
                _activeUploadId = null;
                break;
            case "failed":
                _uploadStartSignal?.TrySetResult(status);
                _uploadCompletionSignal?.TrySetResult(status);
                progressFileTransfer.Value = 0;
                lblTransferValue.Text = status.Message;
                btnUploadFile.Enabled = _viewer?.CanControlRemote == true;
                _activeUploadId = null;
                MessageBox.Show(status.Message, HostUiText.Window("檔案傳輸", "File Transfer"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                if (string.IsNullOrEmpty(message.Text))
                {
                    Clipboard.Clear();
                }
                else
                {
                    Clipboard.SetText(message.Text);
                }

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

    private async void btnUploadFile_Click(object sender, EventArgs e)
    {
        if (!EnsureInteractivePermission(HostUiText.Bi("此帳號可開啟 Viewer，但沒有傳送檔案的權限。", "This account can open viewer sessions but does not have permission to transfer files.")))
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = HostUiText.Window("選擇要上傳的檔案", "Select a file to upload"),
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.FileName))
        {
            return;
        }

        await UploadFileAsync(dialog.FileName);
    }

    private async void btnSendClipboard_Click(object sender, EventArgs e)
    {
        if (!EnsureInteractivePermission(HostUiText.Bi("此帳號可開啟 Viewer，但沒有同步剪貼簿的權限。", "This account can open viewer sessions but does not have permission to sync clipboard text.")))
        {
            return;
        }

        string text;
        try
        {
            if (!Clipboard.ContainsText())
            {
                MessageBox.Show(HostUiText.Bi("本機剪貼簿沒有純文字內容。", "Local clipboard does not contain plain text."), HostUiText.Window("剪貼簿同步", "Clipboard Sync"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            text = Clipboard.GetText();
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
            btnSendClipboard.Enabled = _viewer?.CanControlRemote == true;
            btnGetClipboard.Enabled = _viewer?.CanControlRemote == true;
        }
    }

    private async void btnGetClipboard_Click(object sender, EventArgs e)
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
            btnSendClipboard.Enabled = _viewer?.CanControlRemote == true;
            btnGetClipboard.Enabled = _viewer?.CanControlRemote == true;
        }
    }

    private async Task UploadFileAsync(string filePath)
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
        btnUploadFile.Enabled = false;
        progressFileTransfer.Value = 0;
        lblTransferValue.Text = HostUiText.Bi($"正在上傳 {fileInfo.Name}...", $"Uploading {fileInfo.Name}...");

        try
        {
            await UploadFileCoreAsync(fileInfo, uploadId);

            var completionStatus = await _uploadCompletionSignal!.Task.WaitAsync(TimeSpan.FromSeconds(30));
            if (string.Equals(completionStatus.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(completionStatus.Message);
            }
        }
        catch (Exception exception)
        {
            var shouldShowDialog = !string.IsNullOrWhiteSpace(_activeUploadId);
            await TryAbortUploadAsync(uploadId);
            _activeUploadId = null;
            btnUploadFile.Enabled = _viewer?.CanControlRemote == true;
            progressFileTransfer.Value = 0;
            lblTransferValue.Text = HostUiText.Bi($"上傳失敗：{exception.Message}", $"Upload failed: {exception.Message}");
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

    private async Task UploadFileCoreAsync(FileInfo fileInfo, string uploadId)
    {
        await SendViewerCommandCoreAsync(new ViewerCommandMessage
        {
            Type = "file-upload-start",
            UploadId = uploadId,
            FileName = fileInfo.Name,
            FileSize = fileInfo.Length
        }, CancellationToken.None).ConfigureAwait(false);

        var startStatus = await _uploadStartSignal!.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
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

        await UpdateUiAsync(() =>
        {
            lblTransferValue.Text = HostUiText.Bi($"等待 Agent 完成 {fileInfo.Name} 的檔案寫入...", $"Waiting for Agent to finalize {fileInfo.Name}...");
        }).ConfigureAwait(false);
    }

    private async Task TryAbortUploadAsync(string uploadId)
    {
        try
        {
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

    private void btnFocusRemote_Click(object sender, EventArgs e)
    {
        pictureStream.Focus();
    }

    private async void btnDisconnect_Click(object sender, EventArgs e)
    {
        if (_deviceBroker is not null && _device is not null)
        {
            await _deviceBroker.DetachViewerAsync(_device.DeviceId);
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
        if (!_attached || _deviceBroker is null || _device is null)
        {
            return;
        }

        if (!EnsureInteractivePermission(HostUiText.Bi("此帳號可開啟 Viewer，但沒有傳送遠端控制或檔案的權限。", "This account can open viewer sessions but does not have permission to send remote control input or transfer files.")))
        {
            return;
        }

        try
        {
            await _deviceBroker.ForwardViewerCommandAsync(_device.DeviceId, command, CancellationToken.None);
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
        if (!_attached || _deviceBroker is null || _device is null)
        {
            throw new InvalidOperationException(HostUiText.Bi("Viewer 尚未連線到遠端裝置。", "The viewer is not connected to a remote device."));
        }

        return _deviceBroker.ForwardViewerCommandAsync(_device.DeviceId, command, cancellationToken);
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
        if (_viewer is { CanControlRemote: true })
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
        Text = HostUiText.Window("遠端檢視", "Remote Viewer");
        lblDeviceCaption.Text = HostUiText.Bi("裝置", "Device");
        lblHostCaption.Text = HostUiText.Bi("主機", "Host");
        lblResolutionCaption.Text = HostUiText.Bi("解析度", "Resolution");
        lblStatusCaption.Text = HostUiText.Bi("狀態", "Status");
        lblStatusValue.Text = HostUiText.Bi("閒置", "Idle");
        lblClipboardCaption.Text = HostUiText.Bi("剪貼簿", "Clipboard");
        lblClipboardValue.Text = HostUiText.Bi("尚未執行剪貼簿操作。", "No clipboard action yet.");
        lblTransferCaption.Text = HostUiText.Bi("傳輸", "Transfer");
        lblTransferValue.Text = HostUiText.Bi("目前沒有進行中的傳輸。", "No active transfer.");
        HostUiText.ApplyButton(btnSendClipboard, "送出剪貼簿", "Send Clipboard");
        HostUiText.ApplyButton(btnGetClipboard, "取得剪貼簿", "Get Clipboard");
        HostUiText.ApplyButton(btnUploadFile, "上傳檔案", "Upload File");
        HostUiText.ApplyButton(btnFocusRemote, "聚焦 Viewer", "Focus Viewer");
        HostUiText.ApplyButton(btnDisconnect, "中斷連線", "Disconnect");
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
}

