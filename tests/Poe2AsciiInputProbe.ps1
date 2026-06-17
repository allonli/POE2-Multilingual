Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class Poe2AsciiProbeNative
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

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

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        Mouse(0x0002);
        Mouse(0x0004);
    }

    public static void TapVk(ushort vk)
    {
        Key(vk, 0);
        Key(vk, 0x0002);
    }

    private static void Key(ushort vk, uint flags)
    {
        var input = new INPUT
        {
            type = 1,
            U = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = vk, dwFlags = flags }
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

function Capture-Poe2Window {
    param(
        [IntPtr]$Handle,
        [string]$Path
    )

    $rect = New-Object Poe2AsciiProbeNative+RECT
    [Poe2AsciiProbeNative]::GetWindowRect($Handle, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $hdc = $graphics.GetHdc()
        try {
            [Poe2AsciiProbeNative]::PrintWindow($Handle, $hdc, 2) | Out-Null
        } finally {
            $graphics.ReleaseHdc($hdc)
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$poe = Get-Process -Name PathOfExileSteam -ErrorAction Stop | Select-Object -First 1
$rect = New-Object Poe2AsciiProbeNative+RECT
[Poe2AsciiProbeNative]::GetWindowRect($poe.MainWindowHandle, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
$searchX = $rect.Left + [int]($width * 0.545)
$searchY = $rect.Top + [int]($height * 0.874)
$clearX = $rect.Left + [int]($width * 0.607)

[Poe2AsciiProbeNative]::ShowWindow($poe.MainWindowHandle, 5) | Out-Null
[Poe2AsciiProbeNative]::SetForegroundWindow($poe.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 200
[Poe2AsciiProbeNative]::Click($clearX, $searchY)
Start-Sleep -Milliseconds 150
[Poe2AsciiProbeNative]::Click($searchX, $searchY)
Start-Sleep -Milliseconds 150
foreach ($i in 1..10) {
    [Poe2AsciiProbeNative]::TapVk(0x08)
    Start-Sleep -Milliseconds 20
}

[Poe2AsciiProbeNative]::TapVk(0x41)
[Poe2AsciiProbeNative]::TapVk(0x42)
[Poe2AsciiProbeNative]::TapVk(0x43)
Start-Sleep -Milliseconds 250

$path = Join-Path (Resolve-Path ".\tests") "poe2-ascii-input-probe.png"
Capture-Poe2Window -Handle $poe.MainWindowHandle -Path $path
Write-Output $path
