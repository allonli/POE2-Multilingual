using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Poe2DbLookup.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const int VkLeftControl = 0xA2;
    private const int VkRightControl = 0xA3;
    private const int VkE = 0x45;
    private const int VkW = 0x57;

    private readonly NativeMethods.LowLevelKeyboardProc _keyboardProc;
    private readonly HashSet<int> _pressedKeys = [];
    private nint _hook;
    private HotkeyGesture _hotkey = HotkeyGesture.Parse("LeftCtrl+E");
    private bool _registered;
    private bool _disposed;
    private bool _chordHandled;

    public HotkeyService()
    {
        _keyboardProc = KeyboardProc;
    }

    public event EventHandler<nint>? LeftCtrlEPressed;

    public event EventHandler? CtrlWPressed;

    public Func<bool>? ShouldHandleCtrlW { get; set; }

    public bool IsPaused { get; set; }

    public void SetHotkey(HotkeyGesture hotkey)
    {
        _hotkey = hotkey;
        _chordHandled = false;
        _pressedKeys.Clear();
    }

    public void Start()
    {
        if (_registered)
        {
            return;
        }

        using var currentProcess = Process.GetCurrentProcess();
        var moduleHandle = currentProcess.MainModule is null
            ? 0
            : NativeMethods.GetModuleHandle(currentProcess.MainModule.ModuleName);

        _hook = NativeMethods.SetWindowsHookEx(
            WhKeyboardLl,
            _keyboardProc,
            moduleHandle,
            0);
        if (_hook == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法注册全局热键 Left Ctrl+E。");
        }

        _registered = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_registered && _hook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = 0;
            _registered = false;
        }

        _disposed = true;
    }

    private nint KeyboardProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var keyboard = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);

        if (message is WmKeyDown or WmSysKeyDown)
        {
            _pressedKeys.Add(keyboard.VkCode);
        }

        if (message is WmKeyUp or WmSysKeyUp)
        {
            _pressedKeys.Remove(keyboard.VkCode);
            _chordHandled = false;
        }

        if (IsPaused)
        {
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        if (!_chordHandled
            && message is WmKeyDown or WmSysKeyDown
            && keyboard.VkCode == VkW
            && IsControlDown()
            && ShouldHandleCtrlW?.Invoke() == true)
        {
            _chordHandled = true;
            CtrlWPressed?.Invoke(this, EventArgs.Empty);
            return 1;
        }

        if (!_chordHandled
            && message is WmKeyDown or WmSysKeyDown
            && _hotkey.MatchesNative(keyboard.VkCode, IsKeyDown))
        {
            _chordHandled = true;
            LeftCtrlEPressed?.Invoke(this, NativeMethods.GetForegroundWindow());
            return 1;
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool IsKeyDown(int virtualKey)
    {
        return _pressedKeys.Contains(virtualKey) || (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private bool IsControlDown()
    {
        return IsKeyDown(VkLeftControl) || IsKeyDown(VkRightControl);
    }

    private static bool IsModifierKey(int virtualKey)
    {
        return virtualKey is VkLeftControl or VkRightControl
            or 0xA0 or 0xA1
            or 0xA4 or 0xA5
            or 0x5B or 0x5C;
    }
}
