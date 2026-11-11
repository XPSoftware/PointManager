using System.Windows;
using System.Windows.Controls;
using PointManager.Services;
using PointManager.ViewModels;
using PointManager.Views.Controls;

namespace PointManager.Views;

public partial class SettingsWindow : TrayWindow
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow()
    {
        InitializeComponent();
        _vm = new SettingsViewModel();
        DataContext = _vm;
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (Confirm("确定要清空所有积分记录吗？此操作不可撤销。"))
            _vm.ClearHistoryCommand.Execute(null);
    }

    private void ResetPoints_Click(object sender, RoutedEventArgs e)
    {
        if (Confirm("确定要将所有学生的积分重置为 0，并清空记录吗？此操作不可撤销。"))
            _vm.ResetPointsCommand.Execute(null);
    }

    private bool Confirm(string message) =>
        ConfirmDialog.Confirm("确认操作", message,
            confirmText: "确定", icon: MaterialDesignThemes.Wpf.PackIconKind.AlertCircleOutline,
            owner: this);

    // Guards programmatic selection resets so they are not treated as new requests.
    private bool _isRevertingSelection;

    /// <summary>
    /// Keeps a ComboBox selection honest: the value only changes when the action behind it
    /// succeeded, otherwise it is snapped back without re-entering the handler.
    /// </summary>
    private void RevertSelection(ComboBox combo, object value)
    {
        _isRevertingSelection = true;
        combo.SelectedValue = value;
        _isRevertingSelection = false;
    }

    // ---- 存储位置 -------------------------------------------------------------

    private void DataLocation_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isRevertingSelection || sender is not ComboBox combo) return;

        // The SelectedValue binding fires this while the window is still wiring up.
        var target = combo.SelectedValue as string;
        if (string.IsNullOrEmpty(target) || target == DataService.Location) return;

        var targetRoot = DataService.DirFor(target);
        var message = DataService.HasData(target)
            ? $"数据将改为保存在「{DataService.DisplayName(target)}」。\n\n{targetRoot}\n\n" +
              "该位置已有数据，会被当前数据覆盖（同名文件逐个覆盖，其余保留）。是否继续？"
            : $"数据将复制到「{DataService.DisplayName(target)}」：\n\n{targetRoot}\n\n" +
              "包含 Settings.json、Profile\\、Histories\\。是否继续？";

        if (!ConfirmDialog.Confirm("更改存储位置", message,
                confirmText: "更改", icon: MaterialDesignThemes.Wpf.PackIconKind.FolderMoveOutline,
                owner: this))
        {
            RevertSelection(combo, DataService.Location);
            return;
        }

        if (!_vm.TryChangeDataLocation(target, out var error))
        {
            RevertSelection(combo, DataService.Location);
            Warn("更改存储位置", error ?? "更改存储位置失败。");
        }
    }

    // ---- 旧数据迁移 -----------------------------------------------------------

    private void MigrateLegacy_Click(object sender, RoutedEventArgs e)
    {
        var message =
            $"将旧版单文件数据进行迁移：\n\n{DataService.LegacyFilePath}\n\n" +
            $"保存在 {DataService.RootDir}，旧文件将备份在Backup目录下。是否继续？";

        if (!ConfirmDialog.Confirm("迁移旧数据", message,
                confirmText: "迁移", icon: MaterialDesignThemes.Wpf.PackIconKind.FolderMoveOutline,
                owner: this)) return;

        if (_vm.TryMigrateLegacy(out var result))
        {
            MessageBox.Show(this,
                string.IsNullOrEmpty(result)
                    ? $"迁移完成。\n\n{DataService.RootDir}"
                    : $"迁移完成，但有提示：\n{result}",
                "迁移旧数据", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Warn("迁移旧数据", result ?? "迁移失败。");
    }

    private void Warn(string title, string message) =>
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
}
