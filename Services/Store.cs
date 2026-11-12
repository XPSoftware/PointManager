using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using PointManager.Models;
using PointManager.Services;

namespace PointManager.Services;

/// <summary>
/// Application-wide observable data store. All view-models share this single instance.
/// </summary>
public sealed class Store
{
    public static Store Instance { get; } = new();

    public ObservableCollection<Group> Groups { get; } = new();
    public ObservableCollection<Student> Students { get; } = new();
    public ObservableCollection<PointRecord> Records { get; } = new();
    public AppSettings Settings { get; private set; } = new();

    /// <summary>Raised after any mutation so hosts can autosave.</summary>
    public event Action? DataChanged;

    private Store() { }

    public void Load()
    {
        var data = DataService.Load();

        Groups.Clear();
        foreach (var g in data.Groups) Groups.Add(g);

        Students.Clear();
        foreach (var s in data.Students) Students.Add(s);

        Records.Clear();
        foreach (var r in data.Records.OrderByDescending(r => r.Time)) Records.Add(r);

        Settings = data.Settings ?? new AppSettings();

        ResolveLinks();
        Renumber();
        TrimHistory();
        RecomputeCounts();
    }

    public void Save()
    {
        // While a 档案 switch is in flight the settings already name the incoming file but the
        // collections still hold the outgoing one - auto-saves must not cross the two.
        if (_isSwitchingProfile) return;

        DataService.Save(new PersistedData
        {
            Groups = Groups.ToList(),
            Students = Students.ToList(),
            Records = Records.ToList(),
            Settings = Settings,
        });
    }

    private bool _isSwitchingProfile;

    /// <summary>
    /// Loads another 档案 file (Profile\&lt;name&gt;.json plus its monthly history). The current
    /// 档案 is flushed first so nothing it holds is lost.
    /// </summary>
    public void SwitchProfile(string? name)
    {
        var target = DataService.SanitizeName(name);
        if (string.Equals(target, Settings.ActiveProfile, StringComparison.OrdinalIgnoreCase)) return;

        Save();                       // flush the outgoing 档案 under its own name

        _isSwitchingProfile = true;
        try
        {
            Settings.ActiveProfile = target;

            var profile = DataService.LoadProfile(target);

            Groups.Clear();
            foreach (var g in profile.Groups) Groups.Add(g);

            Students.Clear();
            foreach (var s in profile.Students) Students.Add(s);

            Records.Clear();
            foreach (var r in DataService.LoadHistory(target)) Records.Add(r);

            ResolveLinks();
            Renumber();
            TrimHistory();
            RecomputeCounts();
        }
        finally
        {
            _isSwitchingProfile = false;
        }

        Save();                       // one write of the incoming 档案 plus the new active name
        DataChanged?.Invoke();
    }

    private void ResolveLinks()
    {
        var byId = Groups.ToDictionary(g => g.Id);
        foreach (var s in Students)
            s.Group = s.GroupId is { } id && byId.TryGetValue(id, out var g) ? g : null;
    }

    public void NotifyChanged()
    {
        RecomputeCounts();
        DataChanged?.Invoke();
    }

    private void RecomputeCounts()
    {
        foreach (var g in Groups)
            g.MemberCount = Students.Count(s => s.GroupId == g.Id);
    }

    /// <summary>学号就是列表中的位置（从 1 起），任何增删或排序后都要重算。</summary>
    private void Renumber()
    {
        for (var i = 0; i < Students.Count; i++)
            Students[i].Number = i + 1;
    }

    public Group? FindGroup(Guid? id) => id is { } g ? Groups.FirstOrDefault(x => x.Id == g) : null;

    public IEnumerable<Student> StudentsInGroup(Guid? groupId) =>
        Students.Where(s => s.GroupId == groupId);

    // ---- Mutations -------------------------------------------------------

    public Group AddGroup(string name)
    {
        var g = new Group { Name = name };
        Groups.Add(g);
        NotifyChanged();
        return g;
    }

    public void RemoveGroup(Group g)
    {
        Groups.Remove(g);
        foreach (var s in Students.Where(s => s.GroupId == g.Id))
            s.GroupId = null; // becomes ungrouped
        ResolveLinks();
        NotifyChanged();
    }

    public Student AddStudent(string name, Guid? groupId)
    {
        var s = new Student { Name = name, GroupId = groupId };
        s.Group = FindGroup(groupId);
        Students.Add(s);
        Renumber();
        NotifyChanged();
        return s;
    }

    public void RemoveStudent(Student s)
    {
        Students.Remove(s);
        DeleteAvatarFile(s);
        Renumber();
        NotifyChanged();
    }

    /// <summary>Creates one student per non-empty line; returns the added students.</summary>
    public List<Student> ImportStudents(IEnumerable<string> names)
    {
        var added = new List<Student>();
        foreach (var raw in names)
        {
            var name = raw.Trim();
            if (name.Length == 0) continue;
            var s = new Student { Name = name };
            Students.Add(s);
            added.Add(s);
        }
        if (added.Count > 0)
        {
            Renumber();
            NotifyChanged();
        }
        return added;
    }

