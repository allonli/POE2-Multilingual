using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Poe2DbLookup.Models;
using Poe2DbLookup.Services;

namespace Poe2DbLookup;

public partial class MainWindow : Window
{
    private readonly Poe2DbClient _client = new();
    private readonly ObservableCollection<NameRecord> _results = [];
    private readonly string _cachePath = Path.Combine(AppContext.BaseDirectory, "cache", "poe2db_names.json");
    private AppSettings _settings;
    private NameIndex _index = NameIndex.Empty;
    private nint _lastForegroundWindow;
    private SettingsWindow? _settingsWindow;
    private int _activationVersion;
    private bool _isRefreshing;
    private bool _isSettingSearchText;
    private bool _userEditedAfterActivation;

    public MainWindow()
        : this(new AppSettings())
    {
    }

    public MainWindow(AppSettings settings)
    {
        _settings = settings.Clone();
        InitializeComponent();
        ResultsList.ItemsSource = _results;
    }

    public event EventHandler<AppSettings>? SettingsChanged;

    public event EventHandler<bool>? SettingsHotkeyCaptureChanged;

    public async Task InitializeAsync()
    {
        if (File.Exists(_cachePath))
        {
            try
            {
                _index = await NameIndex.LoadCacheAsync(_cachePath);
                SetStatus(BuildReadyStatus("已加载本地缓存", _index.Records.Count));
                UpdateResults();
                FocusSearchBox();
                return;
            }
            catch (Exception ex)
            {
                SetStatus($"本地缓存读取失败：{ex.Message}");
            }
        }

        await RefreshIndexAsync(allowFallbackCache: false);
        FocusSearchBox();
    }

    public void ToggleFromHotkey(nint foregroundWindow)
    {
        var focusedSelectionText = ClipboardBridge.TryReadFocusedSelectionText(out _);
        var externalWindow = IsLookupWindow(foregroundWindow) ? _lastForegroundWindow : foregroundWindow;
        if (externalWindow != 0)
        {
            _lastForegroundWindow = externalWindow;
        }

        var activationVersion = ++_activationVersion;
        _userEditedAfterActivation = false;
        ShowLookup(focusedSelectionText);

        if (string.IsNullOrWhiteSpace(focusedSelectionText) && externalWindow != 0)
        {
            _ = TryFillQueryFromClipboardFallbackAsync(externalWindow, activationVersion);
        }
    }

    private bool IsLookupWindow(nint window)
    {
        if (window == 0)
        {
            return false;
        }

        var lookupWindow = new WindowInteropHelper(this).Handle;
        return lookupWindow != 0 && window == lookupWindow;
    }

    private void ShowLookup(string? query)
    {
        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        Topmost = true;

        SetSearchText(query);

        UpdateResults();
        ActivateLookupWindow();
    }

    private async Task TryFillQueryFromClipboardFallbackAsync(nint externalWindow, int activationVersion)
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (activationVersion != _activationVersion || _userEditedAfterActivation || !IsVisible)
        {
            return;
        }

