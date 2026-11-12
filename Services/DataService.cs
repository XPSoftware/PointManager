using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using PointManager.Models;

namespace PointManager.Services;

/// <summary>Everything the app keeps, as one object (also the legacy data.json shape).</summary>
public class PersistedData
{
    public List<Group> Groups { get; set; } = new();
    public List<Student> Students { get; set; } = new();
    public List<PointRecord> Records { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}

/// <summary>One 档案 file (Profile\&lt;name&gt;.json): groups plus students.</summary>
public sealed class ProfileData
{
    public List<Group> Groups { get; set; } = new();
    public List<Student> Students { get; set; } = new();
}

/// <summary>One history file (Histories\&lt;档案&gt;\yyyy-MM.json): that month's records.</summary>
public sealed class HistoryData
{
    public string Month { get; set; } = string.Empty;
    public List<PointRecord> Records { get; set; } = new();
}

/// <summary>
/// Storage layer. The data set lives under one root folder (the executable's folder for
/// Release builds, AppData for Debug builds, unless the pointer file says otherwise)
/// split into three parts:
///   Settings.json                      application settings
///   Profile\&lt;档案&gt;.json               groups + students of one 档案
///   Histories\&lt;档案&gt;\yyyy-MM.json   that 档案's point records, one file per month
/// A pointer file under AppData records which root is active, because startup must find
/// the data before reading it. The old single data.json is still supported and can be
/// migrated into the layout above (the original moves to Backup\&lt;时间&gt;\).
/// </summary>
public static class DataService
{
    public const string AppDataLocation = "AppData";
    public const string CurrentDirLocation = "CurrentDir";
    public const string DefaultProfileName = "Default";

    // Where a brand new install keeps its data when no pointer file exists yet:
    // Debug runs stay in AppData so development never touches the exe folder,
    // Release ships portable by default (data next to the executable).
#if DEBUG
    public const string DefaultLocation = AppDataLocation;
#else
    public const string DefaultLocation = CurrentDirLocation;
#endif

    public const string SettingsFileName = "Settings.json";
    public const string ProfileFolderName = "Profile";
    public const string HistoryFolderName = "Histories";
    public const string AvatarFolderName = "Avatars";
    public const string BackupFolderName = "Backup";
    public const string LegacyFileName = "data.json";

    // Fixed diagnostic/pointer folder. crash.log must stay reachable even when the data
    // root moves next to the executable, so this never depends on Location.
    public static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointManager");

    // "当前目录" means the executable's folder. AppContext.BaseDirectory is stable,
    // while Environment.CurrentDirectory changes with how the app was launched.
    public static string ExeDir => (AppContext.BaseDirectory ?? Environment.CurrentDirectory)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string PointerPath => Path.Combine(AppDataDir, "storage.location");

    /// <summary>Active storage location; the pointer file is authoritative.</summary>
    public static string Location { get; private set; } = ReadPointer();

    /// <summary>True while the user has kept the old single data.json instead of migrating.</summary>
    public static bool UsesLegacyFormat { get; set; }

    // ---- paths ---------------------------------------------------------------

    public static string RootDir => DirFor(Location);

    public static string SettingsPath => Path.Combine(RootDir, SettingsFileName);

    public static string ProfileDir => Path.Combine(RootDir, ProfileFolderName);

    public static string HistoryDir => Path.Combine(RootDir, HistoryFolderName);

    public static string AvatarDir => Path.Combine(RootDir, AvatarFolderName);

    public static string BackupDir => Path.Combine(RootDir, BackupFolderName);

    public static string LegacyFilePath => Path.Combine(RootDir, LegacyFileName);

    public static bool LegacyDataExists => File.Exists(LegacyFilePath);

    public static string DirFor(string? location) =>
        Normalize(location) == CurrentDirLocation ? ExeDir : AppDataDir;

    public static string ProfilePath(string? name) =>
        Path.Combine(ProfileDir, SanitizeName(name) + ".json");

    public static string HistoryProfileDir(string? name) =>
        Path.Combine(HistoryDir, SanitizeName(name));

    public static string AvatarProfileDir(string? name) =>
        Path.Combine(AvatarDir, SanitizeName(name));

