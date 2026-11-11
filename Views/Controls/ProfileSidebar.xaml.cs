using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using PointManager.Services;

namespace PointManager.Views.Controls;

/// <summary>One row of the 管理档案 sidebar.</summary>
public sealed class ProfileItem
{
    public string Name { get; init; } = string.Empty;
    public string FileName => Name + ".json";
    public bool IsActive { get; init; }
}

/// <summary>
/// The right-hand 管理档案 panel: lists every 档案 file, switches between them live and
/// offers 新建 / 复制 / 重命名 / 删除 plus a shortcut into the Profile folder.
/// </summary>
public partial class ProfileSidebar : UserControl
{
    private readonly SnackbarMessageQueue _queue = new(TimeSpan.FromMilliseconds(2200));

    // Guards Refresh() against the SelectionChanged it raises while re-selecting.
    private bool _isRefreshing;

    public ObservableCollection<ProfileItem> Profiles { get; } = new();

    public ProfileSidebar()
    {
        InitializeComponent();
        ProfileList.ItemsSource = Profiles;
        SidebarSnackbar.MessageQueue = _queue;

        Store.Instance.DataChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        var active = Store.Instance.Settings.ActiveProfile;
        _isRefreshing = true;
        try
        {
            Profiles.Clear();
            foreach (var name in DataService.ProfileNames())
                Profiles.Add(new ProfileItem
                {
                    Name = name,
                    IsActive = string.Equals(name, active, StringComparison.OrdinalIgnoreCase),
                });
            ProfileList.SelectedItem = Profiles.FirstOrDefault(p => p.IsActive);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    // ---- switching -------------------------------------------------------------

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing) return;
        if (ProfileList.SelectedItem is not ProfileItem item || item.IsActive) return;
        SwitchTo(item.Name);
    }

    private void SwitchTo(string name)
    {
        try
        {
            Store.Instance.SwitchProfile(name);
            _queue.Enqueue($"已切换到档案「{name}」。");
        }
        catch (Exception ex)
        {
            _queue.Enqueue($"切换档案失败：{ex.Message}");
        }
        Refresh();
    }

    // ---- actions ---------------------------------------------------------------

    private async void CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptAsync("新建档案", "档案名称（例如：初二（3）班）");
        if (name is null) return;

        if (DataService.TryCreateProfile(name, out var created, out var error))
            _queue.Enqueue($"已创建档案「{created}」。");
        else
            _queue.Enqueue(error ?? "创建档案失败。");
        Refresh();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(DataService.ProfileDir);
            Process.Start(new ProcessStartInfo(DataService.ProfileDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _queue.Enqueue($"打开文件夹失败：{ex.Message}");
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private async void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is not ProfileItem item) return;

        var name = await PromptAsync("复制档案", "新档案名称", item.Name + " 副本");
        if (name is null) return;

        if (DataService.TryDuplicateProfile(item.Name, name, out var created, out var error))
            _queue.Enqueue($"已复制为档案「{created}」。");
        else
            _queue.Enqueue(error ?? "复制档案失败。");
        Refresh();
    }

    private async void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is not ProfileItem item) return;

        var name = await PromptAsync("重命名档案", "档案名称", item.Name);
        if (name is null || string.Equals(name, item.Name, StringComparison.OrdinalIgnoreCase)) return;

        if (!DataService.TryRenameProfile(item.Name, name, out var renamed, out var error))
        {
            _queue.Enqueue(error ?? "重命名档案失败。");
            Refresh();
            return;
        }

        // The loaded data is unchanged; only the file that holds it moved.
        if (string.Equals(item.Name, Store.Instance.Settings.ActiveProfile, StringComparison.OrdinalIgnoreCase))
        {
            Store.Instance.Settings.ActiveProfile = renamed;
            Store.Instance.Save();
        }

        _queue.Enqueue($"档案已重命名为「{renamed}」。");
        Refresh();
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is not ProfileItem item) return;

        if (!ConfirmDialog.Confirm("删除档案",
                $"删除档案「{item.Name}」及其全部积分记录，不可撤销。",
                confirmText: "删除", icon: PackIconKind.TrashCanOutline,
                owner: Window.GetWindow(this),
                targetText: item.FileName, targetIcon: PackIconKind.FileDocumentOutline))
            return;

        try
        {
            if (item.IsActive)
            {
                var other = Profiles.FirstOrDefault(p => !p.IsActive)?.Name;
                if (other is null)
                {
                    _queue.Enqueue("至少需要保留一个档案。");
                    return;
                }
                Store.Instance.SwitchProfile(other);
            }

            if (DataService.TryDeleteProfile(item.Name, out var error))
                _queue.Enqueue($"已删除档案「{item.Name}」。");
            else
                _queue.Enqueue(error ?? "删除档案失败。");
        }
        catch (Exception ex)
        {
            _queue.Enqueue($"删除档案失败：{ex.Message}");
        }
        Refresh();
    }

    private static async Task<string?> PromptAsync(string title, string hint, string initial = "")
    {
        var result = await DialogHost.Show(new InputDialog(title, hint, initial), "EditorDialog");
        return result is string text && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
    }
}
