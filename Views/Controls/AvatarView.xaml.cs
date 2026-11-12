using System.Windows;
using System.Windows.Controls;
using PointManager.Models;

namespace PointManager.Views.Controls;

/// <summary>Round avatar: the student's photo when set, otherwise the first character of the name.</summary>
public partial class AvatarView : UserControl
{
    public static readonly DependencyProperty StudentProperty = DependencyProperty.Register(
        nameof(Student), typeof(Student), typeof(AvatarView), new PropertyMetadata(null));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(AvatarView), new PropertyMetadata(32d));

    public Student? Student
    {
        get => (Student?)GetValue(StudentProperty);
        set => SetValue(StudentProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public AvatarView() => InitializeComponent();
}