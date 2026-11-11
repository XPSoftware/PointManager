using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        NotifyChanged();
        return s;
    }

    public void RemoveStudent(Student s)
    {
        Students.Remove(s);
        NotifyChanged();
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
        var max = Math.Max(50, Settings.MaxHistoryRecords);
        while (Records.Count > max)
            Records.RemoveAt(Records.Count - 1);
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
