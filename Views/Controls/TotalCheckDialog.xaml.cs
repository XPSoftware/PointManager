using System.Windows;
using System.Windows.Interop;
using MaterialDesignThemes.Wpf;
using PointManager.Models;
using PointManager.Services;

namespace PointManager.Views.Controls;

public partial class TotalCheckDialog : Window
{
    private readonly TotalCheckResult _result;

    public TotalCheckDialog(TotalCheckResult result)
    {
        InitializeComponent();
        _result = result;

        MismatchList.ItemsSource = result.Mismatches;
        SummaryText.Text = $"共 {result.Mismatches.Count} 名学生的总分与历史记录合计不一致。";
        CaptionText.Text = $"合计基于当前保留的 {result.RecordCount} 条历史记录；" +
                           "接近上限时旧记录会按学生合并（合计不变），超出上限被裁剪后合计仅覆盖保留范围内的记录。";

        if (result.OrphanRecordCount > 0)
        {
            OrphanNote.Visibility = Visibility.Visible;
            OrphanNoteText.Text = $"另有 {result.OrphanRecordCount} 条记录（合计 " +
                                  $"{result.OrphanDeltaSum} 分）属于已删除的学生，未参与校验。";
        }
    }

    private void Fix_Click(object sender, RoutedEventArgs e)
    {
        var ok = ConfirmDialog.Confirm(
            "按历史记录修正",
            "所列学生的总分将被改为历史记录合计值，此操作不可撤销。是否继续？",
            confirmText: "修正",
            icon: PackIconKind.ClipboardCheckOutline,
            owner: this,
            targetText: $"{_result.Mismatches.Count} 名学生",
            targetIcon: PackIconKind.AccountGroupOutline);
        if (!ok) return;

        Store.Instance.ApplyVerifiedTotals(_result.Mismatches);
        EndDialog(true);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => EndDialog(false);

    // The startup auto-check shows this window non-modal; DialogResult only exists in a modal loop.
    private void EndDialog(bool result)
    {
        if (ComponentDispatcher.IsThreadModal)
            DialogResult = result;
        else
            Close();
    }
}
