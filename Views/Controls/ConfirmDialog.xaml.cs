using System.Windows;
using MaterialDesignThemes.Wpf;

namespace PointManager.Views.Controls;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message,
        string confirmText = "确定", string cancelText = "取消",
        PackIconKind icon = PackIconKind.HelpCircleOutline,
        string? targetText = null, PackIconKind targetIcon = PackIconKind.FileDocumentOutline)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;
        TitleIcon.Kind = icon;

        if (!string.IsNullOrWhiteSpace(targetText))
        {
            TargetRow.Visibility = Visibility.Visible;
            TargetText.Text = targetText;
            TargetIcon.Kind = targetIcon;
        }
    }

    /// <summary>Shows a MaterialDesign-styled modal confirm dialog. Returns true on 确定.</summary>
    public static bool Confirm(string title, string message,
        string confirmText = "确定", string cancelText = "取消",
        PackIconKind icon = PackIconKind.HelpCircleOutline, Window? owner = null,
        string? targetText = null, PackIconKind targetIcon = PackIconKind.FileDocumentOutline)
    {
        var dialog = new ConfirmDialog(title, message, confirmText, cancelText, icon, targetText, targetIcon);
        if (owner is { IsLoaded: true })
            dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
