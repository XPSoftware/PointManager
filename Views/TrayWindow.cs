using System.ComponentModel;
using System.Windows;

namespace PointManager.Views;

/// <summary>
/// Base for the four main windows: closing hides the window instead of quitting the app.
/// The app only truly closes windows during <see cref="App.Exit"/>.
/// </summary>
public abstract class TrayWindow : Window
{
    protected TrayWindow()
    {
        // Keep windows off the taskbar; the tray icon represents the app.
        ShowInTaskbar = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.IsShuttingDown)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
