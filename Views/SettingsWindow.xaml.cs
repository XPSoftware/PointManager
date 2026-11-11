using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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

        // WPF's ico decoder picks a small frame by default; use the largest for a crisp banner.
        var icon = new IconBitmapDecoder(
            new Uri("pack://application:,,,/PointManager;component/Assets/app.ico"),
            BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        AboutIcon.Source = icon.Frames.OrderByDescending(f => f.PixelWidth).First();
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

    // ---- 关于 -------------------------------------------------------------

    private void AboutLink_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string url) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Warn("打开链接", $"无法打开 {url}\n{ex.Message}");
        }
    }

    private void AboutDiagnostics_Click(object sender, RoutedEventArgs e) =>
        TextDialog.Show("诊断信息", BuildDiagnosticInfo(),
            MaterialDesignThemes.Wpf.PackIconKind.InformationOutline, this);

    private void AboutLibraries_Click(object sender, RoutedEventArgs e) =>
        TextDialog.Show("第三方库", LibrariesText,
            MaterialDesignThemes.Wpf.PackIconKind.PackageVariant, this);

    private void AboutLicense_Click(object sender, RoutedEventArgs e) =>
        TextDialog.Show("开放源代码许可", LicenseText,
            MaterialDesignThemes.Wpf.PackIconKind.FileDocumentOutline, this);

    private void AboutPrivacy_Click(object sender, RoutedEventArgs e) =>
        TextDialog.Show("隐私说明", PrivacyText,
            MaterialDesignThemes.Wpf.PackIconKind.ShieldCheckOutline, this);

    private string BuildDiagnosticInfo()
    {
        var s = _vm.Settings;
        return
            $"PointManager {App.AppVersion}\n" +
            $"运行时: .NET {Environment.Version}\n" +
            $"系统: {Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "64 位" : "32 位")})\n" +
            $"存储位置: {DataService.DisplayName(DataService.Location)}\n" +
            $"数据根目录: {DataService.RootDir}\n" +
            $"当前档案: {s.ActiveProfile}\n" +
            $"学生: {Store.Instance.Students.Count}    小组: {Store.Instance.Groups.Count}    积分记录: {Store.Instance.Records.Count}\n" +
            $"深色主题: {(s.IsDarkTheme ? "开" : "关")}    跟随系统主题色: {(s.FollowSystemAccent ? "开" : "关")}";
    }

    private const string LibrariesText = """
        PointManager 的构建依赖以下开源组件：

        • MaterialDesignThemes 4.8.0 — MIT
          Material Design 主题与控件
          https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit

        • MaterialDesignColors 2.1.2 — MIT
          Material Design 色板
          https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit

        • Hardcodet.NotifyIcon.Wpf 2.0.1 — MIT
          WPF 托盘图标
          https://github.com/hardcodet/wpf-notifyicon

        • CommunityToolkit.Mvvm 8.2.1 — MIT
          MVVM 源生成器与基础设施
          https://github.com/CommunityToolkit/dotnet

        • .NET 8 / WPF — MIT
          运行时与 UI 框架
          https://github.com/dotnet/runtime
        """;

    private const string LicenseText = """
        PointManager 使用的第三方组件均以 MIT 许可证发布。

        MIT License

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.

        ---

        组件版权说明：
        • CommunityToolkit.Mvvm — Copyright © .NET Foundation and Contributors
        • Hardcodet.NotifyIcon.Wpf — Copyright (c) Philipp Sumi
        • MaterialDesignThemes / MaterialDesignColors — Copyright (c) MaterialDesignInXamlToolkit contributors
        • .NET / WPF — Copyright (c) .NET Foundation and Contributors
        """;

    private const string PrivacyText = """
        PointManager 是一款完全本地运行的单机工具：

        • 所有数据（设置、学生与小组档案、积分记录）仅保存在您选择的存储位置，不会上传到任何服务器。
        • 应用不包含遥测、统计或任何后台网络请求。
        • 「关于」页中的链接仅在您主动点击时交由系统浏览器打开。
        • 如需清除全部数据，删除数据根目录即可（见 设置 → 存储）。
        """;
}
