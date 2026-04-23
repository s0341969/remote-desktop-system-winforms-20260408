using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using RemoteDesktop.Agent.Compatibility;
using RemoteDesktop.Agent.Models;

namespace RemoteDesktop.Agent.Services;

public sealed class InputInjectionService
{
    private static readonly IReadOnlyDictionary<string, KeySpec> KeyMap = new Dictionary<string, KeySpec>(StringComparer.Ordinal)
    {
        ["Backspace"] = new(0x08, false),
        ["Tab"] = new(0x09, false),
        ["Enter"] = new(0x0D, false),
        ["ShiftLeft"] = new(0x10, false),
        ["ShiftRight"] = new(0x10, false),
        ["ControlLeft"] = new(0x11, false),
        ["ControlRight"] = new(0x11, true),
        ["AltLeft"] = new(0x12, false),
        ["AltRight"] = new(0x12, true),
        ["Escape"] = new(0x1B, false),
        ["Space"] = new(0x20, false),
        ["PageUp"] = new(0x21, true),
        ["PageDown"] = new(0x22, true),
        ["End"] = new(0x23, true),
        ["Home"] = new(0x24, true),
        ["ArrowLeft"] = new(0x25, true),
        ["ArrowUp"] = new(0x26, true),
        ["ArrowRight"] = new(0x27, true),
        ["ArrowDown"] = new(0x28, true),
        ["Insert"] = new(0x2D, true),
        ["Delete"] = new(0x2E, true),
        ["MetaLeft"] = new(0x5B, true),
        ["MetaRight"] = new(0x5C, true),
        ["F1"] = new(0x70, false),
        ["F2"] = new(0x71, false),
        ["F3"] = new(0x72, false),
        ["F4"] = new(0x73, false),
        ["F5"] = new(0x74, false),
        ["F6"] = new(0x75, false),
        ["F7"] = new(0x76, false),
        ["F8"] = new(0x77, false),
        ["F9"] = new(0x78, false),
        ["F10"] = new(0x79, false),
        ["F11"] = new(0x7A, false),
        ["F12"] = new(0x7B, false),
        ["Semicolon"] = new(0xBA, false),
        ["Equal"] = new(0xBB, false),
        ["Comma"] = new(0xBC, false),
        ["Minus"] = new(0xBD, false),
        ["Period"] = new(0xBE, false),
        ["Slash"] = new(0xBF, false),
        ["Backquote"] = new(0xC0, false),
        ["BracketLeft"] = new(0xDB, false),
        ["Backslash"] = new(0xDC, false),
        ["BracketRight"] = new(0xDD, false),
        ["Quote"] = new(0xDE, false)
    };

    private readonly AgentRuntimeState _runtimeState;
    private readonly ILogger<InputInjectionService> _logger;
    private int _lastPrivilegeWarningTick;

    public InputInjectionService(AgentRuntimeState runtimeState, ILogger<InputInjectionService> logger)
    {
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public void Apply(ViewerCommandMessage request)
    {
        try
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
                case "secure-attention":
                    SwitchToSigninScreen();
                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Input injection failed for command type {CommandType}.", request.Type);
            PublishInjectionWarning(exception.Message);
        }
    }

