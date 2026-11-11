using System.Windows;
using MaterialDesignThemes.Wpf;

namespace PointManager.Views.Controls;

/// <summary>A MaterialDesign-styled modal dialog showing read-only text, with optional copy.</summary>
public partial class TextDialog : Window
{
    private readonly string _text;

    public TextDialog(string title, string text,
        PackIconKind icon = PackIconKind.InformationOutline, bool copyable = true)
    {
        InitializeComponent();
        _text = text;
        TitleText.Text = title;
        TitleIcon.Kind = icon;
        BodyText.Text = text;
        CopyButton.Visibility = copyable ? Visibility.Visible : Visibility.Collapsed;
    }

    public static void Show(string title, string text,
        PackIconKind icon = PackIconKind.InformationOutline, Window? owner = null, bool copyable = true)
    {
        var dialog = new TextDialog(title, text, icon, copyable);
        if (owner is { IsLoaded: true })
            dialog.Owner = owner;
        dialog.ShowDialog();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_text);
            CopyButton.Content = "已复制";
        }
        catch
        {
            // Another process may hold the clipboard; copying is best-effort.
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}