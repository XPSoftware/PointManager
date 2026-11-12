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
        set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(AvatarInitial));
        }
    }

    /// <summary>学号：按列表顺序从 1 自动编号，只能通过调整顺序改变。</summary>
    private int _number;
    public int Number
    {
        get => _number;
        set => SetProperty(ref _number, value);
    }

    private string _gender = string.Empty;
    public string Gender
    {
        get => _gender;
        set => SetProperty(ref _gender, value);
    }

    private string _remark = string.Empty;
    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value);
    }

    /// <summary>相对数据根目录的头像文件路径；为空时显示姓名首字。</summary>
    private string? _avatarPath;
    public string? AvatarPath
    {
        get => _avatarPath;
        set
        {
            if (SetProperty(ref _avatarPath, value))
                OnPropertyChanged(nameof(HasCustomAvatar));
        }
    }

    private bool _isArchived;
    public bool IsArchived
    {
        get => _isArchived;
        set => SetProperty(ref _isArchived, value);
    }

    [JsonIgnore]
    public bool HasCustomAvatar => !string.IsNullOrWhiteSpace(AvatarPath);

    [JsonIgnore]
    public string AvatarInitial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[0].ToString();

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
