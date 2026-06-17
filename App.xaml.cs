using System.Windows;
using Poe2DbLookup.Services;

namespace Poe2DbLookup;

public partial class App : System.Windows.Application
{
    private AppSettings _settings = new();
    private HotkeyService? _hotkeyService;
    private TrayIconService? _trayIconService;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _settings = AppSettingsStore.Load();
        _mainWindow = new MainWindow(_settings);
        _mainWindow.SettingsChanged += (_, settings) => ApplySettings(settings);
        _mainWindow.SettingsHotkeyCaptureChanged += (_, isCapturing) =>
        {
            if (_hotkeyService is not null)
            {
                _hotkeyService.IsPaused = isCapturing;
            }
        };

        _hotkeyService = new HotkeyService();
        _hotkeyService.SetHotkey(_settings.ActivationGesture);
        _hotkeyService.ShouldHandleCtrlW = () => _mainWindow?.IsVisible == true;
        _hotkeyService.LeftCtrlEPressed += (_, foregroundWindow) =>
        {
            Dispatcher.BeginInvoke(new Action(() => _mainWindow.ToggleFromHotkey(foregroundWindow)));
        };
        _hotkeyService.CtrlWPressed += (_, _) =>
        {
            Dispatcher.BeginInvoke(new Action(() => _mainWindow.HideFromShortcut()));
        };
        _hotkeyService.Start();

        _trayIconService = new TrayIconService(
            () => Dispatcher.BeginInvoke(new Action(() => _mainWindow.OpenSettingsWindow())),
            () => Dispatcher.BeginInvoke(new Action(Shutdown)));
        _trayIconService.Start();

        await _mainWindow.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        _hotkeyService?.Dispose();
        base.OnExit(e);
    }

    private void ApplySettings(AppSettings settings)
    {
        _settings = settings.Clone();
        _hotkeyService?.SetHotkey(_settings.ActivationGesture);
    }
}
