using System.Runtime.InteropServices;
using System.Windows.Forms;
using RemoteDesktop.Agent.Models;

namespace RemoteDesktop.Agent.Services;

public sealed class InputInjectionService
{
    private static readonly IReadOnlyDictionary<string, ushort> KeyMap = new Dictionary<string, ushort>(StringComparer.Ordinal)
    {
        ["Backspace"] = 0x08,
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["ShiftLeft"] = 0x10,
        ["ShiftRight"] = 0x10,
        ["ControlLeft"] = 0x11,
        ["ControlRight"] = 0x11,
        ["AltLeft"] = 0x12,
        ["AltRight"] = 0x12,
        ["Escape"] = 0x1B,
        ["Space"] = 0x20,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["End"] = 0x23,
        ["Home"] = 0x24,
        ["ArrowLeft"] = 0x25,
        ["ArrowUp"] = 0x26,
        ["ArrowRight"] = 0x27,
        ["ArrowDown"] = 0x28,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["MetaLeft"] = 0x5B,
        ["MetaRight"] = 0x5C,
        ["F1"] = 0x70,
        ["F2"] = 0x71,
        ["F3"] = 0x72,
        ["F4"] = 0x73,
        ["F5"] = 0x74,
        ["F6"] = 0x75,
        ["F7"] = 0x76,
        ["F8"] = 0x77,
        ["F9"] = 0x78,
        ["F10"] = 0x79,
        ["F11"] = 0x7A,
        ["F12"] = 0x7B,
        ["Semicolon"] = 0xBA,
        ["Equal"] = 0xBB,
        ["Comma"] = 0xBC,
        ["Minus"] = 0xBD,
        ["Period"] = 0xBE,
        ["Slash"] = 0xBF,
        ["Backquote"] = 0xC0,
        ["BracketLeft"] = 0xDB,
        ["Backslash"] = 0xDC,
        ["BracketRight"] = 0xDD,
        ["Quote"] = 0xDE
    };

    public void Apply(ViewerCommandMessage request)
    {
        switch (request.Type)
        {
            case "move":
                MovePointer(request.X, request.Y);
                break;
            case "mousedown":
                MovePointer(request.X, request.Y);
                SendMouseButton(request.Button, true);
                break;
            case "mouseup":
                MovePointer(request.X, request.Y);
                SendMouseButton(request.Button, false);
                break;
            case "wheel":
                MovePointer(request.X, request.Y);
                SendMouseWheel(request.DeltaY);
                break;
            case "keydown":
                SendKey(request.Code, false);
                break;
            case "keyup":
                SendKey(request.Code, true);
                break;
            case "text":
                SendUnicodeText(request.Key);
                break;
        }
    }

    private static void MovePointer(double x, double y)
    {
        var bounds = SystemInformation.VirtualScreen;
        var absoluteX = bounds.Left + (int)Math.Round(Math.Clamp(x, 0d, 1d) * Math.Max(bounds.Width - 1, 1));
        var absoluteY = bounds.Top + (int)Math.Round(Math.Clamp(y, 0d, 1d) * Math.Max(bounds.Height - 1, 1));
        SetCursorPos(absoluteX, absoluteY);
    }

    private static void SendMouseButton(string? button, bool isDown)
    {
        var flag = (button ?? "left").ToLowerInvariant() switch
        {
            "right" => isDown ? MouseEventFRightDown : MouseEventFRightUp,
            "middle" => isDown ? MouseEventFMiddleDown : MouseEventFMiddleUp,
            _ => isDown ? MouseEventFLeftDown : MouseEventFLeftUp
        };

        SendMouseInput(flag, 0);
    }

    private static void SendMouseWheel(int deltaY)
    {
        var wheelDelta = deltaY < 0 ? 120 : -120;
        SendMouseInput(MouseEventFWheel, wheelDelta);
    }

    private static void SendMouseInput(uint flags, int mouseData)
    {
        var input = new INPUT
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dwFlags = flags,
                    mouseData = mouseData
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static void SendKey(string? code, bool isKeyUp)
    {
        var virtualKey = ResolveVirtualKey(code);
        if (virtualKey == 0)
        {
            return;
        }

        var input = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    dwFlags = isKeyUp ? KeyeventfKeyup : 0
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static void SendUnicodeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var character in text)
        {
            var keyDown = new INPUT
            {
                type = InputKeyboard,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wScan = character,
                        dwFlags = KeyeventfUnicode
                    }
                }
            };

            var keyUp = new INPUT
            {
                type = InputKeyboard,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wScan = character,
                        dwFlags = KeyeventfUnicode | KeyeventfKeyup
                    }
                }
            };

            SendInput(2, [keyDown, keyUp], Marshal.SizeOf<INPUT>());
        }
    }

    private static ushort ResolveVirtualKey(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return 0;
        }

        if (KeyMap.TryGetValue(code, out var mapped))
        {
            return mapped;
        }

        if (code.Length == 4 && code.StartsWith("Key", StringComparison.Ordinal))
        {
            return code[3];
        }

        if (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal))
        {
            return code[5];
        }

        return 0;
    }

    private const int InputMouse = 0;
    private const int InputKeyboard = 1;
    private const uint MouseEventFLeftDown = 0x0002;
    private const uint MouseEventFLeftUp = 0x0004;
    private const uint MouseEventFRightDown = 0x0008;
    private const uint MouseEventFRightUp = 0x0010;
    private const uint MouseEventFMiddleDown = 0x0020;
    private const uint MouseEventFMiddleUp = 0x0040;
    private const uint MouseEventFWheel = 0x0800;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfUnicode = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public int time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public int time;
        public nint dwExtraInfo;
    }
}
