using System.Globalization;
using System.Text.Json.Serialization;
using System.Windows.Input;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Poe2DbLookup.Services;

public sealed class HotkeyGesture
{
    public HotkeyGesture()
    {
    }

    public HotkeyGesture(Key key, ModifierKeys modifiers, bool leftCtrlOnly = false)
    {
        Key = key;
        Modifiers = modifiers;
        LeftCtrlOnly = leftCtrlOnly && modifiers.HasFlag(ModifierKeys.Control);
    }

    public Key Key { get; set; }

    public ModifierKeys Modifiers { get; set; }

    public bool LeftCtrlOnly { get; set; }

    [JsonIgnore]
    public int VirtualKeyCode => KeyInterop.VirtualKeyFromKey(Key);

    [JsonIgnore]
    public bool HasModifier => Modifiers != ModifierKeys.None;

    public static HotkeyGesture ParseOrDefault(string? value, HotkeyGesture fallback)
    {
        if (TryParse(value, out var gesture))
        {
            return gesture;
        }

        return fallback;
    }

    public static HotkeyGesture Parse(string value)
    {
        if (!TryParse(value, out var gesture))
        {
            throw new FormatException($"无法解析快捷键：{value}");
        }

        return gesture;
    }

    public static bool TryParse(string? value, out HotkeyGesture gesture)
    {
        gesture = new HotkeyGesture();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var modifiers = ModifierKeys.None;
        var leftCtrlOnly = false;
        Key? key = null;
        foreach (var rawPart in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.ToUpperInvariant();
            switch (part)
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ModifierKeys.Control;
                    break;
                case "LEFTCTRL":
                case "LEFTCONTROL":
                    modifiers |= ModifierKeys.Control;
                    leftCtrlOnly = true;
                    break;
                case "ALT":
                    modifiers |= ModifierKeys.Alt;
                    break;
                case "SHIFT":
                    modifiers |= ModifierKeys.Shift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= ModifierKeys.Windows;
                    break;
                default:
                    try
                    {
                        key = ParseKey(rawPart);
                    }
                    catch
                    {
                        return false;
                    }

                    break;
            }
        }

        if (key is null || IsModifierKey(key.Value))
        {
            return false;
        }

        gesture = new HotkeyGesture(key.Value, modifiers, leftCtrlOnly);
        return true;
    }

    public static HotkeyGesture? FromKeyEvent(WpfKeyEventArgs e)
    {
        var key = NormalizeKey(e);
        if (IsModifierKey(key))
        {
            return null;
        }

        var modifiers = Keyboard.Modifiers;
        var leftCtrlOnly = modifiers.HasFlag(ModifierKeys.Control)
            && Keyboard.IsKeyDown(Key.LeftCtrl)
            && !Keyboard.IsKeyDown(Key.RightCtrl);
        return new HotkeyGesture(key, modifiers, leftCtrlOnly);
    }

    public bool Matches(WpfKeyEventArgs e)
    {
        var key = NormalizeKey(e);
        if (key != Key)
        {
            return false;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != (Modifiers & ModifierKeys.Control)
            || (Keyboard.Modifiers & ModifierKeys.Alt) != (Modifiers & ModifierKeys.Alt)
            || (Keyboard.Modifiers & ModifierKeys.Shift) != (Modifiers & ModifierKeys.Shift)
            || (Keyboard.Modifiers & ModifierKeys.Windows) != (Modifiers & ModifierKeys.Windows))
        {
            return false;
        }

        return !LeftCtrlOnly || (Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl));
    }

    public bool MatchesNative(int vkCode, Func<int, bool> isKeyDown)
    {
        if (vkCode != VirtualKeyCode)
        {
            return false;
        }

        return ModifierMatches(ModifierKeys.Control, isKeyDown(0xA2) || isKeyDown(0xA3), LeftCtrlOnly ? isKeyDown(0xA2) && !isKeyDown(0xA3) : true)
            && ModifierMatches(ModifierKeys.Alt, isKeyDown(0xA4) || isKeyDown(0xA5), true)
            && ModifierMatches(ModifierKeys.Shift, isKeyDown(0xA0) || isKeyDown(0xA1), true)
            && ModifierMatches(ModifierKeys.Windows, isKeyDown(0x5B) || isKeyDown(0x5C), true);
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add(LeftCtrlOnly ? "LeftCtrl" : "Ctrl");
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(KeyToText(Key));
        return string.Join("+", parts);
    }

    private bool ModifierMatches(ModifierKeys modifier, bool isDown, bool sideMatches)
    {
        var required = Modifiers.HasFlag(modifier);
        return required ? isDown && sideMatches : !isDown;
    }

    private static Key NormalizeKey(WpfKeyEventArgs e)
    {
        return e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key
        };
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin
            or Key.System;
    }

    private static Key ParseKey(string text)
    {
        var normalized = text.Trim();
        if (normalized.Length == 1 && char.IsLetterOrDigit(normalized[0]))
        {
            normalized = normalized.ToUpper(CultureInfo.InvariantCulture);
        }

        return (Key)new KeyConverter().ConvertFromInvariantString(normalized)!;
    }

    private static string KeyToText(Key key)
    {
        return key >= Key.A && key <= Key.Z ? key.ToString() : new KeyConverter().ConvertToInvariantString(key) ?? key.ToString();
    }
}