    public static string MonthKey(DateTime time) => time.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static string Normalize(string? location) =>
        string.Equals(location, CurrentDirLocation, StringComparison.OrdinalIgnoreCase)
            ? CurrentDirLocation
            : AppDataLocation;

    public static string DisplayName(string? location) =>
        Normalize(location) == CurrentDirLocation ? "当前目录（程序所在目录）" : "AppData（用户数据目录）";

    /// <summary>Profile names available on disk, Default first.</summary>
    public static List<string> ProfileNames()
    {
        try
        {
            var names = Directory.Exists(ProfileDir)
                ? Directory.GetFiles(ProfileDir, "*.json")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => string.Equals(n, DefaultProfileName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(n => n, StringComparer.Ordinal)
                    .ToList()
                : new List<string>();
            return names.Count > 0 ? names : new List<string> { DefaultProfileName };
        }
        catch
        {
            return new List<string> { DefaultProfileName };
        }
    }

    /// <summary>Strips characters that are illegal in a file name; never returns empty.</summary>
    public static string SanitizeName(string? raw)
    {
        var name = (raw ?? string.Empty).Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "_");
        name = name.Trim('.', ' ');
        return name.Length == 0 ? DefaultProfileName : name;
    }

    /// <summary>True when the root already holds any part of a data set.</summary>
    public static bool HasData(string? location)
    {
        var root = DirFor(location);
        try
        {
            return File.Exists(Path.Combine(root, SettingsFileName)) ||
                   File.Exists(Path.Combine(root, LegacyFileName)) ||
                   Directory.Exists(Path.Combine(root, ProfileFolderName)) ||
                   Directory.Exists(Path.Combine(root, HistoryFolderName));
        }
        catch
        {
            return false;
        }
    }

    // ---- json plumbing -------------------------------------------------------

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static void WriteJson(string path, object value)
    {
        var dir = Path.GetDirectoryName(path) ?? RootDir;
        Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }

    private static T? ReadJson<T>(string path) where T : class
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch
        {
            // Corrupt or unreadable file: the caller falls back to defaults.
        }
        return null;
    }

    // ---- load / save ---------------------------------------------------------

    public static AppSettings LoadSettings()
    {
        var settings = ReadJson<AppSettings>(SettingsPath) ?? new AppSettings();
        settings.DataLocation = Location;
        if (string.IsNullOrWhiteSpace(settings.ActiveProfile))
            settings.ActiveProfile = DefaultProfileName;
        return settings;
    }

    /// <summary>Reads the whole data set from whichever layout is active.</summary>
    public static PersistedData Load()
    {
        var settings = LoadSettings();

        if (UsesLegacyFormat)
        {
            var legacy = ReadJson<PersistedData>(LegacyFilePath) ?? SeedAll();
            legacy.Settings ??= new AppSettings();
            legacy.Settings.DataLocation = Location;
            return legacy;
        }

        var name = SanitizeName(settings.ActiveProfile);
        settings.ActiveProfile = name;

        // Brand new install: seed sample data only when the root holds no data at all,
        // otherwise start empty (an emptied 档案 must stay empty).
        var freshInstall = !HasData(Location);
        if (!File.Exists(ProfilePath(name)))
            WriteJson(ProfilePath(name), freshInstall ? SeedProfile() : new ProfileData());

        var profile = ReadJson<ProfileData>(ProfilePath(name)) ?? new ProfileData();

        return new PersistedData
        {
            Groups = profile.Groups,
            Students = profile.Students,
            Records = LoadHistory(name),
            Settings = settings,
        };
    }

    /// <summary>Writes settings, the active 档案 and its monthly history files.</summary>
    public static void Save(PersistedData data)
    {
        if (UsesLegacyFormat)
        {
            WriteJson(LegacyFilePath, data);
            return;
        }

        var name = SanitizeName(data.Settings.ActiveProfile);
        data.Settings.ActiveProfile = name;

        WriteJson(SettingsPath, data.Settings);
        WriteJson(ProfilePath(name), new ProfileData
        {
            Groups = data.Groups,
            Students = data.Students,
        });
        WriteHistory(name, data.Records);
    }

