using System;
using System.Windows;
using MaterialDesignThemes.Wpf;
using PointManager.Services;
using PointManager.ViewModels;
using PointManager.Views.Controls;

namespace PointManager.Views;

public partial class ScoreViewWindow : TrayWindow
{
    private readonly SnackbarMessageQueue _snackbarQueue;

    public ScoreViewWindow()
    {
        InitializeComponent();
        DataContext = new ScoreViewViewModel();

        _snackbarQueue = new SnackbarMessageQueue(TimeSpan.FromMilliseconds(3000));
        Snackbar.MessageQueue = _snackbarQueue;
    }

    private void VerifyTotals_Click(object sender, RoutedEventArgs e)
    {
        var result = Store.Instance.VerifyTotals();

        if (result.Mismatches.Count == 0)
        {
            var message = $"校验完成：{result.StudentCount} 名学生的总分与 {result.RecordCount} 条历史记录合计一致。";
            if (result.OrphanRecordCount > 0)
                message += $"另有 {result.OrphanRecordCount} 条记录属于已删除的学生，未参与校验。";
            _snackbarQueue.Enqueue(message);
            return;
        }

        var dialog = new TotalCheckDialog(result) { Owner = this };
        if (dialog.ShowDialog() == true)
            _snackbarQueue.Enqueue($"已按历史记录修正 {result.Mismatches.Count} 名学生的总分。");
    }
}
