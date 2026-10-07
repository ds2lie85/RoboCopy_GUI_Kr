using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RoboCopy_GUI_Kr.Core.Exclusions;
using RoboCopy_GUI_Kr.Exclusions;

namespace RoboCopy_GUI_Kr;

public partial class ExclusionWindow : Window
{
    private readonly ExclusionSelectionState _selectionState;
    private readonly ObservableCollection<string> _patterns;
    private bool _syncingCommonPatterns;
    private Point? _patternDragStart;
    private int _patternDragAnchor = -1;
    private bool _isPatternDragging;
    private string[] _patternSelectionBeforeDrag = [];

    public ExclusionWindow(string sourceDirectory, IEnumerable<string> directories, IEnumerable<string> files)
    {
        var physicalFiles = files.Where(IsPhysicalPath).ToArray();
        _patterns = new ObservableCollection<string>(files.Where(file => !IsPhysicalPath(file)));
        _selectionState = new ExclusionSelectionState(directories, physicalFiles);

        RootNodes =
        [
            new ExclusionTreeNode(Path.GetFullPath(sourceDirectory), true, _selectionState),
        ];

        InitializeComponent();
        DataContext = this;
        PatternListBox.ItemsSource = _patterns;
        SyncCommonPatternCheckBoxes();
        RootNodes[0].IsExpanded = true;
    }

    public ObservableCollection<ExclusionTreeNode> RootNodes { get; }

    public IReadOnlyList<string> ExcludedDirectories => _selectionState.Directories;

    public IReadOnlyList<string> ExcludedFiles => _selectionState.Files.Concat(_patterns).ToArray();

    private void TreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: ExclusionTreeNode node })
        {
            node.EnsureChildrenLoaded();
        }
    }

    private void AddPattern_Click(object sender, RoutedEventArgs e)
    {
        var pattern = PatternTextBox.Text.Trim();
        if (pattern.Length == 0)
        {
            return;
        }

        AddPattern(pattern);
        PatternTextBox.Clear();
        PatternTextBox.Focus();
    }

    private void RemovePatterns_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in PatternListBox.SelectedItems.Cast<string>().ToArray())
        {
            _patterns.Remove(item);
        }

        SyncCommonPatternCheckBoxes();
    }

    private void PatternListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item is not null && !item.IsSelected)
        {
            PatternListBox.SelectedItems.Clear();
            item.IsSelected = true;
        }
    }

    private void PatternListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        _patternDragStart = e.GetPosition(PatternListBox);
        _patternDragAnchor = item is null ? -1 : PatternListBox.Items.IndexOf(item.DataContext);
        _patternSelectionBeforeDrag = PatternListBox.SelectedItems.Cast<string>().ToArray();
        _isPatternDragging = false;
        if (_patternDragAnchor >= 0)
        {
            _ = Mouse.Capture(PatternListBox, CaptureMode.SubTree);
        }
    }

    private void PatternListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _patternDragStart is not Point start
            || _patternDragAnchor < 0)
        {
            return;
        }

        var current = e.GetPosition(PatternListBox);
        if (!_isPatternDragging
            && Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var item = FindPatternItemAt(current);
        if (item is null)
        {
            return;
        }

        _isPatternDragging = true;
        _ = PatternListBox.CaptureMouse();
        var currentIndex = PatternListBox.Items.IndexOf(item.DataContext);
        var first = Math.Min(_patternDragAnchor, currentIndex);
        var last = Math.Max(_patternDragAnchor, currentIndex);
        PatternListBox.SelectedItems.Clear();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            foreach (var selected in _patternSelectionBeforeDrag)
            {
                PatternListBox.SelectedItems.Add(selected);
            }
        }

        for (var index = first; index <= last; index++)
        {
            PatternListBox.SelectedItems.Add(PatternListBox.Items[index]);
        }

        e.Handled = true;
    }

    private void PatternListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (PatternListBox.IsMouseCaptured)
        {
            PatternListBox.ReleaseMouseCapture();
        }

        if (_isPatternDragging)
        {
            e.Handled = true;
        }

        _patternDragStart = null;
        _patternDragAnchor = -1;
        _isPatternDragging = false;
        _patternSelectionBeforeDrag = [];
    }

    private void CommonPattern_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingCommonPatterns || sender is not ToggleButton { Tag: string pattern } toggle)
        {
            return;
        }

        if (toggle.IsChecked == true)
        {
            AddPattern(pattern);
        }
        else
        {
            var existing = _patterns.FirstOrDefault(item => string.Equals(item, pattern, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                _patterns.Remove(existing);
            }
        }

        SyncCommonPatternCheckBoxes();
    }

    private void AddPattern(string pattern)
    {
        if (!_patterns.Any(item => string.Equals(item, pattern, StringComparison.OrdinalIgnoreCase)))
        {
            _patterns.Add(pattern);
        }

        SyncCommonPatternCheckBoxes();
    }

    private void SyncCommonPatternCheckBoxes()
    {
        _syncingCommonPatterns = true;
        try
        {
            foreach (var toggle in new[] { TmpPatternToggle, BakPatternToggle, ThumbsPatternToggle })
            {
                toggle.IsChecked = toggle.Tag is string pattern
                    && _patterns.Any(item => string.Equals(item, pattern, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            _syncingCommonPatterns = false;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private static bool IsPhysicalPath(string value)
    {
        return !value.Contains('*', StringComparison.Ordinal)
            && !value.Contains('?', StringComparison.Ordinal)
            && Path.IsPathFullyQualified(value);
    }

    private static T? FindVisualParent<T>(DependencyObject? child)
        where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match)
            {
                return match;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private ListBoxItem? FindPatternItemAt(Point point)
    {
        var hitItem = FindVisualParent<ListBoxItem>(PatternListBox.InputHitTest(point) as DependencyObject);
        if (hitItem is not null)
        {
            return hitItem;
        }

        ListBoxItem? nearest = null;
        var nearestDistance = double.MaxValue;
        for (var index = 0; index < PatternListBox.Items.Count; index++)
        {
            if (PatternListBox.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem candidate)
            {
                continue;
            }

            var top = candidate.TranslatePoint(new Point(0, 0), PatternListBox).Y;
            var bottom = top + candidate.ActualHeight;
            var distance = point.Y < top ? top - point.Y : point.Y > bottom ? point.Y - bottom : 0;
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }
}
