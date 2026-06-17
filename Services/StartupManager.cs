using Microsoft.Win32;

namespace Poe2DbLookup.Services;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Poe2DbLookup";

    public static bool IsStartWithWindowsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public static void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(ValueName, Quote(GetExecutablePath()));
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath
            ?? AppContext.BaseDirectory;
    }

    private static string Quote(string value)
    {
        return value.StartsWith('"') && value.EndsWith('"')
            ? value
            : $"\"{value}\"";
    }
}
