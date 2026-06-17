Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class Poe2GameVerifyNative
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

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

    public static void ScanChord(ushort modScan, ushort keyScan)
    {
        ScanDown(modScan);
        ScanDown(keyScan);
        ScanUp(keyScan);
        ScanUp(modScan);
    }

    public static void TapVk(ushort vk)
    {
        Key(vk, 0);
        Key(vk, KEYEVENTF_KEYUP);
    }

    public static void VkChord(ushort modVk, ushort keyVk)
    {
        Key(modVk, 0);
        Key(keyVk, 0);
        Key(keyVk, KEYEVENTF_KEYUP);
        Key(modVk, KEYEVENTF_KEYUP);
    }

    public static void TypeText(string value)
    {
        foreach (var ch in value)
        {
            Unicode(ch, 0x0004);
            Unicode(ch, 0x0004 | KEYEVENTF_KEYUP);
        }
    }

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        Mouse(0x0002);
        Mouse(0x0004);
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

    public static void PostEsc(IntPtr hWnd)
    {
        PostMessage(hWnd, 0x0100, new IntPtr(0x1B), new IntPtr(0x00010001));
        PostMessage(hWnd, 0x0101, new IntPtr(0x1B), new IntPtr(unchecked((int)0xC0010001)));
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

    private static void Unicode(char ch, uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT { wScan = ch, dwFlags = flags }
            }
        };

        SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void Mouse(uint flags)
    {
        var input = new INPUT
        {
            type = 0,
            U = new InputUnion
            {
                mi = new MOUSEINPUT { dwFlags = flags }
            }
        };

        SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }
}
"@

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

function Get-LookupProcess {
    return Get-Process -Name Poe2DbLookup -ErrorAction SilentlyContinue | Select-Object -First 1
}

function Get-LookupEditValue {
    $lookup = Get-LookupProcess
    if (-not $lookup) { return $null }
    return Get-FirstEditValueByProcessId -ProcessId $lookup.Id
}

