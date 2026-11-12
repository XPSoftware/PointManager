using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PointManager.Models;
using PointManager.Services;

namespace PointManager.ViewModels;

/// <summary>ComboBox item for the group row; Id == null stands for 未分组.</summary>
public sealed class GroupOption : ObservableObject
{
    public Guid? Id { get; }

    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public GroupOption(Guid? id, string name)
    {
        Id = id;
        _name = name;
    }
}

public partial class StudentGroupEditorViewModel : ObservableObject
{
    private readonly Store _store = Store.Instance;

    public ObservableCollection<Group> Groups => _store.Groups;
    public ObservableCollection<Student> Students => _store.Students;

    /// <summary>Students belonging to <see cref="SelectedGroup"/>, shown in the group detail DataGrid.</summary>
    public ListCollectionView GroupStudentsView { get; }

    /// <summary>Group row options: 未分组 first, then every group. Synced in place so the
    /// ComboBox selection survives refreshes.</summary>
    public ObservableCollection<GroupOption> GroupOptions { get; } = new();

    public IReadOnlyList<string> GenderOptions { get; } = new[] { "未设置", "男", "女" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedGroup))]
    private Group? _selectedGroup;

    [ObservableProperty]
    private Student? _selectedStudentInGroup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedStudent))]
    [NotifyPropertyChangedFor(nameof(CanEditSelectedStudent))]
    [NotifyPropertyChangedFor(nameof(IsSelectedStudentArchived))]
    [NotifyPropertyChangedFor(nameof(SelectedStudentGroupId))]
    [NotifyPropertyChangedFor(nameof(SelectedStudentGender))]
    private Student? _selectedStudent;

    public bool HasSelectedGroup => SelectedGroup != null;

    public bool HasSelectedStudent => SelectedStudent != null;

    /// <summary>Archived students are immutable; only deletion stays available.</summary>
    public bool CanEditSelectedStudent => SelectedStudent is { IsArchived: false };

    public bool IsSelectedStudentArchived => SelectedStudent?.IsArchived == true;

    /// <summary>Two-way bridge for the student's group ComboBox (null = ungrouped).</summary>
    public Guid? SelectedStudentGroupId
    {
        get => SelectedStudent?.GroupId;
        set
        {
            if (SelectedStudent is { IsArchived: false } s && value != s.GroupId)
                _store.AssignGroup(s, value);
        }
    }

    /// <summary>Two-way bridge for the gender ComboBox (empty string shows as 未设置).</summary>
    public string SelectedStudentGender
    {
        get => SelectedStudent?.Gender is { Length: > 0 } g ? g : "未设置";
        set
        {
            if (SelectedStudent is { IsArchived: false } s)
                s.Gender = value == "未设置" ? string.Empty : value;
        }
    }

    public StudentGroupEditorViewModel()
    {
        GroupStudentsView = new ListCollectionView(_store.Students)
        {
            Filter = o => o is Student s && SelectedGroup != null && s.GroupId == SelectedGroup.Id,
        };

        SyncGroupOptions();

        _store.DataChanged += () =>
        {
            GroupStudentsView.Refresh();
            SyncGroupOptions();
            OnPropertyChanged(nameof(Groups));
        };
    }

    partial void OnSelectedGroupChanged(Group? oldValue, Group? newValue)
    {
        if (oldValue != null) oldValue.PropertyChanged -= OnSelectedGroupPropertyChanged;
        if (newValue != null) newValue.PropertyChanged += OnSelectedGroupPropertyChanged;
        GroupStudentsView.Refresh();
    }

    partial void OnSelectedStudentChanged(Student? oldValue, Student? newValue)
    {
        if (oldValue != null) oldValue.PropertyChanged -= OnSelectedStudentPropertyChanged;
        if (newValue != null) newValue.PropertyChanged += OnSelectedStudentPropertyChanged;
    }

    // Direct field edits bypass Store mutations, so persist them like SettingsViewModel does.
    private void OnSelectedStudentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Student.Name):
            case nameof(Student.Gender):
            case nameof(Student.Remark):
                _store.NotifyChanged();
                break;
            case nameof(Student.IsArchived):
                OnPropertyChanged(nameof(CanEditSelectedStudent));
                OnPropertyChanged(nameof(IsSelectedStudentArchived));
                break;
        }
    }

    private void OnSelectedGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Group.Name))
            _store.NotifyChanged();
    }

    private void SyncGroupOptions()
    {
        if (GroupOptions.Count == 0)
            GroupOptions.Add(new GroupOption(null, "未分组"));

        for (var i = GroupOptions.Count - 1; i >= 1; i--)
            if (!_store.Groups.Any(g => g.Id == GroupOptions[i].Id))
                GroupOptions.RemoveAt(i);

        foreach (var g in _store.Groups)
        {
            var option = GroupOptions.FirstOrDefault(o => o.Id == g.Id);
            if (option == null)
                GroupOptions.Add(new GroupOption(g.Id, g.DisplayName));
            else
                option.Name = g.DisplayName;
        }
    }

    // ---- Group operations ------------------------------------------------

    public Group? AddGroup(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var g = _store.AddGroup(name.Trim());
        SelectedGroup = g;
        return g;
    }

    [RelayCommand]
    private void RemoveGroup()
    {
        if (SelectedGroup == null) return;
        _store.RemoveGroup(SelectedGroup);
        SelectedGroup = Groups.LastOrDefault();
    }

    [RelayCommand]
    private void UngroupSelectedStudent()
    {
        if (SelectedStudentInGroup != null)
            _store.AssignGroup(SelectedStudentInGroup, null);
    }

    // ---- Student operations ---------------------------------------------

    public Student? AddStudent(string name, Guid? groupId)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var s = _store.AddStudent(name.Trim(), groupId);
        SelectedStudent = s;
        return s;
    }

    /// <summary>Parses one name per line; returns how many students were created.</summary>
    public int ImportStudentsFromText(string text)
    {
        var names = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);
        var added = _store.ImportStudents(names);
        if (added.Count > 0)
            SelectedStudent = added[0];
        return added.Count;
    }

    public void ApplyReorder(IReadOnlyList<Student> order) => _store.ReorderStudents(order);

    [RelayCommand]
    private void ArchiveStudent()
    {
        if (SelectedStudent is not { IsArchived: false } s) return;
        s.IsArchived = true;
        _store.NotifyChanged();
    }

    [RelayCommand]
    private void UnarchiveStudent()
    {
        if (SelectedStudent is not { IsArchived: true } s) return;
        s.IsArchived = false;
        _store.NotifyChanged();
    }

    [RelayCommand]
    private void RemoveStudent()
    {
        if (SelectedStudent != null) _store.RemoveStudent(SelectedStudent);
    }

    [RelayCommand]
    private void RemoveStudentFromGroup()
    {
        if (SelectedStudentInGroup != null) _store.RemoveStudent(SelectedStudentInGroup);
    }
}