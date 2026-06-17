using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using WpfClipboard = System.Windows.Clipboard;

namespace Poe2DbLookup.Services;

public static class ClipboardBridge
{
    private const string ClipboardSentinelPrefix = "__POE2_LOOKUP_COPY_SENTINEL__";
    private static readonly TimeSpan ForegroundTimeout = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan ClipboardTimeout = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan ClipboardSetTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan InputSettleTimeout = TimeSpan.FromMilliseconds(35);

    public static string? TryReadFocusedSelectionText()
    {
        return TryReadFocusedSelectionText(out _);
    }

    public static string? TryReadFocusedSelectionText(out bool automationSupported)
    {
        automationSupported = false;
        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null)
            {
                return null;
            }

            if (!focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out var patternObject))
            {
                return null;
            }

            automationSupported = true;
            var textPattern = (TextPattern)patternObject;
            foreach (var range in textPattern.GetSelection())
            {
                var selectedText = range.GetText(-1);
                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    return selectedText.Trim();
                }
            }
        }
        catch
        {
            // 部分旧控件不完整支持 UI Automation，失败时回退到剪贴板读取。
        }

        return null;
    }

    public static string? TryReadSelectedText(nint foregroundWindow)
    {
        if (foregroundWindow == 0)
        {
            return null;
        }

        System.Windows.IDataObject? previousData = null;
        string? sentinel = null;
        try
        {
            previousData = WpfClipboard.GetDataObject();
            sentinel = ClipboardSentinelPrefix + Guid.NewGuid().ToString("N");
            if (!SetClipboardTextWithRetry(sentinel))
            {
                return null;
            }

            var clipboardSequence = NativeMethods.GetClipboardSequenceNumber();

            NativeMethods.ActivateWindow(foregroundWindow);
            WaitForInputSettle();
            // 热键触发时修饰键可能仍处于按下状态，先释放再发送 Ctrl+C。
            NativeMethods.ReleaseKey(NativeMethods.VirtualKey.LeftControl);
            NativeMethods.ReleaseKey(NativeMethods.VirtualKey.LeftAlt);
            NativeMethods.SendModifiedKey(NativeMethods.VirtualKey.LeftControl, NativeMethods.VirtualKey.C);
            var selectedText = WaitForClipboardText(clipboardSequence);

            TryRestoreClipboard(previousData);

            var result = !string.IsNullOrWhiteSpace(selectedText) && selectedText != sentinel
                ? selectedText.Trim()
                : null;
            return result;
        }
        catch
        {
            if (previousData is not null)
            {
                TryRestoreClipboard(previousData);
            }

            return null;
        }
    }

    public static void CopyAndPaste(string text, nint targetWindow)
    {
        if (targetWindow == 0)
        {
            SetClipboardTextWithRetry(text);
            return;
        }

        NativeMethods.ActivateWindow(targetWindow);
        WaitForInputSettle();
        if (TryPasteIntoFocusedAutomationElement(text, targetWindow))
        {
            return;
        }

        if (!SetClipboardTextWithRetry(text))
        {
            return;
        }

        NativeMethods.ActivateWindow(targetWindow);
        WaitForInputSettle();
        NativeMethods.ReleaseKey(NativeMethods.VirtualKey.LeftControl);
        NativeMethods.ReleaseKey(NativeMethods.VirtualKey.LeftAlt);
        NativeMethods.SendModifiedKey(NativeMethods.VirtualKey.LeftControl, NativeMethods.VirtualKey.V);
    }

    private static string? WaitForClipboardText(uint previousSequence)
    {
        string? selectedText = null;
        InteractionWaiter.WaitUntil(
            () =>
            {
                if (NativeMethods.GetClipboardSequenceNumber() == previousSequence)
                {
                    return false;
                }

                try
                {
                    selectedText = WpfClipboard.ContainsText() ? WpfClipboard.GetText() : null;
                    return true;
                }
                catch
                {
                    return false;
                }
            },
            ClipboardTimeout,
            TimeSpan.FromMilliseconds(8));

        return selectedText;
    }

    private static void WaitForInputSettle()
    {
        Thread.Sleep(InputSettleTimeout);
    }

    private static bool TryPasteIntoFocusedAutomationElement(string text, nint targetWindow)
    {
        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null
                || !IsElementInsideWindow(focusedElement, targetWindow)
                || !focusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject))
            {
                return false;
            }

            var valuePattern = (ValuePattern)valuePatternObject;
            if (valuePattern.Current.IsReadOnly)
            {
                return false;
            }

            var currentValue = valuePattern.Current.Value ?? string.Empty;
            if (!ShouldReplaceWholeAutomationValue(focusedElement, currentValue))
            {
                return false;
            }

            valuePattern.SetValue(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ShouldReplaceWholeAutomationValue(AutomationElement element, string currentValue)
    {
        if (currentValue.Length == 0)
        {
            return true;
        }

        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var textPatternObject))
        {
            return false;
        }

        var textPattern = (TextPattern)textPatternObject;
        return textPattern.GetSelection()
            .Select(range => NormalizeAutomationText(range.GetText(-1)))
            .Any(selectedText => string.Equals(selectedText, NormalizeAutomationText(currentValue), StringComparison.Ordinal));
    }

    private static bool IsElementInsideWindow(AutomationElement element, nint targetWindow)
    {
        var walker = TreeWalker.RawViewWalker;
        var current = element;
        while (current is not null)
        {
            if ((nint)current.Current.NativeWindowHandle == targetWindow)
            {
                return true;
            }

            current = walker.GetParent(current);
        }

        return false;
    }

    private static string NormalizeAutomationText(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\r', '\n');
    }

    private static bool SetClipboardTextWithRetry(string value)
    {
        return InteractionWaiter.WaitUntil(
            () =>
            {
                try
                {
                    WpfClipboard.SetText(value);
                    return true;
                }
                catch (COMException)
                {
                    return false;
                }
            },
            ClipboardSetTimeout,
            TimeSpan.FromMilliseconds(8));
    }

    private static void TryRestoreClipboard(System.Windows.IDataObject? previousData)
    {
        if (previousData is null)
        {
            return;
        }

        InteractionWaiter.WaitUntil(
            () =>
            {
                try
                {
                    WpfClipboard.SetDataObject(previousData, true);
                    return true;
                }
                catch (COMException)
                {
                    return false;
                }
            },
            ClipboardSetTimeout,
            TimeSpan.FromMilliseconds(8));
    }
}