    /// <summary>Loads one 档案's groups and students, creating the file when missing.</summary>
    public static ProfileData LoadProfile(string name)
    {
        name = SanitizeName(name);
        var profile = ReadJson<ProfileData>(ProfilePath(name));
        if (profile is null)
        {
            profile = new ProfileData();
            WriteJson(ProfilePath(name), profile);
        }
        return profile;
    }

    /// <summary>All records of one 档案, newest first, merged across month files.</summary>
    public static List<PointRecord> LoadHistory(string name)
    {
        var dir = HistoryProfileDir(name);
        var records = new List<PointRecord>();
        try
        {
            if (!Directory.Exists(dir)) return records;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var data = ReadJson<HistoryData>(file);
                if (data?.Records is { Count: > 0 })
                    records.AddRange(data.Records);
            }
        }
        catch
        {
            // Unreadable month file: skip it rather than losing the rest.
        }
        return records.OrderByDescending(r => r.Time).ToList();
    }

    /// <summary>One file per month; month files that became empty are removed.</summary>
    public static void WriteHistory(string name, IEnumerable<PointRecord> records)
    {
        var list = records as IList<PointRecord> ?? records.ToList();
        var dir = HistoryProfileDir(name);
        Directory.CreateDirectory(dir);

        var byMonth = list
            .GroupBy(r => MonthKey(r.Time))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Time).ToList());

        foreach (var pair in byMonth)
            WriteJson(Path.Combine(dir, pair.Key + ".json"),
                new HistoryData { Month = pair.Key, Records = pair.Value });

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            if (byMonth.ContainsKey(Path.GetFileNameWithoutExtension(file))) continue;
            try { File.Delete(file); } catch { /* stale file we cannot remove is harmless */ }
        }
    }

    // ---- profile management --------------------------------------------------

    public static bool TryCreateProfile(string rawName, out string created, out string? error)
    {
        error = null;
        created = SanitizeName(rawName);
        if (string.IsNullOrWhiteSpace(rawName))
        {
            error = "档案名称不能为空。";
            return false;
        }
        if (File.Exists(ProfilePath(created)))
        {
            error = $"档案「{created}」已存在。";
            return false;
        }
        try
        {
            WriteJson(ProfilePath(created), new ProfileData());
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Deletes a 档案 together with its history files. The last 档案 is kept.</summary>
    public static bool TryDeleteProfile(string name, out string? error)
    {
        error = null;
        name = SanitizeName(name);

        var remaining = ProfileNames()
            .Where(n => !string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (remaining.Count == 0)
        {
            error = "至少需要保留一个档案。";
            return false;
        }

        try
        {
            if (File.Exists(ProfilePath(name))) File.Delete(ProfilePath(name));

            var historyDir = HistoryProfileDir(name);
            if (Directory.Exists(historyDir)) Directory.Delete(historyDir, true);

            var avatarDir = AvatarProfileDir(name);
            if (Directory.Exists(avatarDir)) Directory.Delete(avatarDir, true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Copies a 档案 together with its history files under a new name.</summary>
    public static bool TryDuplicateProfile(string source, string rawName, out string created, out string? error)
    {
        created = SanitizeName(rawName);
        error = null;
        source = SanitizeName(source);

        if (string.IsNullOrWhiteSpace(rawName))
        {
            error = "档案名称不能为空。";
            return false;
        }
        if (!File.Exists(ProfilePath(source)))
        {
            error = $"档案「{source}」不存在。";
            return false;
        }
        if (File.Exists(ProfilePath(created)))
        {
            error = $"档案「{created}」已存在。";
            return false;
        }

        try
        {
            File.Copy(ProfilePath(source), ProfilePath(created), overwrite: false);

            var sourceHistory = HistoryProfileDir(source);
            if (Directory.Exists(sourceHistory))
                CopyDirectory(sourceHistory, HistoryProfileDir(created));

            // Avatar paths embed the profile name; the copy must point at its own folder.
            var sourceAvatars = AvatarProfileDir(source);
            if (Directory.Exists(sourceAvatars))
            {
                CopyDirectory(sourceAvatars, AvatarProfileDir(created));
                if (ReadJson<ProfileData>(ProfilePath(created)) is { } copied)
                {
                    foreach (var s in copied.Students)
                        if (!string.IsNullOrWhiteSpace(s.AvatarPath))
                            s.AvatarPath = Path.Combine(AvatarFolderName, created, Path.GetFileName(s.AvatarPath));
                    WriteJson(ProfilePath(created), copied);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            // Undo a half-finished copy so no stray 档案 is left behind.
            try
            {
                if (File.Exists(ProfilePath(created))) File.Delete(ProfilePath(created));
            }
            catch { /* best effort */ }
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Renames a 档案 file and moves its history folder with it. The caller keeps
    /// Settings.ActiveProfile in sync when the renamed 档案 is the active one.
    /// </summary>
    public static bool TryRenameProfile(string oldName, string rawName, out string renamed, out string? error)
    {
        renamed = SanitizeName(rawName);
        error = null;
        oldName = SanitizeName(oldName);

        if (string.IsNullOrWhiteSpace(rawName))
        {
            error = "档案名称不能为空。";
            return false;
        }
        if (string.Equals(oldName, renamed, StringComparison.OrdinalIgnoreCase))
        {
            error = "名称没有变化。";
            return false;
        }
        if (!File.Exists(ProfilePath(oldName)))
        {
            error = $"档案「{oldName}」不存在。";
            return false;
        }
        if (File.Exists(ProfilePath(renamed)))
        {
            error = $"档案「{renamed}」已存在。";
            return false;
        }

        try
        {
            File.Move(ProfilePath(oldName), ProfilePath(renamed));

            var sourceHistory = HistoryProfileDir(oldName);
            if (Directory.Exists(sourceHistory))
                Directory.Move(sourceHistory, HistoryProfileDir(renamed));
            return true;
        }
        catch (Exception ex)
        {
            // Roll back a half-done rename so the 档案 is never left under two names.
            try
            {
                if (File.Exists(ProfilePath(renamed)) && !File.Exists(ProfilePath(oldName)))
                    File.Move(ProfilePath(renamed), ProfilePath(oldName));
            }
            catch { /* best effort */ }
            error = ex.Message;
            return false;
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }

    // ---- legacy migration ----------------------------------------------------

    /// <summary>True when a single-file data.json is waiting to be split up.</summary>
    public static bool MigrationIsPending => LegacyDataExists && !File.Exists(SettingsPath);

    /// <summary>
    /// Splits the old data.json into Settings.json + Profile\ + Histories\ and moves the
    /// original into Backup\&lt;yyyy-MM-dd_HHmmss&gt;\. Existing new-format files are replaced.
    /// </summary>
    public static bool TryMigrateLegacy(out string? error)
    {
        error = null;
        if (!LegacyDataExists)
        {
            UsesLegacyFormat = false;
            return true;
        }

        var legacy = ReadJson<PersistedData>(LegacyFilePath);
        if (legacy is null)
        {
            error = $"无法读取 {LegacyFilePath}，未做任何更改。";
            return false;
        }

        legacy.Settings ??= new AppSettings();
        legacy.Settings.DataLocation = Location;
        legacy.Settings.ActiveProfile = DefaultProfileName;

        UsesLegacyFormat = false;
        try
        {
            WriteJson(SettingsPath, legacy.Settings);
            WriteJson(ProfilePath(DefaultProfileName),
                new ProfileData { Groups = legacy.Groups, Students = legacy.Students });
            WriteHistory(DefaultProfileName, legacy.Records);
        }
        catch (Exception ex)
        {
            error = $"迁移数据失败：{ex.Message}";
            return false;
        }

        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        var folder = Path.Combine(BackupDir, stamp);
        try
        {
            Directory.CreateDirectory(folder);
            File.Move(LegacyFilePath, Path.Combine(folder, LegacyFileName), overwrite: true);
        }
        catch (Exception ex)
        {
            // Data is already usable in the new layout; keep the original where it is.
            error = $"数据已迁移到 {RootDir}，但旧文件未能备份到 Backup 目录：{ex.Message}";
        }
        return true;
    }

    // ---- location switch -----------------------------------------------------

    /// <summary>
    /// Switches the active root folder, copying the whole data set (settings, 档案, 记录,
    /// backups) so nothing is left behind. The caller must have flushed data first.
    /// On failure the previous location stays in use.
    /// </summary>
    public static bool TrySetLocation(string? target, bool overwriteExisting, out string? error)
    {
        error = null;
        var next = Normalize(target);
        if (next == Location) return true;

        var sourceRoot = RootDir;
        var targetRoot = DirFor(next);

        if (HasData(next) && !overwriteExisting)
        {
            error = $"{targetRoot} 已存在数据。";
            return false;
        }

        if (!TryPrepareDirectory(targetRoot, out error)) return false;

        try
        {
            CopyDataSet(sourceRoot, targetRoot);
        }
        catch (Exception ex)
        {
            error = $"复制数据失败：{ex.Message}";
            return false;
        }

        if (!TryWritePointer(next))
        {
            // Without the pointer the next launch would read the old location again.
            error = $"无法写入位置标记文件 {PointerPath}，存储位置未更改。";
            return false;
        }

        Location = next;
        return true;
    }

    /// <summary>
    /// Copies only the data parts (settings, legacy file, 档案, 记录, 备份) - the root can be
    /// the executable's folder, which also holds the program binaries.
    /// </summary>
    private static void CopyDataSet(string sourceRoot, string targetRoot)
    {
        if (!Directory.Exists(sourceRoot)) return;

        foreach (var file in new[] { SettingsFileName, LegacyFileName })
        {
            var source = Path.Combine(sourceRoot, file);
            if (!File.Exists(source)) continue;
            Directory.CreateDirectory(targetRoot);
            File.Copy(source, Path.Combine(targetRoot, file), overwrite: true);
        }

        foreach (var folder in new[] { ProfileFolderName, HistoryFolderName, AvatarFolderName, BackupFolderName })
        {
            var sourceDir = Path.Combine(sourceRoot, folder);
            if (!Directory.Exists(sourceDir)) continue;

            var targetDir = Path.Combine(targetRoot, folder);
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                var destination = Path.Combine(targetDir, Path.GetRelativePath(sourceDir, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }
        }
    }

    private static bool TryPrepareDirectory(string dir, out string? error)
    {
        error = null;
        try
        {
            Directory.CreateDirectory(dir);
            // Probe write access - the exe folder may sit under Program Files.
            var probe = Path.Combine(dir, $".pm-write-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex)
        {
            error = $"目标目录不可写：{dir}\n{ex.Message}";
            return false;
        }
    }

    private static bool TryWritePointer(string location)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(PointerPath, location);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ReadPointer()
    {
        try
        {
            if (File.Exists(PointerPath))
            {
                if (Normalize(File.ReadAllText(PointerPath).Trim()) == CurrentDirLocation)
                    return CurrentDirLocation;
                return AppDataLocation;
            }
        }
        catch
        {
            // Unreadable pointer: fall back to the build's default location.
        }
        return DefaultLocation;
    }

    // ---- first-run sample data ----------------------------------------------

    private static ProfileData SeedProfile()
    {
        var g1 = new Group { Name = "第一组" };
        var g2 = new Group { Name = "第二组" };
        return new ProfileData
        {
            Groups = new List<Group> { g1, g2 },
            Students = new List<Student>
            {
                new() { Name = "张三", GroupId = g1.Id, Points = 0, Gender = "男", Remark = "班长" },
                new() { Name = "李四", GroupId = g1.Id, Points = 0, Gender = "女" },
                new() { Name = "王五", GroupId = g2.Id, Points = 0, Gender = "男" },
                new() { Name = "赵六", GroupId = g2.Id, Points = 0, Gender = "女" },
            },
        };
    }

    private static PersistedData SeedAll()
    {
        var profile = SeedProfile();
        return new PersistedData
        {
            Groups = profile.Groups,
            Students = profile.Students,
            Records = new List<PointRecord>(),
            Settings = new AppSettings(),
        };
    }
}