    public void SwitchToSigninScreen()
    {
        if (!LockWorkStation())
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "LockWorkStation failed.");
        }
    }

    public bool IsProcessElevated() => IsCurrentProcessElevated();

    private void PublishInjectionWarning(string reason)
    {
        var suffix = IsCurrentProcessElevated()
            ? string.Empty
            : AgentUiText.Bi(" 目前 Agent 並未以系統管理員權限執行，較高權限視窗可能拒絕接收輸入。", " The Agent is not running elevated, so higher-privilege windows may reject input.");

        var now = Environment.TickCount;
        if (unchecked(now - _lastPrivilegeWarningTick) < 5000)
        {
            return;
        }

        _lastPrivilegeWarningTick = now;
        _runtimeState.MarkWarning(AgentUiText.Bi($"遠端控制輸入失敗：{reason}.{suffix}", $"Remote control input failed: {reason}.{suffix}"));
    }

    private static void MovePointer(double x, double y)
    {
        var bounds = SystemInformation.VirtualScreen;
        var absoluteX = bounds.Left + (int)Math.Round(Net48Compat.Clamp(x, 0d, 1d) * Math.Max(bounds.Width - 1, 1));
        var absoluteY = bounds.Top + (int)Math.Round(Net48Compat.Clamp(y, 0d, 1d) * Math.Max(bounds.Height - 1, 1));

        var normalizedX = NormalizeAbsoluteCoordinate(absoluteX, bounds.Left, bounds.Width);
        var normalizedY = NormalizeAbsoluteCoordinate(absoluteY, bounds.Top, bounds.Height);

        SendMouseInput(MouseEventFMove | MouseEventFAbsolute | MouseEventFVirtualDesk, 0, normalizedX, normalizedY);
    }

    private static void SendMouseButton(string? button, bool isDown)
    {
        var flag = (button ?? "left").ToLowerInvariant() switch
        {
            "right" => isDown ? MouseEventFRightDown : MouseEventFRightUp,
            "middle" => isDown ? MouseEventFMiddleDown : MouseEventFMiddleUp,
            _ => isDown ? MouseEventFLeftDown : MouseEventFLeftUp
        };

        SendMouseInput(flag, 0, 0, 0);
    }

    private static void SendMouseWheel(int deltaY)
    {
        var wheelDelta = deltaY < 0 ? unchecked((uint)-120) : 120u;
        SendMouseInput(MouseEventFWheel, wheelDelta, 0, 0);
    }

    private static void SendMouseInput(uint flags, uint mouseData, int dx, int dy)
    {
        var input = new INPUT
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    mouseData = mouseData,
                    dwFlags = flags
                }
            }
        };

        EnsureSent([input]);
    }

    private static void SendKey(string? code, bool isKeyUp)
    {
        var keySpec = ResolveKeySpec(code);
        if (keySpec is null)
        {
            return;
        }

        var scanCode = MapVirtualKey(keySpec.Value.VirtualKey, MapvkVkToVsc);
        if (scanCode == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not resolve scan code for key '{code}'.");
        }

        var flags = KeyeventfScancode | (isKeyUp ? KeyeventfKeyup : 0u);
        if (keySpec.Value.IsExtended)
        {
            flags |= KeyeventfExtendedKey;
        }

        var input = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = (ushort)scanCode,
                    dwFlags = flags
                }
            }
        };

        EnsureSent([input]);
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

            EnsureSent([keyDown, keyUp]);
        }
    }

    private static void EnsureSent(INPUT[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == inputs.Length)
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error == 0)
        {
            throw new InvalidOperationException("SendInput reported a partial send.");
        }

        throw new Win32Exception(error);
    }

    private static KeySpec? ResolveKeySpec(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        if (KeyMap.TryGetValue(code, out var mapped))
        {
            return mapped;
        }

        if (code.Length == 4 && code.StartsWith("Key", StringComparison.Ordinal))
        {
            return new KeySpec((ushort)char.ToUpperInvariant(code[3]), false);
        }

        if (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal))
        {
            return new KeySpec((ushort)code[5], false);
        }

        return null;
    }

    private static int NormalizeAbsoluteCoordinate(int actualCoordinate, int offset, int span)
    {
        if (span <= 1)
        {
            return 0;
        }

        var relative = actualCoordinate - offset;
        return (int)Math.Round(relative * 65535d / Math.Max(span - 1, 1));
    }

    private static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private readonly record struct KeySpec(ushort VirtualKey, bool IsExtended);

    private const int InputMouse = 0;
    private const int InputKeyboard = 1;
    private const uint MouseEventFMove = 0x0001;
    private const uint MouseEventFLeftDown = 0x0002;
    private const uint MouseEventFLeftUp = 0x0004;
    private const uint MouseEventFRightDown = 0x0008;
    private const uint MouseEventFRightUp = 0x0010;
    private const uint MouseEventFMiddleDown = 0x0020;
    private const uint MouseEventFMiddleUp = 0x0040;
    private const uint MouseEventFWheel = 0x0800;
    private const uint MouseEventFVirtualDesk = 0x4000;
    private const uint MouseEventFAbsolute = 0x8000;
    private const uint KeyeventfExtendedKey = 0x0001;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfUnicode = 0x0004;
    private const uint KeyeventfScancode = 0x0008;
    private const uint MapvkVkToVsc = 0;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

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
        public uint mouseData;
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