    /// <summary>Applies a new ordering of exactly the current students, then renumbers.</summary>
    public void ReorderStudents(IReadOnlyList<Student> order)
    {
        if (order.Count != Students.Count) return;
        for (var i = 0; i < order.Count; i++)
        {
            var current = Students.IndexOf(order[i]);
            if (current < 0) return;
            if (current != i) Students.Move(current, i);
        }
        Renumber();
        NotifyChanged();
    }

    /// <summary>Copies an image into the active profile's avatar folder and points the student at it.</summary>
    public void SetAvatar(Student s, string sourceFile)
    {
        var dir = DataService.AvatarProfileDir(Settings.ActiveProfile);
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(sourceFile);
        if (string.IsNullOrEmpty(ext)) ext = ".png";
        var target = Path.Combine(dir, s.Id.ToString("N") + ext);
        File.Copy(sourceFile, target, overwrite: true);

        var old = s.AvatarPath;
        s.AvatarPath = Path.GetRelativePath(DataService.RootDir, target);
        if (!string.IsNullOrWhiteSpace(old))
        {
            try
            {
                var oldFull = Path.Combine(DataService.RootDir, old);
                if (File.Exists(oldFull) && !string.Equals(oldFull, target, StringComparison.OrdinalIgnoreCase))
                    File.Delete(oldFull);
            }
            catch { /* stale file we cannot remove is harmless */ }
        }
        NotifyChanged();
    }

    private static void DeleteAvatarFile(Student s)
    {
        if (string.IsNullOrWhiteSpace(s.AvatarPath)) return;
        try
        {
            var path = Path.Combine(DataService.RootDir, s.AvatarPath);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* best effort */ }
    }

    public void AssignGroup(Student s, Guid? groupId)
    {
        s.GroupId = groupId;
        s.Group = FindGroup(groupId);
        NotifyChanged();
    }

    /// <summary>Applies a delta to one or more students and records history.</summary>
    public void ApplyPoints(IEnumerable<Student> targets, int delta, string reason)
    {
        var list = targets as IList<Student> ?? targets.ToList();
        if (list.Count == 0 || delta == 0) return;

        foreach (var s in list)
        {
            s.Points += delta;
            Records.Insert(0, new PointRecord
            {
                StudentId = s.Id,
                StudentName = s.DisplayName,
                GroupName = s.GroupName,
                Delta = delta,
                Reason = reason,
                Time = DateTime.Now,
            });
        }
        TrimHistory();
        NotifyChanged();
    }

    private void TrimHistory()
    {
        if (Settings.MaxHistoryRecords <= 0) return;
        var max = Math.Max(50, Settings.MaxHistoryRecords);
        MergeOldRecords(max);
        while (Records.Count > max)
            Records.RemoveAt(Records.Count - 1);
    }

    // Near the cap, fold the oldest records into one summary per student so the
    // retained history still sums to each student's total.
    private void MergeOldRecords(int max)
    {
        if (Records.Count < max * 9 / 10) return;
        var keep = max / 2;
        var old = Records.Skip(keep).ToList();
        var summaries = old.GroupBy(r => r.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Delta = g.Sum(r => r.Delta),
                Name = g.First().StudentName,
                Group = g.First().GroupName,
                Time = g.Max(r => r.Time),
            })
            .Where(x => x.Delta != 0)
            .Select(x => new PointRecord
            {
                StudentId = x.StudentId,
                StudentName = x.Name,
                GroupName = x.Group,
                Delta = x.Delta,
                Reason = "历史合并",
                Time = x.Time,
            })
            .OrderByDescending(r => r.Time)
            .ToList();
        foreach (var r in old) Records.Remove(r);
        foreach (var r in summaries) Records.Add(r);
    }

    public void ClearHistory()
    {
        Records.Clear();
        NotifyChanged();
    }

    /// <summary>
    /// Reconciles every student's stored total against the sum of the retained history
    /// records. Records left behind by deleted students cannot be attributed to any
    /// student and are reported separately.
    /// </summary>
    public TotalCheckResult VerifyTotals()
    {
        var sums = Records.GroupBy(r => r.StudentId)
                          .ToDictionary(g => g.Key, g => g.Sum(r => r.Delta));
        var known = new HashSet<Guid>(Students.Select(s => s.Id));

        var mismatches = new List<TotalCheckItem>();
        foreach (var s in Students)
        {
            var fromHistory = sums.TryGetValue(s.Id, out var sum) ? sum : 0;
            if (fromHistory != s.Points)
                mismatches.Add(new TotalCheckItem(s.Id, s.DisplayName, s.GroupName, s.Points, fromHistory));
        }

        var orphanDeltas = Records.Where(r => !known.Contains(r.StudentId))
                                  .Select(r => r.Delta).ToList();
        return new TotalCheckResult(mismatches, Students.Count, Records.Count,
            orphanDeltas.Count, orphanDeltas.Sum());
    }

    /// <summary>Overwrites the stored totals of the given students with their history sums.</summary>
    public void ApplyVerifiedTotals(IEnumerable<TotalCheckItem> items)
    {
        var byId = Students.ToDictionary(s => s.Id);
        foreach (var item in items)
            if (byId.TryGetValue(item.StudentId, out var s))
                s.Points = item.FromHistory;
        NotifyChanged();
    }

    public void ResetAllPoints()
    {
        foreach (var s in Students) s.Points = 0;
        Records.Clear();
        NotifyChanged();
    }
}
