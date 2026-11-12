using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PointManager.Models;
using PointManager.Services;

namespace PointManager.ViewModels;

public partial class SelectableStudent : ObservableObject
{
    public Student Student { get; }

    [ObservableProperty]
    private bool _isSelected;

    public SelectableStudent(Student student) => Student = student;
}

public record GroupFilterOption(string Label, int Kind, Guid? GroupId)
{
    public const int All = 0;
    public const int Specific = 1;
    public const int Ungrouped = 2;
}

public partial class ScoreAdjustViewModel : ObservableObject
{
    private readonly Store _store = Store.Instance;
    private readonly HashSet<Guid> _selectedIds = new();

    public ObservableCollection<SelectableStudent> Students { get; } = new();
    public ObservableCollection<GroupFilterOption> GroupFilters { get; } = new();

    public IEnumerable<int> Presets => _store.Settings.PointPresets;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCountText))]
    private int _selectedCount;

    [ObservableProperty]
    private GroupFilterOption? _selectedGroupFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _reason = string.Empty;

    [ObservableProperty]
    private string _customAmountText = "1";

    public string SelectedCountText => $"已选择 {SelectedCount} 名学生";

    public event Action<string>? Message;

    public ScoreAdjustViewModel()
    {
        Reason = _store.Settings.DefaultReason;
        RebuildGroupFilters();
        SelectedGroupFilter = GroupFilters.FirstOrDefault();
        RebuildStudents();

        _store.DataChanged += () =>
        {
            OnPropertyChanged(nameof(Presets));
            RebuildGroupFilters();
            RebuildStudents();
        };
    }

    partial void OnSelectedGroupFilterChanged(GroupFilterOption? value) => RebuildStudents();
    partial void OnSearchTextChanged(string value) => RebuildStudents();

    private void RebuildGroupFilters()
    {
        var keep = SelectedGroupFilter;
        GroupFilters.Clear();
        GroupFilters.Add(new GroupFilterOption("全部学生", GroupFilterOption.All, null));
        foreach (var g in _store.Groups)
            GroupFilters.Add(new GroupFilterOption(g.DisplayName, GroupFilterOption.Specific, g.Id));
        GroupFilters.Add(new GroupFilterOption("未分组", GroupFilterOption.Ungrouped, null));

        // Preserve prior selection when the same option still exists.
        var match = keep != null
            ? GroupFilters.FirstOrDefault(f => f.Kind == keep.Kind && f.GroupId == keep.GroupId)
            : null;
        SelectedGroupFilter = match ?? GroupFilters[0];
    }

    private void RebuildStudents()
    {
        // Remember current selection.
        _selectedIds.Clear();
        foreach (var s in Students.Where(s => s.IsSelected)) _selectedIds.Add(s.Student.Id);

        // Archived students are immutable, so they cannot receive points either.
        IEnumerable<Student> query = _store.Students.Where(s => !s.IsArchived);
        var filter = SelectedGroupFilter;
        if (filter != null)
        {
            query = filter.Kind switch
            {
                GroupFilterOption.Specific => query.Where(s => s.GroupId == filter.GroupId),
                GroupFilterOption.Ungrouped => query.Where(s => s.GroupId == null),
                _ => query,
            };
        }

        var search = SearchText?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(s =>
                s.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.GroupName.Contains(search, StringComparison.OrdinalIgnoreCase));

        Students.Clear();
        foreach (var s in query.OrderBy(s => s.GroupName).ThenBy(s => s.DisplayName))
        {
            var item = new SelectableStudent(s) { IsSelected = _selectedIds.Contains(s.Id) };
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SelectableStudent.IsSelected)) UpdateCount();
            };
            Students.Add(item);
        }
        UpdateCount();
    }

    private void UpdateCount() => SelectedCount = Students.Count(s => s.IsSelected);

    private List<Student> SelectedStudents() =>
        Students.Where(s => s.IsSelected).Select(s => s.Student).ToList();

    private void ApplyPoints(int delta)
    {
        var targets = SelectedStudents();
        if (targets.Count == 0)
        {
            Message?.Invoke("请先选择至少一名学生");
            return;
        }
        var reason = string.IsNullOrWhiteSpace(Reason) ? _store.Settings.DefaultReason : Reason.Trim();
        _store.ApplyPoints(targets, delta, reason);
        var sign = delta > 0 ? "+" : string.Empty;
        Message?.Invoke($"已为 {targets.Count} 名学生 {sign}{delta} 分");
    }

    [RelayCommand] private void AddPreset(int value) => ApplyPoints(value);
    [RelayCommand] private void SubPreset(int value) => ApplyPoints(-value);

    [RelayCommand]
    private void AddCustom()
    {
        if (int.TryParse(CustomAmountText?.Trim(), out var v) && v != 0) ApplyPoints(Math.Abs(v));
        else Message?.Invoke("请输入有效的自定义分值");
    }

    [RelayCommand]
    private void SubCustom()
    {
        if (int.TryParse(CustomAmountText?.Trim(), out var v) && v != 0) ApplyPoints(-Math.Abs(v));
        else Message?.Invoke("请输入有效的自定义分值");
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var s in Students) s.IsSelected = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var s in Students) s.IsSelected = false;
    }
}
