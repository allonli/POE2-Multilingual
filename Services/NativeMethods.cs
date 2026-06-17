using System.Runtime.InteropServices;

namespace Poe2DbLookup.Services;

internal static class NativeMethods
{
    internal delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    internal enum VirtualKey : ushort
    {
        LeftAlt = 0xA4,
        LeftControl = 0xA2,
        C = 0x43,
        V = 0x56
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KbdLlHookStruct
    {
        public int VkCode;
        public int ScanCode;
        public int Flags;
        public int Time;
        public nint DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mi;

        [FieldOffset(0)]
        public KeyboardInput Ki;
    }

    // SendInput 要求传入完整 Win32 INPUT union 大小；只定义键盘分支会让 x64 cbSize 变成 32 而不是 40。
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, Input[] pInputs, int cbSize);

    internal static bool BringWindowToForeground(nint windowHandle)
    {
        return ActivateWindowCore(windowHandle, makeTopMost: true);
    }

    internal static bool ActivateWindow(nint windowHandle)
    {
        return ActivateWindowCore(windowHandle, makeTopMost: false);
    }

    private static bool ActivateWindowCore(nint windowHandle, bool makeTopMost)
    {
        if (windowHandle == 0)
        {
            return false;
        }

        const int swRestore = 9;
        const uint swpNoMove = 0x0002;
        const uint swpNoSize = 0x0001;
        const uint swpShowWindow = 0x0040;
        var topMost = new nint(-1);

        var currentThreadId = GetCurrentThreadId();
        var foregroundWindow = GetForegroundWindow();
        var foregroundThreadId = foregroundWindow == 0
            ? 0
            : GetWindowThreadProcessId(foregroundWindow, out _);

        if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
        {
            AttachThreadInput(currentThreadId, foregroundThreadId, true);
        }

        try
        {
            ShowWindow(windowHandle, swRestore);
            if (makeTopMost)
            {
                SetWindowPos(windowHandle, topMost, 0, 0, 0, 0, swpNoMove | swpNoSize | swpShowWindow);
            }

            SetForegroundWindow(windowHandle);
            return WaitForForegroundWindow(windowHandle, TimeSpan.FromMilliseconds(120));
        }
        finally
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    internal static bool WaitForForegroundWindow(nint windowHandle, TimeSpan timeout)
    {
        if (windowHandle == 0)
        {
            return false;
        }

        return GetForegroundWindow() == windowHandle
            || InteractionWaiter.WaitUntil(
                () => GetForegroundWindow() == windowHandle,
                timeout,
                TimeSpan.FromMilliseconds(8));
    }

    internal static void SendModifiedKey(VirtualKey modifier, VirtualKey key)
    {
        const uint keyboard = 1;
        const uint keyup = 0x0002;

        var inputs = new[]
        {
            new Input { Type = keyboard, U = new InputUnion { Ki = new KeyboardInput { Vk = (ushort)modifier } } },
            new Input { Type = keyboard, U = new InputUnion { Ki = new KeyboardInput { Vk = (ushort)key } } },
            new Input { Type = keyboard, U = new InputUnion { Ki = new KeyboardInput { Vk = (ushort)key, Flags = keyup } } },
            new Input { Type = keyboard, U = new InputUnion { Ki = new KeyboardInput { Vk = (ushort)modifier, Flags = keyup } } }
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    internal static void ReleaseKey(VirtualKey key)
    {
        const uint keyboard = 1;
        const uint keyup = 0x0002;

        var inputs = new[]
        {
            new Input { Type = keyboard, U = new InputUnion { Ki = new KeyboardInput { Vk = (ushort)key, Flags = keyup } } }
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }
}
