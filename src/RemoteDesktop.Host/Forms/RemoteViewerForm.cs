using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public partial class RemoteViewerForm : Form
{
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
    private string _viewerName = string.Empty;
    private DeviceBroker? _deviceBroker;
    private Size _frameSize = Size.Empty;
    private bool _attached;
    private long _lastMoveAt;
    private bool _commandFailed;

    public RemoteViewerForm()
    {
        InitializeComponent();
        KeyPreview = true;
    }

    public void Bind(DeviceRecord device, string viewerName, DeviceBroker deviceBroker)
    {
        _device = device;
        _viewerName = viewerName;
        _deviceBroker = deviceBroker;
        Text = $"遠端檢視 - {device.DeviceName}";
        lblDeviceValue.Text = $"{device.DeviceName} ({device.DeviceId})";
        lblHostValue.Text = device.HostName;
        lblResolutionValue.Text = $"{device.ScreenWidth} x {device.ScreenHeight}";
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_deviceBroker is null || _device is null)
        {
            Close();
            return;
        }

        lblStatusValue.Text = "連線中...";
        var attached = await _deviceBroker.AttachViewerAsync(_device.DeviceId, _viewerName, PublishFrameAsync, CancellationToken.None);
        if (!attached)
        {
            MessageBox.Show("裝置目前不可用，可能已離線或已被其他檢視器占用。", "RemoteDesktop.Host", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
            return;
        }

        _attached = true;
        lblStatusValue.Text = "已連線";
        pictureStream.Focus();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
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

    private void ApplyFrame(byte[] payload)
    {
        using var stream = new MemoryStream(payload);
        using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
        var bitmap = new Bitmap(source);
        _frameSize = bitmap.Size;

        var previous = pictureStream.Image;
        pictureStream.Image = bitmap;
        previous?.Dispose();

        lblStatusValue.Text = $"串流中 {_frameSize.Width} x {_frameSize.Height}";
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

    private async Task SendCommandAsync(ViewerCommandMessage command)
    {
        if (!_attached || _deviceBroker is null || _device is null)
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
            lblStatusValue.Text = "控制指令傳送失敗";
            if (_commandFailed)
            {
                return;
            }

            _commandFailed = true;
            MessageBox.Show(
                $"遠端控制指令傳送失敗：{exception.Message}",
                "RemoteDesktop.Host",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
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
