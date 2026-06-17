using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Poe2DbLookup.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIconService(Action openSettings, Action exitApplication)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("设置", null, (_, _) => openSettings());
        menu.Items.Add("退出", null, (_, _) => exitApplication());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "POE2 Multilingual",
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = false
        };
    }

    public void Start()
    {
        _notifyIcon.Visible = true;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }

    private static Icon LoadIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app-icon.ico"));
        if (resource is not null)
        {
            using var icon = new Icon(resource.Stream);
            return (Icon)icon.Clone();
        }

        return Environment.ProcessPath is { } path
            ? Icon.ExtractAssociatedIcon(path) ?? SystemIcons.Application
            : SystemIcons.Application;
    }
}
