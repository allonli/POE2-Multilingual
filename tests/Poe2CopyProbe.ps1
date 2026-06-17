Add-Type -AssemblyName System.Windows.Forms
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class Poe2CopyProbeNative
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

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

    public static void Drag(int startX, int startY, int endX, int endY)
    {
        SetCursorPos(startX, startY);
        Mouse(0x0002);
        for (int i = 1; i <= 12; i++)
        {
            int x = startX + ((endX - startX) * i / 12);
            int y = startY + ((endY - startY) * i / 12);
            SetCursorPos(x, y);
            System.Threading.Thread.Sleep(12);
        }
        Mouse(0x0004);
    }

    public static void VkChord(ushort modVk, ushort keyVk)
    {
        Key(modVk, 0);
        Key(keyVk, 0);
        Key(keyVk, 0x0002);
        Key(modVk, 0x0002);
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

function Format-Codepoints {
    param([string]$Value)
    if ($null -eq $Value) { return "<null>" }
    return (($Value.ToCharArray() | ForEach-Object { "U+{0:X4}" -f [int][char]$_ }) -join " ")
}

$poe = Get-Process -Name PathOfExileSteam -ErrorAction Stop | Select-Object -First 1
$rect = New-Object Poe2CopyProbeNative+RECT
[Poe2CopyProbeNative]::GetWindowRect($poe.MainWindowHandle, [ref]$rect) | Out-Null

$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
$searchX = $rect.Left + [int]($width * 0.545)
$searchY = $rect.Top + [int]($height * 0.874)
$textStartX = $rect.Left + [int]($width * 0.471)
$textEndX = $rect.Left + [int]($width * 0.555)
$firstItemX = $rect.Left + [int]($width * 0.407)
$firstItemY = $rect.Top + [int]($height * 0.099)

[Poe2CopyProbeNative]::ShowWindow($poe.MainWindowHandle, 5) | Out-Null
[Poe2CopyProbeNative]::SetForegroundWindow($poe.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 200
[Poe2CopyProbeNative]::Click($searchX, $searchY)
Start-Sleep -Milliseconds 150
[Poe2CopyProbeNative]::VkChord(0xA2, 0x41)
Start-Sleep -Milliseconds 100

$sentinel = "POE2_COPY_PROBE_$([Guid]::NewGuid().ToString('N'))"
[System.Windows.Forms.Clipboard]::SetText($sentinel)
[Poe2CopyProbeNative]::VkChord(0xA2, 0x43)
Start-Sleep -Milliseconds 600

$value = if ([System.Windows.Forms.Clipboard]::ContainsText()) {
    [System.Windows.Forms.Clipboard]::GetText()
} else {
    $null
}

Write-Output ("ctrlA copied='{0}' codepoints={1}" -f $value, (Format-Codepoints $value))

[Poe2CopyProbeNative]::SetForegroundWindow($poe.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 100
[Poe2CopyProbeNative]::Click($searchX, $searchY)
Start-Sleep -Milliseconds 100
[Poe2CopyProbeNative]::Drag($textEndX, $searchY, $textStartX, $searchY)
Start-Sleep -Milliseconds 100

$sentinel = "POE2_COPY_PROBE_$([Guid]::NewGuid().ToString('N'))"
[System.Windows.Forms.Clipboard]::SetText($sentinel)
[Poe2CopyProbeNative]::VkChord(0xA2, 0x43)
Start-Sleep -Milliseconds 600

$dragValue = if ([System.Windows.Forms.Clipboard]::ContainsText()) {
    [System.Windows.Forms.Clipboard]::GetText()
} else {
    $null
}

Write-Output ("drag copied='{0}' codepoints={1}" -f $dragValue, (Format-Codepoints $dragValue))

[Poe2CopyProbeNative]::SetForegroundWindow($poe.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 100
[Poe2CopyProbeNative]::Click($searchX, $searchY)
Start-Sleep -Milliseconds 100
[Poe2CopyProbeNative]::VkChord(0xA2, 0x41)
Start-Sleep -Milliseconds 100

$sentinel = "POE2_COPY_PROBE_$([Guid]::NewGuid().ToString('N'))"
[System.Windows.Forms.Clipboard]::SetText($sentinel)
[Poe2CopyProbeNative]::VkChord(0xA2, 0x2D)
Start-Sleep -Milliseconds 600

$insertValue = if ([System.Windows.Forms.Clipboard]::ContainsText()) {
    [System.Windows.Forms.Clipboard]::GetText()
} else {
    $null
}

Write-Output ("ctrlInsert copied='{0}' codepoints={1}" -f $insertValue, (Format-Codepoints $insertValue))

[Poe2CopyProbeNative]::SetForegroundWindow($poe.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 100
[Poe2CopyProbeNative]::Click($firstItemX, $firstItemY)
Start-Sleep -Milliseconds 300

$sentinel = "POE2_COPY_PROBE_$([Guid]::NewGuid().ToString('N'))"
[System.Windows.Forms.Clipboard]::SetText($sentinel)
[Poe2CopyProbeNative]::VkChord(0xA2, 0x43)
Start-Sleep -Milliseconds 600

$hoverValue = if ([System.Windows.Forms.Clipboard]::ContainsText()) {
    [System.Windows.Forms.Clipboard]::GetText()
} else {
    $null
}

Write-Output ("hover copied='{0}' codepoints={1}" -f $hoverValue, (Format-Codepoints $hoverValue))
