using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PointManager.Models;

public partial class AppSettings : ObservableObject
{
    private bool _isDarkTheme;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => SetProperty(ref _isDarkTheme, value);
    }

    // MaterialDesign swatch name, e.g. "Indigo", "Teal", "DeepPurple".
    private string _primaryColor = "Indigo";
    public string PrimaryColor
    {
        get => _primaryColor;
        set => SetProperty(ref _primaryColor, value);
    }

    private string _secondaryColor = "Amber";
    public string SecondaryColor
    {
        get => _secondaryColor;
        set => SetProperty(ref _secondaryColor, value);
    }

    // When true, the primary color follows the Windows system accent color
    // and the manual 主题色 picker is ignored.
    private bool _followSystemAccent;
    public bool FollowSystemAccent
    {
        get => _followSystemAccent;
        set => SetProperty(ref _followSystemAccent, value);
    }

    // Font family name applied app-wide; empty means the MaterialDesign default.
    private string _fontFamily = string.Empty;
    public string FontFamily
    {
        get => _fontFamily;
        set => SetProperty(ref _fontFamily, value);
    }

    private bool _startMinimizedToTray;
    public bool StartMinimizedToTray
    {
        get => _startMinimizedToTray;
        set => SetProperty(ref _startMinimizedToTray, value);
    }

    private bool _confirmOnExit = true;
    public bool ConfirmOnExit
    {
        get => _confirmOnExit;
        set => SetProperty(ref _confirmOnExit, value);
    }

    // Quick +/- presets shown as buttons in the 加减分 window.
    public ObservableCollection<int> PointPresets { get; set; } = new() { 1, 2, 3, 5, 10 };

    private string _defaultReason = string.Empty;
    public string DefaultReason
    {
        get => _defaultReason;
        set => SetProperty(ref _defaultReason, value);
    }

    // 0 means unlimited; positive values cap the retained records (floor of 50).
    private int _maxHistoryRecords;
    public int MaxHistoryRecords
    {
        get => _maxHistoryRecords;
        set => SetProperty(ref _maxHistoryRecords, Math.Max(0, value));
    }

    private bool _isAutoVerifyEnabled = true;
    public bool IsAutoVerifyEnabled
    {
        get => _isAutoVerifyEnabled;
        set => SetProperty(ref _isAutoVerifyEnabled, value);
    }

    // Recently picked colors as "#RRGGBB", most recent first; shared by both color pickers.
    public ObservableCollection<string> RecentColors { get; set; } = new();

    // Where the data set lives: "AppData" (per-user folder, default) or "CurrentDir"
    // (next to PointManager.exe). Purely informational here - DataService's pointer
    // file under AppData decides the location at startup, before this value is read.
    private string _dataLocation = "AppData";
    public string DataLocation
    {
        get => _dataLocation;
        set => SetProperty(ref _dataLocation, value);
    }

    // Which file under Profile\ holds the groups and students in use, without the
    // .json extension. Multiple 档案 can exist; only this one is loaded at startup.
    private string _activeProfile = "Default";
    public string ActiveProfile
    {
        get => _activeProfile;
        set => SetProperty(ref _activeProfile, value);
    }

    private bool _isAutoBackupEnabled = true;
    public bool IsAutoBackupEnabled
    {
        get => _isAutoBackupEnabled;
        set => SetProperty(ref _isAutoBackupEnabled, value);
    }

    private int _autoBackupIntervalDays = 7;
    public int AutoBackupIntervalDays
    {
        get => _autoBackupIntervalDays;
        set => SetProperty(ref _autoBackupIntervalDays, Math.Max(1, value));
    }

    // 0 means unlimited; otherwise the oldest auto backups are deleted beyond this count.
    private int _autoBackupLimit = 16;
    public int AutoBackupLimit
    {
        get => _autoBackupLimit;
        set => SetProperty(ref _autoBackupLimit, Math.Max(0, value));
    }

    private DateTime? _lastAutoBackupTime;
    public DateTime? LastAutoBackupTime
    {
        get => _lastAutoBackupTime;
        set => SetProperty(ref _lastAutoBackupTime, value);
    }
}
