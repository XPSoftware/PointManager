using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using PointManager.Services;
using PointManager.Views;
using PointManager.Views.Controls;

namespace PointManager;

public partial class App : Application
{
    /// <summary>When true, windows close for real instead of hiding.</summary>
    public static bool IsShuttingDown { get; private set; }

    /// <summary>Assembly version shown on the 关于 page, e.g. "1.0.0.0".</summary>
    public static string AppVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

    private TaskbarIcon? _trayIcon;

    private StudentGroupEditorWindow? _editor;
    private SettingsWindow? _settings;
    private ScoreAdjustWindow? _adjust;
    private ScoreViewWindow? _scoreView;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Capture any unhandled error instead of silently crashing.
        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            args.Handled = !IsShuttingDown;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log(args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log(args.Exception);
            args.SetObserved();
        };

        AskAboutLegacyData();

        Store.Instance.Load();
        Store.Instance.DataChanged += () => Store.Instance.Save();        ThemeService.Apply(Store.Instance.Settings);
        ThemeService.MonitorSystemTheme(Store.Instance.Settings);
        BackupService.StartAutoBackup();

        CreateTrayIcon();

        if (!Store.Instance.Settings.StartMinimizedToTray)
            ShowScoreAdjust();

        // Non-modal so a mismatch report never blocks the tray app at startup.
        var startupCheck = Store.Instance.VerifyTotals();
        if (startupCheck.Mismatches.Count > 0)
            new TotalCheckDialog(startupCheck).Show();
    }

    /// <summary>
    /// Offers to split the old single data.json into Settings.json + Profile\ + Histories\.
    /// Declining keeps the app on the legacy file, and 设置 → 存储 can migrate later.
    /// </summary>
    private void AskAboutLegacyData()
    {
        if (!DataService.MigrationIsPending) return;

        var message =
            $"检测到旧版单文件数据：\n{DataService.LegacyFilePath}\n\n" +
            $"迁移后保存在：\n{DataService.RootDir}\n\n" +
            "旧文件将备份在Backup目录下。是否现在迁移？\n\n" +
            "选择\"稍后\"将仍按旧格式读写，之后可以在 设置 → 存储 里随时迁移。";

        if (!ConfirmDialog.Confirm("升级数据存储结构", message,
                confirmText: "迁移", cancelText: "稍后",
                icon: MaterialDesignThemes.Wpf.PackIconKind.FolderMoveOutline))
        {
            DataService.UsesLegacyFormat = true;
            return;
        }

        var migrated = DataService.TryMigrateLegacy(out var error);
        if (!migrated)
        {
            // Stay on the legacy file so nothing is lost.
            DataService.UsesLegacyFormat = true;
            MessageBox.Show(error ?? "迁移失败。", "升级数据存储结构",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (error != null)
            MessageBox.Show(error, "升级数据存储结构", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/PointManager;component/Assets/app.ico")),
            ToolTipText = "PointManager - 学生积分管理",
            ContextMenu = BuildTrayMenu(),
            MenuActivation = PopupActivationMode.RightClick,
        };
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowScoreAdjust();
    }

    private System.Windows.Controls.ContextMenu BuildTrayMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        menu.Items.Add(MakeItem("学生及小组编辑", "AccountGroupOutline", (_, _) => ShowEditor()));
        menu.Items.Add(MakeItem("加减分", "PlusMinusVariant", (_, _) => ShowScoreAdjust()));
        menu.Items.Add(MakeItem("总分查看", "FormatListNumbered", (_, _) => ShowScoreView()));
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(MakeItem("设置", "CogOutline", (_, _) => ShowSettings()));
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(MakeItem("重启", "RestartAlert", (_, _) => RestartApplication()));
        menu.Items.Add(MakeItem("退出", "ExitToApp", (_, _) => ExitApplication()));

        return menu;
    }

    private static System.Windows.Controls.MenuItem MakeItem(string header, string iconKind, RoutedEventHandler onClick)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header };
        var icon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = (MaterialDesignThemes.Wpf.PackIconKind)Enum.Parse(
                typeof(MaterialDesignThemes.Wpf.PackIconKind), iconKind),
            Width = 18,
            Height = 18,
        };
        item.Icon = icon;
        item.Click += onClick;
        return item;
    }

    // ---- Window management (single instance, hide-on-close) --------------

    private static void Reveal(Window? w)
    {
        if (w is null) return;
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Show();
        w.Activate();
        w.Focus();
    }

    public void ShowEditor()
    {
        _editor ??= new StudentGroupEditorWindow();
        Reveal(_editor);
    }

    public void ShowSettings()
    {
        _settings ??= new SettingsWindow();
        Reveal(_settings);
    }

    public void ShowScoreAdjust()
    {
        _adjust ??= new ScoreAdjustWindow();
        Reveal(_adjust);
    }

    public void ShowScoreView()
    {
        _scoreView ??= new ScoreViewWindow();
        Reveal(_scoreView);
    }

    public void ExitApplication()
    {
        if (!Store.Instance.Settings.ConfirmOnExit)
        {
            BeginShutdown();
            return;
        }

        // The tray ContextMenu lives in a Popup that still holds mouse capture when the
        // Click handler runs. A modal dialog shown before that capture is released gets
        // auto-dismissed as the popup tears down, so it just flashes. ApplicationIdle is
        // not late enough — the capture is released on a subsequent input message. Wait a
        // real slice of time so the menu is fully gone, then show the dialog.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (ConfirmDialog.Confirm("退出确认", "确定要退出 PointManager 吗？",
                    confirmText: "退出", icon: MaterialDesignThemes.Wpf.PackIconKind.ExitToApp))
                BeginShutdown();
        };
        timer.Start();
    }

    /// <summary>Launches a fresh instance of PointManager and shuts this one down.</summary>
    public void RestartApplication()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重启失败：{ex.Message}", "PointManager",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        BeginShutdown();
    }

    /// <summary>
    /// Saves state and shuts down. Shutdown is deferred to the dispatcher so the tray
    /// context-menu popup has closed first; disposing the icon mid-click can crash.
    /// </summary>
    private void BeginShutdown()
    {
        try { Store.Instance.Save(); } catch (Exception ex) { Log(ex); }
        IsShuttingDown = true;

        // Defer to idle so the tray ContextMenu popup has fully closed before we
        // dispose the icon that owns it; disposing mid-click is a known crash source.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (_trayIcon?.ContextMenu != null)
                    _trayIcon.ContextMenu.IsOpen = false;
                DisposeTray();
                Shutdown();
            }
            catch (Exception ex)
            {
                Log(ex);
                try { Environment.Exit(0); } catch { }
            }
        }), DispatcherPriority.ApplicationIdle);
    }

    private void DisposeTray()
    {
        var icon = _trayIcon;
        _trayIcon = null; // dispose exactly once; OnExit must not repeat it
        try { icon?.Dispose(); } catch (Exception ex) { Log(ex); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { Store.Instance.Save(); } catch (Exception ex) { Log(ex); }
        DisposeTray();
        base.OnExit(e);
    }

    private static void Log(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointManager");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch { /* logging must never throw */ }
    }
}
