Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class RealTargetNative
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, IntPtr extraInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_SHOWWINDOW = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static void ScanChord(ushort modScan, ushort keyScan)
    {
        ScanDown(modScan);
        ScanDown(keyScan);
        ScanUp(keyScan);
        ScanUp(modScan);
    }

    public static void TapScan(ushort scan)
    {
        ScanDown(scan);
        ScanUp(scan);
    }

    public static void TapVk(ushort vk)
    {
        Key(vk, 0);
        Key(vk, KEYEVENTF_KEYUP);
    }

    public static void ClickCenter(IntPtr hWnd)
    {
        RECT rect;
        if (!GetWindowRect(hWnd, out rect)) { return; }
        var x = rect.Left + ((rect.Right - rect.Left) / 2);
        var y = rect.Top + ((rect.Bottom - rect.Top) / 2);
        SetCursorPos(x, y);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
    }

    public static void ForceForeground(IntPtr hWnd)
    {
        uint ignored;
        var currentThreadId = GetCurrentThreadId();
        var foreground = GetForegroundWindow();
        var foregroundThreadId = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out ignored);
        if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId) {
            AttachThreadInput(currentThreadId, foregroundThreadId, true);
        }

        try {
            ShowWindow(hWnd, 5);
            SetWindowPos(hWnd, new IntPtr(-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            SetForegroundWindow(hWnd);
        } finally {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId) {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    public static string ForegroundTitle()
    {
        var builder = new StringBuilder(512);
        GetWindowText(GetForegroundWindow(), builder, builder.Capacity);
        return builder.ToString();
    }

    public static void PostEnter(IntPtr hWnd)
    {
        PostMessage(hWnd, 0x0100, new IntPtr(0x0D), new IntPtr(0x001C0001));
        PostMessage(hWnd, 0x0101, new IntPtr(0x0D), new IntPtr(unchecked((int)0xC01C0001)));
    }

    private static void ScanDown(ushort scan) { SendScan(scan, KEYEVENTF_SCANCODE); }
    private static void ScanUp(ushort scan) { SendScan(scan, KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP); }

    private static void SendScan(ushort scan, uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT { wScan = scan, dwFlags = flags }
            }
        };

        SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void Key(ushort vk, uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = vk, dwFlags = flags }
            }
        };

        SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }
}
"@

function Wait-MainWindowHandle {
    param(
        [System.Diagnostics.Process]$Process,
        [TimeSpan]$Timeout
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed -lt $Timeout) {
        $Process.Refresh()
        $handle = $Process.MainWindowHandle
        if ($null -ne $handle -and $handle.ToInt64() -ne 0) {
            return $handle
        }

        Start-Sleep -Milliseconds 50
    }

    throw "target window handle was not available"
}

function Wait-AutomationWindowHandle {
    param(
        [int]$ProcessId,
        [string]$Title,
        [TimeSpan]$Timeout
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed -lt $Timeout) {
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $procCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $ProcessId)
        $windowCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Window)
        $cond = New-Object System.Windows.Automation.AndCondition($procCond, $windowCond)
        $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Subtree, $cond)
        foreach ($window in $windows) {
            $editCond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::Edit)
            $edit = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $editCond)
            if ($window.Current.Name -eq $Title -or $edit) {
                return [IntPtr]$window.Current.NativeWindowHandle
            }
        }

        Start-Sleep -Milliseconds 50
    }

    throw "target automation window '$Title' was not available"
}

function Get-FirstEditValueByProcessId {
    param([int]$ProcessId)

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $procCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)
    $editCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $cond = New-Object System.Windows.Automation.AndCondition($procCond, $editCond)
    $edit = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cond)
    if (-not $edit) { return $null }

    $valueObj = $null
    if ($edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$valueObj)) {
        return ([System.Windows.Automation.ValuePattern]$valueObj).Current.Value
    }

    $textObj = $null
    if ($edit.TryGetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern, [ref]$textObj)) {
        return ([System.Windows.Automation.TextPattern]$textObj).DocumentRange.GetText(-1).Trim()
    }

    return $null
}