function Get-LookupWindowHandle {
    $lookup = Get-LookupProcess
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

function Get-OutputMenuItem {
    param([string]$Expected)

    $lookup = Get-LookupProcess
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

function Capture-Poe2Window {
    param(
        [IntPtr]$Handle,
        [string]$Path
    )

    $rect = New-Object Poe2GameVerifyNative+RECT
    if (-not [Poe2GameVerifyNative]::GetWindowRect($Handle, [ref]$rect)) {
        throw "could not read PoE2 window rect"
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "invalid PoE2 window size ${width}x${height}"
    }

    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $hdc = $graphics.GetHdc()
        try {
            [Poe2GameVerifyNative]::PrintWindow($Handle, $hdc, 2) | Out-Null
        } finally {
            $graphics.ReleaseHdc($hdc)
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Set-ClipboardText {
    param([string]$Text)
    [System.Windows.Forms.Clipboard]::SetText($Text, [System.Windows.Forms.TextDataFormat]::UnicodeText)
}

function Get-ClipboardTextSafe {
    if ([System.Windows.Forms.Clipboard]::ContainsText()) {
        return [System.Windows.Forms.Clipboard]::GetText([System.Windows.Forms.TextDataFormat]::UnicodeText)
    }

    return $null
}

function Wait-ClipboardText {
    param(
        [string]$Expected,
        [TimeSpan]$Timeout
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed -lt $Timeout) {
        $value = Get-ClipboardTextSafe
        if ($value -eq $Expected) { return $value }
        Start-Sleep -Milliseconds 50
    }

    return (Get-ClipboardTextSafe)
}

$sourceText = "$([char]0x8FDC)$([char]0x5C04)"
$expectedOutput = "$sourceText I"
$sentinel = "POE2_VERIFY_SENTINEL_$([Guid]::NewGuid().ToString('N'))"
$screenshotPath = Join-Path (Resolve-Path ".\tests") "poe2-game-input-after-paste.png"
$preparedScreenshotPath = Join-Path (Resolve-Path ".\tests") "poe2-game-input-prepared.png"

$poe = Get-Process -Name PathOfExileSteam -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $poe -or $poe.MainWindowHandle.ToInt64() -eq 0) {
    throw "PathOfExileSteam window was not available"
}

if (-not (Get-LookupProcess)) {
    throw "Poe2DbLookup process was not running"
}

$initialLookupHandle = Get-LookupWindowHandle
if ($initialLookupHandle -ne [IntPtr]::Zero) {
    [Poe2GameVerifyNative]::PostEsc($initialLookupHandle)
    Start-Sleep -Milliseconds 150
}

$poeHandle = $poe.MainWindowHandle
[Poe2GameVerifyNative]::ShowWindow($poeHandle, 5) | Out-Null
[Poe2GameVerifyNative]::SetForegroundWindow($poeHandle) | Out-Null
Start-Sleep -Milliseconds 350

$foregroundBeforeInput = [Poe2GameVerifyNative]::ForegroundTitle()

# Prepare selected text in the Anjie currency search box.
$rectForClick = New-Object Poe2GameVerifyNative+RECT
if (-not [Poe2GameVerifyNative]::GetWindowRect($poeHandle, [ref]$rectForClick)) {
    throw "could not read PoE2 window rect before input"
}

$windowWidth = $rectForClick.Right - $rectForClick.Left
$windowHeight = $rectForClick.Bottom - $rectForClick.Top
$searchX = $rectForClick.Left + [int]($windowWidth * 0.545)
$searchY = $rectForClick.Top + [int]($windowHeight * 0.874)
$clearX = $rectForClick.Left + [int]($windowWidth * 0.607)
[Poe2GameVerifyNative]::Click($searchX, $searchY)
Start-Sleep -Milliseconds 120
[Poe2GameVerifyNative]::VkChord(0xA2, 0x46)
Start-Sleep -Milliseconds 120
[Poe2GameVerifyNative]::Click($searchX, $searchY)
Start-Sleep -Milliseconds 250
[Poe2GameVerifyNative]::Click($clearX, $searchY)
Start-Sleep -Milliseconds 120
[Poe2GameVerifyNative]::Click($searchX, $searchY)
Start-Sleep -Milliseconds 120
[Poe2GameVerifyNative]::TypeText($sourceText)
Start-Sleep -Milliseconds 150
Capture-Poe2Window -Handle $poeHandle -Path $preparedScreenshotPath
[Poe2GameVerifyNative]::VkChord(0xA2, 0x41)
Start-Sleep -Milliseconds 100

$hotkeyWatch = [System.Diagnostics.Stopwatch]::StartNew()
[Poe2GameVerifyNative]::ScanChord(0x1D, 0x12)
$query = Wait-UntilValue -Read { Get-LookupEditValue } -Expected $sourceText -Timeout ([TimeSpan]::FromSeconds(5))
$queryMs = $hotkeyWatch.Elapsed.TotalMilliseconds
if ($query -ne $sourceText) {
    Capture-Poe2Window -Handle $poeHandle -Path $screenshotPath
    throw "lookup query mismatch foregroundBeforeInput='$foregroundBeforeInput' query='$query' prepared='$preparedScreenshotPath' screenshot='$screenshotPath'"
}

$lookupHandle = Get-LookupWindowHandle
if ($lookupHandle -eq [IntPtr]::Zero) {
    throw "lookup window handle was not available"
}

[Poe2GameVerifyNative]::PostEnter($lookupHandle)
$menuItem = Wait-OutputMenuItem -Expected $expectedOutput -Timeout ([TimeSpan]::FromSeconds(3))
if (-not $menuItem) {
    throw "output menu item was not available for '$expectedOutput'"
}

$invokeObj = $null
if (-not $menuItem.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokeObj)) {
    throw "output menu item did not expose InvokePattern"
}

$pasteWatch = [System.Diagnostics.Stopwatch]::StartNew()
([System.Windows.Automation.InvokePattern]$invokeObj).Invoke()
Start-Sleep -Milliseconds 600
Capture-Poe2Window -Handle $poeHandle -Path $screenshotPath

[Poe2GameVerifyNative]::SetForegroundWindow($poeHandle) | Out-Null
Start-Sleep -Milliseconds 150
Set-ClipboardText -Text $sentinel
[Poe2GameVerifyNative]::VkChord(0xA2, 0x41)
Start-Sleep -Milliseconds 100
[Poe2GameVerifyNative]::VkChord(0xA2, 0x43)
$copiedBack = Wait-ClipboardText -Expected $expectedOutput -Timeout ([TimeSpan]::FromSeconds(3))
$pasteMs = $pasteWatch.Elapsed.TotalMilliseconds

if ($copiedBack -ne $expectedOutput) {
    throw "game paste verification mismatch copiedBack='$copiedBack' expected='$expectedOutput' screenshot='$screenshotPath'"
}

[Poe2GameVerifyNative]::TapVk(0x1B)
Write-Output ("PASS PoE2 game input hotkey chain queryMs={0:N1} pasteVerifyMs={1:N1} screenshot={2}" -f $queryMs, $pasteMs, $screenshotPath)
