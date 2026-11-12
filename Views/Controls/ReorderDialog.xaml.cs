using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using PointManager.Models;

namespace PointManager.Views.Controls;

public partial class ReorderDialog : UserControl
{
    private readonly ObservableCollection<Student> _items;

    public ReorderDialog(IEnumerable<Student> students)
    {
        InitializeComponent();
        _items = new ObservableCollection<Student>(students);
        OrderList.ItemsSource = _items;
        OrderList.SelectedIndex = _items.Count > 0 ? 0 : -1;
        UpdateButtons();
    }

    private void OrderList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        var i = OrderList.SelectedIndex;
        MoveUpButton.IsEnabled = i > 0;
        MoveDownButton.IsEnabled = i >= 0 && i < _items.Count - 1;
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var i = OrderList.SelectedIndex;
        if (i <= 0) return;
        _items.Move(i, i - 1);
        OrderList.SelectedIndex = i - 1;
        // Moving the selected item does not raise SelectionChanged.
        UpdateButtons();
        OrderList.ScrollIntoView(OrderList.SelectedItem);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var i = OrderList.SelectedIndex;
        if (i < 0 || i >= _items.Count - 1) return;
        _items.Move(i, i + 1);
        OrderList.SelectedIndex = i + 1;
        UpdateButtons();
        OrderList.ScrollIntoView(OrderList.SelectedItem);
    }
}