using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PointManager.Models;
using PointManager.Services;

namespace PointManager.ViewModels;

public record SwatchOption(string Name, Brush Color);

// Value is the font family name stored in settings; empty means the app default.
public record FontOption(string Name, string Value, FontFamily Preview);

// Value is DataService.AppDataLocation / CurrentDirLocation.
public record StorageOption(string Value, string Display, string Folder);

public partial class SettingsViewModel : ObservableObject
{
    private readonly Store _store = Store.Instance;
    public AppSettings Settings => _store.Settings;

    public ObservableCollection<SwatchOption> PrimarySwatches { get; }
    public ObservableCollection<SwatchOption> AccentSwatches { get; }
    public ObservableCollection<FontOption> FontOptions { get; }
    public ObservableCollection<StorageOption> StorageOptions { get; }

    [ObservableProperty]
    private string? _newPresetText;

    public SettingsViewModel()
    {
        PrimarySwatches = new ObservableCollection<SwatchOption>(
            ThemeService.PrimarySwatches.Select(s =>
                new SwatchOption(s.Name, new SolidColorBrush(s.ExemplarHue.Color))));

        AccentSwatches = new ObservableCollection<SwatchOption>(
            ThemeService.AccentSwatches.Select(s =>
                new SwatchOption(s.Name, new SolidColorBrush(s.AccentExemplarHue!.Color))));

        FontOptions = new ObservableCollection<FontOption>
        {
            new("默认", string.Empty, ThemeService.DefaultFont)
        };
        foreach (var name in System.Windows.Media.Fonts.SystemFontFamilies
                     .Select(f => f.Source)
                     .Where(s => !string.IsNullOrWhiteSpace(s))
                     .Distinct(System.StringComparer.OrdinalIgnoreCase)
                     .OrderBy(s => s, System.StringComparer.OrdinalIgnoreCase))
        {
            FontOptions.Add(new FontOption(name, name, new FontFamily(name)));
        }

        StorageOptions = new ObservableCollection<StorageOption>
        {
            new(DataService.AppDataLocation, DataService.DisplayName(DataService.AppDataLocation),
                DataService.AppDataDir),
            new(DataService.CurrentDirLocation, DataService.DisplayName(DataService.CurrentDirLocation),
                DataService.ExeDir),
        };

        RefreshRecentSwatches();

        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppSettings.IsDarkTheme)
                or nameof(AppSettings.PrimaryColor)
                or nameof(AppSettings.SecondaryColor)
                or nameof(AppSettings.FollowSystemAccent)
                or nameof(AppSettings.FontFamily))
            {
                ThemeService.Apply(Settings);
            }
            if (e.PropertyName == nameof(AppSettings.FollowSystemAccent))
                OnPropertyChanged(nameof(IsCustomColorsVisible));
            if (e.PropertyName == nameof(AppSettings.PrimaryColor))
            {
                OnPropertyChanged(nameof(PrimaryColorBrush));
                OnPropertyChanged(nameof(PrimaryColorHex));
                OnPropertyChanged(nameof(PrimaryColorValue));
                IsPrimaryColorPickerOpen = false;
                // A preset/grid pick commits now; an HSV drag commits when its popup closes.
                if (!IsPrimaryColorPopupOpen)
                    PushRecentColor(ThemeService.GetPrimaryColor(Settings.PrimaryColor));
            }
            if (e.PropertyName == nameof(AppSettings.SecondaryColor))
            {
                OnPropertyChanged(nameof(SecondaryColorBrush));
                OnPropertyChanged(nameof(SecondaryColorHex));
                OnPropertyChanged(nameof(SecondaryColorValue));
                IsSecondaryColorPickerOpen = false;
                if (!IsSecondaryColorPopupOpen)
                    PushRecentColor(ThemeService.GetSecondaryColor(Settings.SecondaryColor));
            }
            if (e.PropertyName is nameof(AppSettings.DataLocation)
                or nameof(AppSettings.ActiveProfile))
            {
                OnPropertyChanged(nameof(DataRootPath));
                OnPropertyChanged(nameof(DataLocationName));
                OnPropertyChanged(nameof(HistoryHint));
                OnPropertyChanged(nameof(ProfilePathHint));
                OnPropertyChanged(nameof(HistoryPathHint));
            }
            _store.Save();
        };
    }

    // Whether each color card's preset grid is expanded (chevron toggle).
    [ObservableProperty]
    private bool _isPrimaryColorPickerOpen;

    [ObservableProperty]
    private bool _isSecondaryColorPickerOpen;

    // Whether the hex button's full color-picker popup is open.
    [ObservableProperty]
    private bool _isPrimaryColorPopupOpen;

    [ObservableProperty]
    private bool _isSecondaryColorPopupOpen;

    // The custom 主题色/强调色 cards are hidden while following the system accent.
    public bool IsCustomColorsVisible => !Settings.FollowSystemAccent;

    // The effective brush for the currently selected color, shown on the card's button.
    public Brush PrimaryColorBrush => new SolidColorBrush(ThemeService.GetPrimaryColor(Settings.PrimaryColor));
    public Brush SecondaryColorBrush => new SolidColorBrush(ThemeService.GetSecondaryColor(Settings.SecondaryColor));

    // "#RRGGBB" of the current color, shown on the card's button.
    public string PrimaryColorHex => ToHex(ThemeService.GetPrimaryColor(Settings.PrimaryColor));
    public string SecondaryColorHex => ToHex(ThemeService.GetSecondaryColor(Settings.SecondaryColor));

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    // TwoWay binding for the materialDesign:ColorPicker; picking stores an arbitrary hex color.
    public Color PrimaryColorValue
    {
        get => ThemeService.GetPrimaryColor(Settings.PrimaryColor);
        set => Settings.PrimaryColor = ToHex(value);
    }

    public Color SecondaryColorValue
    {
        get => ThemeService.GetSecondaryColor(Settings.SecondaryColor);
        set => Settings.SecondaryColor = ToHex(value);
    }

    // Preset-grid SelectedValue binds to the stored swatch name (or hex) string.
    public string PrimaryColor
    {
        get => Settings.PrimaryColor;
        set { Settings.PrimaryColor = value; OnPropertyChanged(); }
    }

    public string SecondaryColor
    {
        get => Settings.SecondaryColor;
        set { Settings.SecondaryColor = value; OnPropertyChanged(); }
    }

    // ---- Recently used colors -------------------------------------------------

    private const int MaxRecentColors = 10;

    private ObservableCollection<SwatchOption> _recentSwatches = new();

    // Replaced (not mutated in place) so the ColorPicker's RecentSource binding re-fires and rebuilds.
    public ObservableCollection<SwatchOption> RecentSwatches
    {
        get => _recentSwatches;
        private set { _recentSwatches = value; OnPropertyChanged(); }
    }

    private void RefreshRecentSwatches() =>
        RecentSwatches = new ObservableCollection<SwatchOption>(
            Settings.RecentColors.Select(hex =>
                new SwatchOption(hex, new SolidColorBrush(ThemeService.GetPrimaryColor(hex)))));

    // Adds a color to the front of the recent list, de-duplicated and capped, then persists.
    private void PushRecentColor(Color color)
    {
        var hex = ToHex(color);
        var recent = Settings.RecentColors;
        for (var i = recent.Count - 1; i >= 0; i--)
            if (string.Equals(recent[i], hex, System.StringComparison.OrdinalIgnoreCase))
                recent.RemoveAt(i);
        recent.Insert(0, hex);
        while (recent.Count > MaxRecentColors)
            recent.RemoveAt(recent.Count - 1);
        RefreshRecentSwatches();
        _store.Save();
    }

    // Commit the HSV picker's color to recents once its popup closes (avoids flooding mid-drag).
    partial void OnIsPrimaryColorPopupOpenChanged(bool value)
    {
        if (!value) PushRecentColor(PrimaryColorValue);
    }

    partial void OnIsSecondaryColorPopupOpenChanged(bool value)
    {
        if (!value) PushRecentColor(SecondaryColorValue);
    }

    // ---- 存储 -------------------------------------------------------------------

    public string DataRootPath => DataService.RootDir;

    public string DataLocationName => DataService.DisplayName(DataService.Location);

    public string SettingsPathHint => DataService.SettingsPath;

    public string ProfilePathHint => DataService.ProfilePath(Settings.ActiveProfile);

    public string HistoryPathHint => Path.Combine(DataService.HistoryProfileDir(Settings.ActiveProfile),
        DataService.MonthKey(DateTime.Now) + ".json");

    public string HistoryHint =>
        $"{_store.Records.Count} 条记录，按月份分文件：{DataService.HistoryProfileDir(Settings.ActiveProfile)}";

    /// <summary>True while the old data.json still needs splitting.</summary>
    public bool IsLegacyPending => DataService.MigrationIsPending || DataService.UsesLegacyFormat;

    public string LegacyHint => DataService.UsesLegacyFormat
        ? $"正在使用旧版单文件数据：{DataService.LegacyFilePath}"
        : $"检测到旧版单文件数据：{DataService.LegacyFilePath}";

    /// <summary>Re-raises the path/hint properties that depend on the active 档案 or root.</summary>
    private void RefreshStorageHints()
    {
        OnPropertyChanged(nameof(HistoryHint));
        OnPropertyChanged(nameof(ProfilePathHint));
        OnPropertyChanged(nameof(HistoryPathHint));
        OnPropertyChanged(nameof(LegacyHint));
    }

    // 档案的切换、新建、复制、重命名与删除都在学生及小组编辑窗口的「管理档案」侧边栏里
    // （Views/Controls/ProfileSidebar），这里只保留存储位置与迁移相关的入口。

    /// <summary>Splits the legacy data.json into the new layout, backing the original up.</summary>
    public bool TryMigrateLegacy(out string? message)
    {
        // The in-memory data is already the legacy content; after a successful split the same
        // objects are simply written to Settings.json + Profile\ + Histories\ from now on.
        var ok = DataService.TryMigrateLegacy(out message);
        if (ok) _store.Save();
        RefreshStorageHints();
        OnPropertyChanged(nameof(IsLegacyPending));
        OnPropertyChanged(nameof(DataRootPath));
        OnPropertyChanged(nameof(SettingsPathHint));
        return ok;
    }

    /// <summary>
    /// Switches the active storage root, copying the whole data set first. The window owns
    /// the confirmation UI; a failure returns a user-facing message and leaves the previous
    /// location in use.
    /// </summary>
    public bool TryChangeDataLocation(string target, out string? error)
    {
        // Flush first so the copy carries the latest data.
        _store.Save();

        var moved = DataService.TrySetLocation(target, overwriteExisting: true, out error);
        if (!moved) return false;

        Settings.DataLocation = DataService.Location;
        try
        {
            _store.Save();
        }
        catch (Exception ex)
        {
            error = $"数据已复制到 {DataService.RootDir}，但保存失败：{ex.Message}";
        }
        OnPropertyChanged(nameof(DataRootPath));
        OnPropertyChanged(nameof(DataLocationName));
        RefreshStorageHints();
        return true;
    }

    /// <summary>Opens the data root folder in Explorer, creating it if needed.</summary>
    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            System.IO.Directory.CreateDirectory(DataService.RootDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = DataService.RootDir,
                UseShellExecute = true,
            });
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show($"无法打开 {DataService.RootDir}\n{ex.Message}",
                "打开所在文件夹", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void AddPreset()
    {
        if (int.TryParse(NewPresetText?.Trim(), out var v) && v != 0 &&
            !Settings.PointPresets.Contains(v))
        {
            Settings.PointPresets.Add(v);
            var sorted = Settings.PointPresets.OrderBy(x => x).ToList();
            Settings.PointPresets.Clear();
            foreach (var p in sorted) Settings.PointPresets.Add(p);
        }
        NewPresetText = string.Empty;
        _store.Save();
    }

    [RelayCommand]
    private void RemovePreset(int value)
    {
        Settings.PointPresets.Remove(value);
        _store.Save();
    }

    [RelayCommand]
    private void ClearHistory()
    {
        _store.ClearHistory();
    }

    [RelayCommand]
    private void ResetPoints()
    {
        _store.ResetAllPoints();
    }
}
