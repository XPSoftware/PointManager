using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PointManager.Models;
using PointManager.Services;

namespace PointManager.ViewModels;

public partial class StudentGroupEditorViewModel : ObservableObject
{
    private readonly Store _store = Store.Instance;

    public ObservableCollection<Group> Groups => _store.Groups;
    public ObservableCollection<Student> Students => _store.Students;

    /// <summary>Students belonging to <see cref="SelectedGroup"/>, shown in the group detail DataGrid.</summary>
    public ListCollectionView GroupStudentsView { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedGroup))]
    private Group? _selectedGroup;

    [ObservableProperty]
    private Student? _selectedStudentInGroup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedStudentGroupId))]
    private Student? _selectedStudent;

    public bool HasSelectedGroup => SelectedGroup != null;

    /// <summary>Two-way bridge for the student's group ComboBox (null = ungrouped).</summary>
    public Guid? SelectedStudentGroupId
    {
        get => SelectedStudent?.GroupId;
        set
        {
            if (SelectedStudent != null && value != SelectedStudent.GroupId)
                _store.AssignGroup(SelectedStudent, value);
        }
    }

    public StudentGroupEditorViewModel()
    {
        GroupStudentsView = new ListCollectionView(_store.Students)
        {
            Filter = o => o is Student s && SelectedGroup != null && s.GroupId == SelectedGroup.Id,
        };

        _store.DataChanged += () =>
        {
            GroupStudentsView.Refresh();
            OnPropertyChanged(nameof(Groups));
        };
    }

    partial void OnSelectedGroupChanged(Group? value) => GroupStudentsView.Refresh();

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

    [RelayCommand]
    private void RemoveSelectedStudentFromGroup()
    {
        if (SelectedStudent != null)
            _store.AssignGroup(SelectedStudent, null);
    }

    // ---- Student operations ---------------------------------------------

    public Student? AddStudent(string name, Guid? groupId)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var s = _store.AddStudent(name.Trim(), groupId);
        SelectedStudent = s;
        return s;
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

    [RelayCommand]
    private void AssignSelectedStudentToCurrentGroup()
    {
        if (SelectedStudent != null && SelectedGroup != null)
            _store.AssignGroup(SelectedStudent, SelectedGroup.Id);
    }
}