function Focus-FirstEditByProcessId {
    param([int]$ProcessId)

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $procCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)
    $editCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $cond = New-Object System.Windows.Automation.AndCondition($procCond, $editCond)
    $edit = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cond)
    if (-not $edit) { throw "target edit control was not available" }

    $edit.SetFocus()
    $textObj = $null
    if ($edit.TryGetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern, [ref]$textObj)) {
        ([System.Windows.Automation.TextPattern]$textObj).DocumentRange.Select()
        return
    }

    [RealTargetNative]::ScanChord(0x1D, 0x1E)
}

function Get-LookupEditValue {
    $lookup = Get-Process -Name Poe2DbLookup -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $lookup) { return $null }
    return Get-FirstEditValueByProcessId -ProcessId $lookup.Id
}

function Get-LookupWindowHandle {
    $lookup = Get-Process -Name Poe2DbLookup -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $lookup) { return [IntPtr]::Zero }

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $procCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $lookup.Id)
    $windowCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Window)
    $cond = New-Object System.Windows.Automation.AndCondition($procCond, $windowCond)
    $window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if (-not $window) { return [IntPtr]::Zero }

    return [IntPtr]$window.Current.NativeWindowHandle
}

function Wait-LookupWindowHandle {
    param([TimeSpan]$Timeout)

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed -lt $Timeout) {
        $handle = Get-LookupWindowHandle
        if ($handle -ne [IntPtr]::Zero) { return $handle }
        Start-Sleep -Milliseconds 50
    }

    return (Get-LookupWindowHandle)
}

function Wait-LookupWindowHidden {
    param([TimeSpan]$Timeout)

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed -lt $Timeout) {
        $handle = Get-LookupWindowHandle
        if ($handle -eq [IntPtr]::Zero) { return $true }
        Start-Sleep -Milliseconds 50
    }

    return (Get-LookupWindowHandle) -eq [IntPtr]::Zero
}

function Get-LookupButton {
    param([string]$Expected)

    $lookup = Get-Process -Name Poe2DbLookup -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $lookup) { return $null }

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $procCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $lookup.Id)
    $buttonCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $cond = New-Object System.Windows.Automation.AndCondition($procCond, $buttonCond)
    $buttons = $root.FindAll([System.Windows.Automation.TreeScope]::Subtree, $cond)
    foreach ($button in $buttons) {
        if ($button.Current.Name -eq $Expected) {
            return $button
        }
    }

    return $null
}

function Wait-LookupButton {
    param(
        [string]$Expected,
        [TimeSpan]$Timeout
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed -lt $Timeout) {
        $button = Get-LookupButton -Expected $Expected
        if ($button) { return $button }
        Start-Sleep -Milliseconds 50
    }

    return (Get-LookupButton -Expected $Expected)
}

function Wait-SettingsWindow {
    param([TimeSpan]$Timeout)

    $lookup = Get-Process -Name Poe2DbLookup -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $lookup) { return $null }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed -lt $Timeout) {
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $procCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $lookup.Id)
        $windowCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Window)
        $cond = New-Object System.Windows.Automation.AndCondition($procCond, $windowCond)
        $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Subtree, $cond)
        foreach ($window in $windows) {
            if ($window.Current.Name -eq "设置") {
                return $window
            }
        }

        Start-Sleep -Milliseconds 50
    }

    return $null
}

function Get-OutputMenuItem {
    param([string]$Expected)

    $lookup = Get-Process -Name Poe2DbLookup -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $lookup) { return $null }

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $procCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $lookup.Id)
    $menuItemCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::MenuItem)
    $cond = New-Object System.Windows.Automation.AndCondition($procCond, $menuItemCond)
    $items = $root.FindAll([System.Windows.Automation.TreeScope]::Subtree, $cond)
    foreach ($item in $items) {
        if ($item.Current.Name -like "*$Expected*") {
            return $item
        }
    }

    return $null
}

function Wait-OutputMenuItem {
    param(
        [string]$Expected,
        [TimeSpan]$Timeout
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed -lt $Timeout) {
        $item = Get-OutputMenuItem -Expected $Expected
        if ($item) { return $item }
        Start-Sleep -Milliseconds 50
    }

    return (Get-OutputMenuItem -Expected $Expected)
}

