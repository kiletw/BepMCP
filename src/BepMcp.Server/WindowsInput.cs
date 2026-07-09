using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BepMcp.Server;

public sealed class WindowsInput
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008;
    private const uint MouseRightUp = 0x0010;
    private const uint MouseMiddleDown = 0x0020;
    private const uint MouseMiddleUp = 0x0040;
    private const uint MouseWheel = 0x0800;

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _active =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public WindowsInput()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("BepMCP input currently requires Windows.");
        }

        SetProcessDPIAware();
    }

    public async Task<string> ExecuteAsync(
        IReadOnlyList<InputStep> actions,
        int processId,
        string? requestId,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        if (actions.Count is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(actions), "Actions must contain 1 to 100 items.");
        }

        if (timeoutMs is < 100 or > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "timeoutMs must be from 100 to 30000.");
        }

        var id = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId;
        using var timeout = new CancellationTokenSource(timeoutMs);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeout.Token);
        if (!_active.TryAdd(id, stop))
        {
            throw new InvalidOperationException("requestId is already active.");
        }

        var heldKeys = new HashSet<ushort>();
        try
        {
            await _gate.WaitAsync(stop.Token).ConfigureAwait(false);
            try
            {
                var window = GetGameWindow(processId);
                FocusGameWindow(window, processId);

                foreach (var action in actions)
                {
                    stop.Token.ThrowIfCancellationRequested();
                    await ExecuteStep(window, action, heldKeys, stop.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                foreach (var key in heldKeys)
                {
                    SendKey(key, true);
                }

                _gate.Release();
            }
        }
        finally
        {
            _active.TryRemove(id, out _);
        }

        return JsonSerializer.Serialize(new
        {
            requestId = id,
            ok = true,
            completed = actions.Count
        });
    }

    public bool Cancel(string requestId)
    {
        if (!_active.TryGetValue(requestId, out var stop))
        {
            return false;
        }

        stop.Cancel();
        return true;
    }

    internal static void SelfTest()
    {
        Assert(ResolveVirtualKey("W") == 0x57);
        Assert(ResolveVirtualKey("space") == 0x20);
        Assert(ResolveVirtualKey("left") == 0x25);

        try
        {
            ResolveVirtualKey("not-a-key");
            throw new InvalidOperationException("Invalid key was accepted.");
        }
        catch (ArgumentException)
        {
        }
    }

    private static void Assert(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Windows input self-test failed.");
        }
    }

    private static async Task ExecuteStep(
        nint window,
        InputStep action,
        HashSet<ushort> heldKeys,
        CancellationToken cancellationToken)
    {
        switch (action.Type.Trim().ToLowerInvariant())
        {
            case "key_down":
            {
                var key = ResolveVirtualKey(action.Key);
                SendKey(key, false);
                heldKeys.Add(key);
                break;
            }
            case "key_up":
            {
                var key = ResolveVirtualKey(action.Key);
                SendKey(key, true);
                heldKeys.Remove(key);
                break;
            }
            case "key_tap":
            {
                var key = ResolveVirtualKey(action.Key);
                var duration = action.Ms ?? 80;
                if (duration is < 10 or > 2000)
                {
                    throw new ArgumentOutOfRangeException(nameof(action.Ms), "key_tap ms must be from 10 to 2000.");
                }

                SendKey(key, false);
                heldKeys.Add(key);
                await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
                SendKey(key, true);
                heldKeys.Remove(key);
                break;
            }
            case "mouse_move":
                MoveCursor(window, Required(action.X, "x"), Required(action.Y, "y"));
                break;
            case "mouse_click":
                if (action.X.HasValue || action.Y.HasValue)
                {
                    MoveCursor(window, Required(action.X, "x"), Required(action.Y, "y"));
                }

                Click(action.Button ?? "left");
                break;
            case "scroll":
                Scroll(action.Delta ?? 120);
                break;
            case "wait":
            {
                var duration = action.Ms ?? throw new ArgumentException("wait requires ms.");
                if (duration is < 0 or > 5000)
                {
                    throw new ArgumentOutOfRangeException(nameof(action.Ms), "wait ms must be from 0 to 5000.");
                }

                await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
                break;
            }
            default:
                throw new ArgumentException("Unknown input step: " + action.Type);
        }
    }

    private static nint GetGameWindow(int processId)
    {
        using var process = Process.GetProcessById(processId);
        process.Refresh();
        if (process.MainWindowHandle != 0)
        {
            return process.MainWindowHandle;
        }

        nint bestWindow = 0;
        long bestArea = 0;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var ownerProcessId);
            if (ownerProcessId != processId ||
                !IsWindowVisible(window) ||
                !GetClientRect(window, out var rect))
            {
                return true;
            }

            var area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
            if (area > bestArea)
            {
                bestArea = area;
                bestWindow = window;
            }

            return true;
        }, 0);

        if (bestWindow == 0)
        {
            throw new InvalidOperationException("Unity game has no main window.");
        }

        return bestWindow;
    }

    private static void FocusGameWindow(nint window, int processId)
    {
        ShowWindow(window, 5);
        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var gameThread = GetWindowThreadProcessId(window, out _);
        var attached = foregroundThread != gameThread &&
            AttachThreadInput(foregroundThread, gameThread, true);
        try
        {
            BringWindowToTop(window);
            SetForegroundWindow(window);
            SetFocus(window);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(foregroundThread, gameThread, false);
            }
        }

        Thread.Sleep(50);
        foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out var foregroundProcessId);
        if (foregroundProcessId != processId)
        {
            throw new InvalidOperationException("Could not focus the Unity game window.");
        }
    }

    private static void MoveCursor(nint window, int x, int y)
    {
        if (!GetClientRect(window, out var rect) ||
            x < 0 || y < 0 || x >= rect.Right || y >= rect.Bottom)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Mouse coordinates are outside the game client area.");
        }

        var point = new Point { X = x, Y = y };
        if (!ClientToScreen(window, ref point) || !SetCursorPos(point.X, point.Y))
        {
            throw new InvalidOperationException("Could not move the mouse cursor.");
        }
    }

    private static void Click(string button)
    {
        var flags = button.Trim().ToLowerInvariant() switch
        {
            "left" => (MouseLeftDown, MouseLeftUp),
            "right" => (MouseRightDown, MouseRightUp),
            "middle" => (MouseMiddleDown, MouseMiddleUp),
            _ => throw new ArgumentException("Mouse button must be left, right, or middle.")
        };

        SendMouse(flags.Item1, 0);
        SendMouse(flags.Item2, 0);
    }

    private static void Scroll(int delta)
    {
        if (delta is < -1200 or > 1200)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Scroll delta must be from -1200 to 1200.");
        }

        SendMouse(MouseWheel, delta);
    }

    private static void SendKey(ushort key, bool keyUp)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = key,
                    Flags = keyUp ? KeyUp : 0
                }
            }
        };
        Send(input);
    }

    private static void SendMouse(uint flags, int data)
    {
        var input = new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    MouseData = data,
                    Flags = flags
                }
            }
        };
        Send(input);
    }

    private static void Send(Input input)
    {
        if (SendInput(1, new[] { input }, Marshal.SizeOf<Input>()) != 1)
        {
            throw new InvalidOperationException("Windows SendInput failed.");
        }
    }

    private static ushort ResolveVirtualKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Keyboard action requires key.");
        }

        var key = name.Trim().ToUpperInvariant();
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return key[0];
        }

        return key switch
        {
            "SPACE" => 0x20,
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            "ENTER" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "TAB" => 0x09,
            "SHIFT" => 0x10,
            "CTRL" or "CONTROL" => 0x11,
            "ALT" => 0x12,
            "BACKSPACE" => 0x08,
            _ => throw new ArgumentException("Unsupported key: " + name)
        };
    }

    private static int Required(int? value, string name)
    {
        return value ?? throw new ArgumentException("Mouse action requires " + name + ".");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public int MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(nint window);

    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint window);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint sourceThread, uint targetThread, bool attach);

    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out int processId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(nint window, ref Point point);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
