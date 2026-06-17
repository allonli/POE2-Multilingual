using System.Windows;
using System.Windows.Input;
using Poe2DbLookup.Services;

namespace Poe2DbLookup;

public partial class SettingsWindow : Window
{
    private AppSettings _settings;
    private HotkeyGesture _activationHotkey;
    private HotkeyGesture _refreshHotkey;
    private CaptureTarget _captureTarget = CaptureTarget.None;

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings.Clone();
        _settings.StartWithWindows = StartupManager.IsStartWithWindowsEnabled();
        _activationHotkey = _settings.ActivationGesture;
        _refreshHotkey = _settings.RefreshGesture;
        Settings = _settings.Clone();

        InitializeComponent();
        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        UpdateHotkeyButtons();
    }

    public AppSettings Settings { get; private set; }

    public event EventHandler<bool>? CaptureStateChanged;

    private void ActivationHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        BeginCapture(CaptureTarget.Activation);
    }

    private void RefreshHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        BeginCapture(CaptureTarget.Refresh);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_captureTarget == CaptureTarget.None)
        {
            return;
        }

        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            SetCaptureTarget(CaptureTarget.None);
            StatusText.Text = "已取消快捷键监听。";
            UpdateHotkeyButtons();
            return;
        }

        var gesture = HotkeyGesture.FromKeyEvent(e);
        if (gesture is null)
        {
            StatusText.Text = "请按一个非修饰键。";
            return;
        }

        if (_captureTarget == CaptureTarget.Activation && !gesture.HasModifier)
        {
            StatusText.Text = "呼出快捷键需要包含 Ctrl、Alt、Shift 或 Win。";
            return;
        }

        if (_captureTarget == CaptureTarget.Activation)
        {
            _activationHotkey = gesture;
        }
        else
        {
            _refreshHotkey = gesture;
        }

        SetCaptureTarget(CaptureTarget.None);
        StatusText.Text = "已记录快捷键，保存后生效。";
        UpdateHotkeyButtons();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.ActivationGesture = _activationHotkey;
            _settings.RefreshGesture = _refreshHotkey;
            _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            StartupManager.SetStartWithWindows(_settings.StartWithWindows);
            AppSettingsStore.Save(_settings);
            Settings = _settings.Clone();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"保存失败：{ex.Message}";
        }
    }

    private void BeginCapture(CaptureTarget target)
    {
        SetCaptureTarget(target);
        StatusText.Text = "请按新的快捷键。Esc 取消。";
        UpdateHotkeyButtons();
        Focus();
        Keyboard.Focus(this);
    }

    private void SetCaptureTarget(CaptureTarget target)
    {
        var wasCapturing = _captureTarget != CaptureTarget.None;
        _captureTarget = target;
        var isCapturing = _captureTarget != CaptureTarget.None;
        if (wasCapturing != isCapturing)
        {
            CaptureStateChanged?.Invoke(this, isCapturing);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SetCaptureTarget(CaptureTarget.None);
        base.OnClosed(e);
    }

    private void UpdateHotkeyButtons()
    {
        ActivationHotkeyButton.Content = _captureTarget == CaptureTarget.Activation
            ? "按下新的呼出快捷键..."
            : _activationHotkey.ToString();
        RefreshHotkeyButton.Content = _captureTarget == CaptureTarget.Refresh
            ? "按下新的刷新快捷键..."
            : _refreshHotkey.ToString();
    }

    private enum CaptureTarget
    {
        None,
        Activation,
        Refresh
    }
}
