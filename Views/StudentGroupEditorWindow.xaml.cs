using System;
using System.Threading.Tasks;
using System.Windows;
using MaterialDesignThemes.Wpf;
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
        _vm.AddStudent(name, _vm.SelectedGroup?.Id);
        _snackbarQueue.Enqueue($"已创建学生「{name}」");
    }
}
