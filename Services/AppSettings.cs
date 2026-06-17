using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poe2DbLookup.Services;

public sealed class AppSettings
{
    public string ActivationHotkey { get; set; } = "LeftCtrl+E";

    public string RefreshHotkey { get; set; } = "F5";

    public bool StartWithWindows { get; set; }

    [JsonIgnore]
    public HotkeyGesture ActivationGesture
    {
        get => HotkeyGesture.ParseOrDefault(ActivationHotkey, HotkeyGesture.Parse("LeftCtrl+E"));
        set => ActivationHotkey = value.ToString();
    }

    [JsonIgnore]
    public HotkeyGesture RefreshGesture
    {
        get => HotkeyGesture.ParseOrDefault(RefreshHotkey, HotkeyGesture.Parse("F5"));
        set => RefreshHotkey = value.ToString();
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            ActivationHotkey = ActivationHotkey,
            RefreshHotkey = RefreshHotkey,
            StartWithWindows = StartWithWindows
        };
    }
}

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Poe2DbLookup",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                ?? new AppSettings();
            settings.StartWithWindows = StartupManager.IsStartWithWindowsEnabled();
            return settings;
        }
        catch
        {
            return new AppSettings
            {
                StartWithWindows = StartupManager.IsStartWithWindowsEnabled()
            };
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
