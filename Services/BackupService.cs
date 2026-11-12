using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PointManager.Models;

namespace PointManager.Services;

/// <summary>
/// Zip backups of the data set (settings, 档案, 记录, 头像) under Backups\.
/// Manual backups are kept forever; auto backups run on a timer and are pruned
/// to the configured limit, oldest first.
/// </summary>
public static class BackupService
{
    private static DispatcherTimer? _timer;
    private static int _busy;

    public static bool IsDue(AppSettings settings) =>
        settings.IsAutoBackupEnabled &&
        (settings.LastAutoBackupTime is null ||
         (DateTime.Now - settings.LastAutoBackupTime.Value).TotalDays >= settings.AutoBackupIntervalDays);

    /// <summary>Creates one zip of the data parts and returns its full path.</summary>
    public static string CreateBackup(bool auto)
    {
        Directory.CreateDirectory(DataService.BackupsDir);

        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        var prefix = auto ? "auto_" : "manual_";
        var path = Path.Combine(DataService.BackupsDir, prefix + stamp + ".zip");
        for (var i = 1; File.Exists(path); i++)
            path = Path.Combine(DataService.BackupsDir, $"{prefix}{stamp}_{i}.zip");

        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var file in new[] { DataService.SettingsFileName, DataService.LegacyFileName })
            {
                var full = Path.Combine(DataService.RootDir, file);
                if (File.Exists(full)) AddFile(zip, full, file);
            }

            foreach (var folder in new[]
                     {
                         DataService.ProfileFolderName, DataService.HistoryFolderName, DataService.AvatarFolderName,
                     })
            {
                var dir = Path.Combine(DataService.RootDir, folder);
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                    AddFile(zip, file, Path.GetRelativePath(DataService.RootDir, file));
                }
            }
        }

        if (auto) PruneAutoBackups(Store.Instance.Settings.AutoBackupLimit);
        return path;
    }

    // Share write/delete so a concurrent Store.Save (tmp + move) is not blocked by the zip read.
    private static void AddFile(ZipArchive zip, string fullPath, string entryName)
    {
        using var source = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var entry = zip.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Optimal);
        using var target = entry.Open();
        source.CopyTo(target);
    }

    /// <summary>Deletes the oldest auto backups beyond the limit; 0 keeps everything.</summary>
    public static void PruneAutoBackups(int limit)
    {
        if (limit <= 0) return;
        try
        {
            if (!Directory.Exists(DataService.BackupsDir)) return;
            var oldestFirst = Directory.GetFiles(DataService.BackupsDir, "auto_*.zip")
                .OrderBy(File.GetLastWriteTime)
                .ToList();
            for (var i = 0; i < oldestFirst.Count - limit; i++)
            {
                try { File.Delete(oldestFirst[i]); } catch { /* best effort */ }
            }
        }
        catch { /* unreadable backup folder must never break the app */ }
    }

    /// <summary>Checks once shortly after startup, then hourly.</summary>
    public static void StartAutoBackup()
    {
        if (_timer is not null) return;

        var first = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        first.Tick += (_, _) =>
        {
            first.Stop();
            TryAutoBackup();
        };
        first.Start();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _timer.Tick += (_, _) => TryAutoBackup();
        _timer.Start();
    }

    public static void TryAutoBackup()
    {
        if (!IsDue(Store.Instance.Settings)) return;
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;

        Store.Instance.Save();
        Task.Run(() =>
        {
            var failed = false;
            try
            {
                CreateBackup(auto: true);
            }
            catch
            {
                failed = true;
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }

            if (failed) return; // LastAutoBackupTime stays old; the next tick retries.
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Store.Instance.Settings.LastAutoBackupTime = DateTime.Now;
                Store.Instance.Save();
            });
        });
    }
}