function Wait-UntilValue {
    param(
        [scriptblock]$Read,
        [string]$Expected,
        [TimeSpan]$Timeout
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed -lt $Timeout) {
        $value = & $Read
        if ($value -eq $Expected) {
            return $value
        }

        Start-Sleep -Milliseconds 50
    }

    return (& $Read)
}

$sourceText = "$([char]0x8FDC)$([char]0x5C04)"
$expectedOutput = "$sourceText I"
$dotnetExe = "C:\Users\allon\.dotnet\dotnet.exe"
$targetDll = Resolve-Path ".\tests\bin\Debug\net8.0-windows\Poe2DbLookup.Tests.dll"
$target = $null

try {
    if ((Get-LookupWindowHandle) -ne [IntPtr]::Zero) {
        throw "lookup window should be hidden while app is only in tray"
    }

    $target = Start-Process -FilePath $dotnetExe -ArgumentList "`"$targetDll`" --text-target" -PassThru
    $targetHandle = Wait-AutomationWindowHandle -ProcessId $target.Id -Title "POE2 Lookup Interaction Target" -Timeout ([TimeSpan]::FromSeconds(5))
    [RealTargetNative]::ForceForeground($targetHandle)
    [RealTargetNative]::ClickCenter($targetHandle)
    Start-Sleep -Milliseconds 300
    Focus-FirstEditByProcessId -ProcessId $target.Id
    Start-Sleep -Milliseconds 120

    $before = Get-FirstEditValueByProcessId -ProcessId $target.Id
    $foreground = [RealTargetNative]::ForegroundTitle()
    $hotkeyAt = [System.Diagnostics.Stopwatch]::StartNew()
    [RealTargetNative]::ScanChord(0x1D, 0x12)
    $query = Wait-UntilValue -Read { Get-LookupEditValue } -Expected $sourceText -Timeout ([TimeSpan]::FromSeconds(4))
    $queryMs = $hotkeyAt.Elapsed.TotalMilliseconds

    if ($query -ne $sourceText) {
        if ($foreground -notlike "*$sourceText*" -and $foreground -notlike "*POE2 Lookup Interaction Target*") {
            Write-Output "WARN real .NET WPF selected-text chain skipped because foreground was '$foreground' and query was '$query'"
            if ((Get-LookupWindowHandle) -ne [IntPtr]::Zero) {
                [RealTargetNative]::ScanChord(0x1D, 0x11)
                Wait-LookupWindowHidden -Timeout ([TimeSpan]::FromSeconds(3)) | Out-Null
            }
        }
        else {
            throw "lookup query mismatch before='$before' foreground='$foreground' query='$query'"
        }
    }
    else {
        $lookupHandle = Get-LookupWindowHandle
        if ($lookupHandle -eq [IntPtr]::Zero) {
            throw "lookup window handle was not available"
        }

        [RealTargetNative]::PostEnter($lookupHandle)
        $menuItem = Wait-OutputMenuItem -Expected $expectedOutput -Timeout ([TimeSpan]::FromSeconds(3))
        if (-not $menuItem) {
            throw "output menu item was not available for '$expectedOutput'"
        }

        $invokeObj = $null
        if (-not $menuItem.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokeObj)) {
            throw "output menu item did not expose InvokePattern"
        }

        ([System.Windows.Automation.InvokePattern]$invokeObj).Invoke()

        $pasteWatch = [System.Diagnostics.Stopwatch]::StartNew()
        $final = Wait-UntilValue -Read { Get-FirstEditValueByProcessId -ProcessId $target.Id } -Expected $expectedOutput -Timeout ([TimeSpan]::FromSeconds(4))
        $pasteMs = $pasteWatch.Elapsed.TotalMilliseconds

        if ($final -ne $expectedOutput) {
            throw "paste result mismatch before='$before' query='$query' final='$final'"
        }

        Write-Output ("PASS real .NET WPF hotkey chain queryMs={0:N1} pasteMs={1:N1}" -f $queryMs, $pasteMs)
    }

} finally {
    if ($target -and -not $target.HasExited) {
        $target.Kill()
        $target.Dispose()
    }
}
