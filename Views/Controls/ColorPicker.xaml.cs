using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using PointManager.ViewModels;

namespace PointManager.Views.Controls;

/// <summary>
/// A color grid: recently used colors (optional) above the preset swatches.
/// The selected entry is marked by a white badge with a black check at its bottom-right.
/// </summary>
public partial class ColorPicker : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(ColorPicker),
        new PropertyMetadata(null, OnSourceOrSelectionChanged));

    public static readonly DependencyProperty RecentSourceProperty = DependencyProperty.Register(
        nameof(RecentSource), typeof(IEnumerable), typeof(ColorPicker),
        new PropertyMetadata(null, OnSourceOrSelectionChanged));

    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(
        nameof(SelectedValue), typeof(string), typeof(ColorPicker),
        new PropertyMetadata(null, OnSourceOrSelectionChanged));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IEnumerable? RecentSource
    {
        get => (IEnumerable?)GetValue(RecentSourceProperty);
        set => SetValue(RecentSourceProperty, value);
    }

    public string? SelectedValue
    {
        get => (string?)GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public ColorPicker()
    {
        InitializeComponent();
    }

    private static void OnSourceOrSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ColorPicker)d).RebuildTiles();

    private void RebuildTiles()
    {
        BuildPanel(RecentTiles, RecentSource);
        BuildPanel(Tiles, ItemsSource);

        var hasRecent = RecentTiles.Children.Count > 0;
        RecentHeader.Visibility = hasRecent ? Visibility.Visible : Visibility.Collapsed;
        RecentTiles.Visibility = hasRecent ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BuildPanel(WrapPanel panel, IEnumerable? source)
    {
        panel.Children.Clear();
        if (source is null) return;

        var tileStyle = (Style)Resources["TileButton"];
        foreach (var item in source)
        {
            if (item is not SwatchOption opt) continue;

            var selected = string.Equals(opt.Name, SelectedValue, StringComparison.OrdinalIgnoreCase);
            var button = new Button
            {
                Style = tileStyle,
                Background = opt.Color,
                Tag = opt.Name,
                ToolTip = opt.Name,
            };
            if (selected)
            {
                button.Content = new PackIcon
                {
                    Kind = PackIconKind.Check,
                    Width = 14,
                    Height = 14,
                    Background = Brushes.White,
                    Foreground = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                };
            }
            var name = opt.Name;
            button.Click += (_, _) => SelectedValue = name;
            panel.Children.Add(button);
        }
    }
}