        var query = ClipboardBridge.TryReadSelectedText(externalWindow);
        if (activationVersion != _activationVersion || _userEditedAfterActivation || !IsVisible)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            SetSearchText(query);
            UpdateResults();
        }

        ActivateLookupWindow();
    }

    private void SetSearchText(string? query)
    {
        _isSettingSearchText = true;
        try
        {
            SearchBox.Text = query?.Trim() ?? string.Empty;
            SearchBox.SelectAll();
        }
        finally
        {
            _isSettingSearchText = false;
        }
    }

    private void ActivateLookupWindow()
    {
        WindowState = WindowState.Normal;
        Topmost = true;

        var windowHandle = new WindowInteropHelper(this).Handle;
        NativeMethods.BringWindowToForeground(windowHandle);
        FocusSearchBox();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusSearchBox));
    }

    private async Task RefreshIndexAsync(bool allowFallbackCache)
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        ResultsList.IsEnabled = false;
        SetStatus("正在刷新 PoE2DB 数据...");

        try
        {
            _index = await _client.RefreshAsync();
            await _index.SaveCacheAsync(_cachePath);
            SetStatus(BuildReadyStatus("刷新完成", _index.Records.Count));
            ResultsList.IsEnabled = true;
            UpdateResults();
        }
        catch (Exception ex)
        {
            if (allowFallbackCache && File.Exists(_cachePath))
            {
                try
                {
                    _index = await NameIndex.LoadCacheAsync(_cachePath);
                    ResultsList.IsEnabled = true;
                    UpdateResults();
                    SetStatus($"刷新失败，已使用本地缓存：{ex.Message}");
                    return;
                }
                catch
                {
                    // 继续显示原始刷新错误，避免用二次错误覆盖网络问题。
                }
            }

            ResultsList.IsEnabled = _index.Records.Count > 0;
            SetStatus($"错误：{ex.Message}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void UpdateResults()
    {
        _results.Clear();
        foreach (var record in _index.Search(SearchBox.Text))
        {
            _results.Add(record);
        }

        if (_results.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            ResultsList.ScrollIntoView(_results[0]);
        }
    }

    private void OpenOutputMenu()
    {
        if (ResultsList.SelectedItem is not NameRecord record)
        {
            return;
        }

        var row = ResultsList.ItemContainerGenerator.ContainerFromItem(record) as FrameworkElement;
        var menu = new ContextMenu
        {
            PlacementTarget = row ?? ResultsList,
            Placement = PlacementMode.Bottom,
            VerticalOffset = 4,
            StaysOpen = false,
            Style = (Style)FindResource("OutputContextMenuStyle")
        };

        menu.Opened += (_, _) =>
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => FocusFirstOutputItem(menu)));
        };

        AddOutputItem(menu, "简中", record.CnLabel, Poe2DbUrlBuilder.Build("cn", record.Value));
        AddOutputItem(menu, "繁中", record.TwLabel, Poe2DbUrlBuilder.Build("tw", record.Value));
        AddOutputItem(menu, "英文", record.UsLabel, Poe2DbUrlBuilder.Build("us", record.Value));
        AddOutputItem(menu, "value", record.Value, Poe2DbUrlBuilder.Build("us", record.Value));

        menu.IsOpen = true;
    }

    private void AddOutputItem(ContextMenu menu, string label, string text, string url)
    {
        var item = new MenuItem
        {
            Header = $"{label}    {text}",
            Tag = new OutputChoice(text, url),
            Style = (Style)FindResource("OutputMenuItemStyle")
        };
        ToolTipService.SetIsEnabled(item, false);
        item.Click += (_, _) =>
        {
            if (item.Tag is OutputChoice choice)
            {
                if (ShouldOpenUrlFromOutputMenu())
                {
                    OpenRecordUrl(choice.Url);
                    return;
                }

                PasteToPreviousWindow(choice.Text);
            }
        };
        menu.Items.Add(item);
    }

    private static bool ShouldOpenUrlFromOutputMenu()
    {
        return Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
    }

    private static void FocusFirstOutputItem(ContextMenu menu)
    {
        if (menu.Items.Count > 0 && menu.Items[0] is MenuItem firstItem)
        {
            firstItem.Focus();
            Keyboard.Focus(firstItem);
        }
    }

    private void PasteToPreviousWindow(string text)
    {
        HideLookup();
        ClipboardBridge.CopyAndPaste(text, _lastForegroundWindow);
    }

    private void OpenRecordUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            HideLookup();
        }
        catch (Exception ex)
        {
            SetStatus($"打开网页失败：{ex.Message}");
        }
    }

    public void OpenSettingsWindow()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsWindow = new SettingsWindow(_settings.Clone())
        {
            Owner = IsVisible ? this : null,
            Topmost = true
        };
        _settingsWindow = settingsWindow;
        settingsWindow.Closed += (_, _) => _settingsWindow = null;
        settingsWindow.CaptureStateChanged += (_, isCapturing) =>
        {
            SettingsHotkeyCaptureChanged?.Invoke(this, isCapturing);
        };
        if (settingsWindow.ShowDialog() == true)
        {
            _settings = settingsWindow.Settings.Clone();
            if (_index.Records.Count > 0)
            {
                SetStatus(BuildReadyStatus("已加载本地缓存", _index.Records.Count));
            }

            SettingsChanged?.Invoke(this, _settings.Clone());
        }
    }

    public void HideFromShortcut()
    {
        HideLookup();
    }

    private void FocusSearchBox()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
    }

    private string BuildReadyStatus(string action, int count)
    {
        return $"{action}：{count:N0} 条。{_settings.RefreshGesture} 可刷新。按住 Alt 或 Ctrl 选中跳转到词缀网页。";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSettingSearchText)
        {
            return;
        }

        _userEditedAfterActivation = true;
        UpdateResults();
    }

    private async void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideLookup();
            e.Handled = true;
            return;
        }

        if (IsCloseSearchGesture(e))
        {
            HideLookup();
            e.Handled = true;
            return;
        }

        if (_settings.RefreshGesture.Matches(e))
        {
            e.Handled = true;
            await RefreshIndexAsync(allowFallbackCache: true);
            FocusSearchBox();
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OpenOutputMenu();
            return;
        }

        if (e.Key == Key.Down && ResultsList.Items.Count > 0)
        {
            e.Handled = true;
            ResultsList.SelectedIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            return;
        }

        if (e.Key == Key.Up && ResultsList.Items.Count > 0)
        {
            e.Handled = true;
            ResultsList.SelectedIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenOutputMenu();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Topmost = true;
    }

    private void HideLookup()
    {
        Hide();
    }

    private static bool IsCloseSearchGesture(System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
    }

    private sealed record OutputChoice(string Text, string Url);
}
