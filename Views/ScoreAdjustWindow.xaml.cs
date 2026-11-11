using System;
using System.Windows;
using MaterialDesignThemes.Wpf;
using PointManager.ViewModels;

namespace PointManager.Views;

public partial class ScoreAdjustWindow : TrayWindow
{
    private readonly ScoreAdjustViewModel _vm;
    private readonly SnackbarMessageQueue _snackbarQueue;

    public ScoreAdjustWindow()
    {
        InitializeComponent();
        _vm = new ScoreAdjustViewModel();
        DataContext = _vm;

        _snackbarQueue = new SnackbarMessageQueue(TimeSpan.FromMilliseconds(2000));
        Snackbar.MessageQueue = _snackbarQueue;
        _vm.Message += msg => _snackbarQueue.Enqueue(msg);
    }
}
