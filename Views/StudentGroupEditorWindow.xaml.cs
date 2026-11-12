using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MaterialDesignThemes.Wpf;
using PointManager.Models;
using PointManager.Services;
using PointManager.ViewModels;
using PointManager.Views.Controls;

namespace PointManager.Views;

public partial class StudentGroupEditorWindow : TrayWindow
{
    private readonly StudentGroupEditorViewModel _vm;
    private readonly SnackbarMessageQueue _snackbarQueue;

    public StudentGroupEditorWindow()
    {
        InitializeComponent();
        _vm = new StudentGroupEditorViewModel();
        DataContext = _vm;

        _snackbarQueue = new SnackbarMessageQueue(TimeSpan.FromMilliseconds(2000));
        Snackbar.MessageQueue = _snackbarQueue;

        Store.Instance.DataChanged += () => UpdateProfilePill();
        UpdateProfilePill();
    }

    private void UpdateProfilePill() =>
        ProfileChip.Content = Store.Instance.Settings.ActiveProfile + ".json";

    private void ToggleProfilePanel_Click(object sender, RoutedEventArgs e)
    {
        var open = !ProfileDrawer.IsRightDrawerOpen;
        ProfileDrawer.IsRightDrawerOpen = open;
        if (open) ProfilePanel.Refresh();
    }

    private async Task<string?> PromptAsync(string title, string hint, string initial = "")
    {
        var dialog = new InputDialog(title, hint, initial);
        var result = await DialogHost.Show(dialog, "EditorDialog");
        var text = result as string;
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private async void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptAsync("新建小组", "小组名称");
        if (name == null) return;
        _vm.AddGroup(name);
        _snackbarQueue.Enqueue($"已创建小组「{name}」");
    }

    private async void AddStudentToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedGroup == null)
        {
            _snackbarQueue.Enqueue("请先选择一个小组");
            return;
        }
        var name = await PromptAsync("添加学生", "学生姓名");
        if (name == null) return;
        _vm.AddStudent(name, _vm.SelectedGroup.Id);
        _snackbarQueue.Enqueue($"已将「{name}」添加到「{_vm.SelectedGroup.DisplayName}」");
    }

    private async void AddStudent_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptAsync("新建学生", "学生姓名");
        if (name == null) return;
        _vm.AddStudent(name, null);
        _snackbarQueue.Enqueue($"已创建学生「{name}」");
    }

    private async void ImportStudents_Click(object sender, RoutedEventArgs e)
    {
        var result = await DialogHost.Show(new ImportStudentsDialog(), "EditorDialog");
        if (result is not string text) return;
        var count = _vm.ImportStudentsFromText(text);
        _snackbarQueue.Enqueue(count > 0 ? $"已导入 {count} 名学生" : "没有可导入的学生姓名");
    }

    private async void ReorderStudents_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Students.Count == 0)
        {
            _snackbarQueue.Enqueue("暂无学生可调整顺序");
            return;
        }
        var result = await DialogHost.Show(new ReorderDialog(_vm.Students), "EditorDialog");
        if (result is not System.Collections.IList order) return;
        _vm.ApplyReorder(order.Cast<Student>().ToList());
        _snackbarQueue.Enqueue("顺序已更新，学号已重新编号");
    }

    private void RemoveStudent_Click(object sender, RoutedEventArgs e)
    {
        var s = _vm.SelectedStudent;
        if (s == null)
        {
            _snackbarQueue.Enqueue("请先选择一名学生");
            return;
        }
        if (!ConfirmDialog.Confirm("删除学生", "删除后不可恢复，头像文件将一并删除。",
                confirmText: "删除", icon: PackIconKind.DeleteOutline, owner: this,
                targetText: s.DisplayName, targetIcon: PackIconKind.AccountOutline))
            return;
        _vm.RemoveStudentCommand.Execute(null);
        _snackbarQueue.Enqueue($"已删除学生「{s.DisplayName}」");
    }

    private void ArchiveStudent_Click(object sender, RoutedEventArgs e)
    {
        var s = _vm.SelectedStudent;
        if (s == null || s.IsArchived) return;
        if (!ConfirmDialog.Confirm("存档学生",
                "存档后该学生的信息不可修改，仅可删除，且不再出现在加减分列表中。",
                confirmText: "存档", icon: PackIconKind.ArchiveOutline, owner: this,
                targetText: s.DisplayName, targetIcon: PackIconKind.AccountOutline))
            return;
        _vm.ArchiveStudentCommand.Execute(null);
        _snackbarQueue.Enqueue($"已存档学生「{s.DisplayName}」");
    }

    private void UnarchiveStudent_Click(object sender, RoutedEventArgs e)
    {
        var s = _vm.SelectedStudent;
        if (s == null || !s.IsArchived) return;
        _vm.UnarchiveStudentCommand.Execute(null);
        _snackbarQueue.Enqueue($"已取消存档学生「{s.DisplayName}」");
    }

    private void ChangeAvatar_Click(object sender, RoutedEventArgs e)
    {
        var s = _vm.SelectedStudent;
        if (s == null || s.IsArchived) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择头像图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            Store.Instance.SetAvatar(s, dialog.FileName);
            _snackbarQueue.Enqueue("头像已更新");
        }
        catch (Exception ex)
        {
            _snackbarQueue.Enqueue("头像设置失败：" + ex.Message);
        }
    }
}
