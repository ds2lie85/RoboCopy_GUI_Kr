using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RoboCopy_GUI_Kr.Core.Paths;

namespace RoboCopy_GUI_Kr.Agent;

public sealed class AgentPathTextBlock : TextBlock
{
    private DataGridCell? _hostCell;

    public static readonly DependencyProperty FullPathProperty = DependencyProperty.Register(
        nameof(FullPath), typeof(string), typeof(AgentPathTextBlock),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnFullPathChanged));

    public AgentPathTextBlock()
    {
        TextTrimming = TextTrimming.CharacterEllipsis;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateDisplayText();
    }

    public string FullPath
    {
        get => (string)GetValue(FullPathProperty);
        set => SetValue(FullPathProperty, value);
    }

    private static void OnFullPathChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (AgentPathTextBlock)dependencyObject;
        control.ToolTip = e.NewValue as string;
        control.UpdateDisplayText();
    }

    private void UpdateDisplayText()
    {
        var hostWidth = _hostCell?.ActualWidth ?? ActualWidth;
        if (string.IsNullOrEmpty(FullPath) || hostWidth <= 0)
        {
            Text = FullPath;
            return;
        }

        if (_hostCell is not null)
        {
            Width = Math.Max(0, hostWidth - 10);
        }

        var availableWidth = Math.Max(0, hostWidth - Padding.Left - Padding.Right - 10);
        var best = PathDisplayFormatter.Compact(FullPath, 1);
        for (var maximumCharacters = FullPath.Length; maximumCharacters >= 1; maximumCharacters--)
        {
            var candidate = PathDisplayFormatter.Compact(FullPath, maximumCharacters);
            if (Measure(candidate) <= availableWidth)
            {
                best = candidate;
                break;
            }
        }

        Text = best;
    }

    private double Measure(string value) => new FormattedText(
        value,
        CultureInfo.CurrentUICulture,
        FlowDirection,
        new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
        FontSize,
        Foreground,
        VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hostCell = FindParent<DataGridCell>(this);
        if (_hostCell is not null)
        {
            _hostCell.SizeChanged += HostCell_SizeChanged;
        }
        UpdateDisplayText();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hostCell is not null)
        {
            _hostCell.SizeChanged -= HostCell_SizeChanged;
            _hostCell = null;
        }
    }

    private void HostCell_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateDisplayText();

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T value) return value;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}
