using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PointManager.Models;
using PointManager.Services;

namespace PointManager.ViewModels;

public record GroupSummary(string Name, int MemberCount, int TotalPoints, double AveragePoints);

public partial class ScoreViewViewModel : ObservableObject
{
    private readonly Store _store = Store.Instance;

    public ObservableCollection<Student> Students => _store.Students;
    public ObservableCollection<PointRecord> Records => _store.Records;
    public ObservableCollection<GroupSummary> GroupSummaries { get; } = new();

    [ObservableProperty] private int _groupCount;
    [ObservableProperty] private int _studentCount;
    [ObservableProperty] private int _recordCount;
    [ObservableProperty] private int _totalPoints;

    public ScoreViewViewModel()
    {
        Recompute();
        _store.DataChanged += Recompute;
    }

    private void Recompute()
    {
        GroupSummaries.Clear();

        foreach (var g in _store.Groups)
        {
            var members = _store.StudentsInGroup(g.Id).ToList();
            var total = members.Sum(m => m.Points);
            GroupSummaries.Add(new GroupSummary(
                g.DisplayName, members.Count, total,
                members.Count == 0 ? 0 : Math.Round(total / (double)members.Count, 1)));
        }

        var ungrouped = _store.Students.Where(s => s.GroupId == null).ToList();
        if (ungrouped.Count > 0)
        {
            var total = ungrouped.Sum(s => s.Points);
            GroupSummaries.Add(new GroupSummary("未分组", ungrouped.Count, total,
                Math.Round(total / (double)ungrouped.Count, 1)));
        }

        GroupCount = _store.Groups.Count;
        StudentCount = _store.Students.Count;
        RecordCount = _store.Records.Count;
        TotalPoints = _store.Students.Sum(s => s.Points);
    }
}
