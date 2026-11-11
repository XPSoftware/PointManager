using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace PointManager.Views.Controls;

public partial class InputDialog : UserControl
{
    public InputDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public InputDialog(string title, string hint, string initialValue = "") : this()
    {
        TitleText.Text = title;
        HintAssist.SetHint(InputBox, hint);
        InputBox.Text = initialValue;
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            DialogHost.CloseDialogCommand.Execute(InputBox.Text, null);
    }
}
