using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PointManager.Models;

public partial class Group : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private int _memberCount;
    [JsonIgnore]
    public int MemberCount
    {
        get => _memberCount;
        set => SetProperty(ref _memberCount, value);
    }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(未命名小组)" : Name;
}

public partial class Student : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private Guid? _groupId;
    public Guid? GroupId
    {
        get => _groupId;
        set
        {
            if (SetProperty(ref _groupId, value))
            {
                OnPropertyChanged(nameof(Group));
                OnPropertyChanged(nameof(GroupName));
            }
        }
    }

    private int _points;
    public int Points
    {
        get => _points;
        set => SetProperty(ref _points, value);
    }

    // Resolved at load time by the store; not persisted.
    [JsonIgnore]
    public Group? Group { get; set; }

    [JsonIgnore]
    public string GroupName => Group?.DisplayName ?? "未分组";

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(未命名学生)" : Name;
}

public class PointRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime Time { get; set; } = DateTime.Now;
}

/// <summary>One student whose stored total disagrees with the sum of the history records.</summary>
public record TotalCheckItem(Guid StudentId, string Name, string GroupName, int Current, int FromHistory)
{
    public int Difference => Current - FromHistory;
}

/// <summary>Outcome of reconciling student totals against the retained history records.</summary>
public record TotalCheckResult(
    IReadOnlyList<TotalCheckItem> Mismatches,
    int StudentCount,
    int RecordCount,
    int OrphanRecordCount,
    int OrphanDeltaSum);
