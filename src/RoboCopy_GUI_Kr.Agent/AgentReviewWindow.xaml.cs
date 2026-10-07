using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RoboCopy_GUI_Kr.Core.Sync;

namespace RoboCopy_GUI_Kr.Agent;

public partial class AgentReviewWindow : Window
{
    private readonly ObservableCollection<AgentReviewRow> _rows;
    private readonly IReadOnlyList<OneWaySyncExtraItem> _retainedItems;
    private IReadOnlyList<OneWaySyncExtraItem> _selectedRetainedForDeletion = [];
    private bool _updatingSelectAll;
    private bool _updatingHierarchy;

    public AgentReviewWindow(
        IEnumerable<OneWaySyncExtraItem> items,
        IEnumerable<OneWaySyncExtraItem>? retainedItems = null,
        bool retainedListMode = false)
    {
        InitializeComponent();
        _retainedItems = retainedItems?.ToArray() ?? [];
        _rows = new ObservableCollection<AgentReviewRow>(items.Select(item => new AgentReviewRow(
            item,
            FindResource(item.IsDirectory ? "Folder" : "File") as string ?? string.Empty,
            FormatSize(item))));
        foreach (var row in _rows)
        {
            row.PropertyChanged += Row_PropertyChanged;
        }

        ItemsDataGrid.ItemsSource = _rows;
        RetainedListButton.Visibility = !retainedListMode && _retainedItems.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (retainedListMode)
        {
            Title = FindResource("RetainedListTitle") as string ?? string.Empty;
            HeadingTextBlock.Text = FindResource("RetainedListHeading") as string ?? string.Empty;
            InstructionTextBlock.Text = FindResource("RetainedListInstruction") as string ?? string.Empty;
        }
        UpdateSelectAllState();
    }

    public IReadOnlyList<OneWaySyncExtraItem> SelectedForDeletion => _rows
        .Where(row => row.Delete)
        .Select(row => row.Item)
        .ToArray();

    public IReadOnlyList<OneWaySyncExtraItem> SelectedRetainedForDeletion => _selectedRetainedForDeletion
        .Where(item => _rows.Any(row => row.Delete
            && string.Equals(row.RelativePath, item.RelativePath, StringComparison.OrdinalIgnoreCase)))
        .ToArray();

    private void RetainedList_Click(object sender, RoutedEventArgs e)
    {
        var alreadyReturned = _rows.Select(row => row.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var window = new AgentReviewWindow(
            _retainedItems.Where(item => !alreadyReturned.Contains(item.RelativePath)),
            retainedListMode: true) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _selectedRetainedForDeletion = _selectedRetainedForDeletion
                .Concat(window.SelectedForDeletion)
                .DistinctBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var item in window.SelectedForDeletion)
            {
                var row = new AgentReviewRow(
                    item,
                    FindResource(item.IsDirectory ? "Folder" : "File") as string ?? string.Empty,
                    FormatSize(item));
                row.PropertyChanged += Row_PropertyChanged;
                row.Delete = true;
                _rows.Add(row);
            }
            RetainedListButton.Visibility = _rows.Count(row =>
                _retainedItems.Any(item => string.Equals(item.RelativePath, row.RelativePath, StringComparison.OrdinalIgnoreCase)))
                < _retainedItems.Count ? Visibility.Visible : Visibility.Collapsed;
            UpdateSelectAllState();
        }
    }

    private string FormatSize(OneWaySyncExtraItem item)
    {
        if (item.IsDirectory) return string.Empty;
        var units = new[] { "SizeUnitBytes", "SizeUnitKb", "SizeUnitMb", "SizeUnitGb", "SizeUnitTb" };
        var value = (double)item.Size;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        var human = index == 0
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:N0} {1}", value, FindResource(units[index]))
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:N1} {1}", value, FindResource(units[index]));
        return human;
    }

    private void SelectAll_Checked(object sender, RoutedEventArgs e) => SetAll(true);

    private void SelectAll_Unchecked(object sender, RoutedEventArgs e) => SetAll(false);

    private void SetAll(bool delete)
    {
        if (_updatingSelectAll)
        {
            return;
        }

        _updatingHierarchy = true;
        try
        {
            foreach (var row in _rows)
            {
                row.Delete = delete;
            }
        }
        finally
        {
            _updatingHierarchy = false;
        }

        UpdateSelectAllState();
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AgentReviewRow.Delete) || _updatingHierarchy)
        {
            return;
        }

        if (sender is AgentReviewRow { Item.IsDirectory: true } directoryRow)
        {
            _updatingHierarchy = true;
            try
            {
                foreach (var descendant in _rows.Where(row =>
                             OneWaySyncSelection.IsDescendant(row.RelativePath, directoryRow.RelativePath)))
                {
                    descendant.Delete = directoryRow.Delete;
                }
            }
            finally
            {
                _updatingHierarchy = false;
            }
        }

        if (sender is AgentReviewRow changedRow)
        {
            UpdateAncestorSelections(changedRow.RelativePath);
        }

        UpdateSelectAllState();
    }

    private void UpdateAncestorSelections(string changedRelativePath)
    {
        var ancestors = _rows
            .Where(row => row.Item.IsDirectory
                          && OneWaySyncSelection.IsDescendant(changedRelativePath, row.RelativePath))
            .OrderByDescending(row => row.RelativePath.Length)
            .ToArray();
        if (ancestors.Length == 0)
        {
            return;
        }

        _updatingHierarchy = true;
        try
        {
            foreach (var ancestor in ancestors)
            {
                ancestor.Delete = OneWaySyncSelection.AreAllDescendantsSelected(
                    _rows.Select(row => (row.RelativePath, row.Delete)),
                    ancestor.RelativePath);
            }
        }
        finally
        {
            _updatingHierarchy = false;
        }
    }

    private void ItemsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var cell = FindParent<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell?.Column.DisplayIndex != 0 || cell.DataContext is not AgentReviewRow row)
        {
            return;
        }

        row.Delete = !row.Delete;
        ItemsDataGrid.SelectedItem = row;
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T parent)
            {
                return parent;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void UpdateSelectAllState()
    {
        _updatingSelectAll = true;
        try
        {
            SelectAllCheckBox.IsChecked = _rows.All(row => row.Delete)
                ? true
                : _rows.All(row => !row.Delete) ? false : null;
        }
        finally
        {
            _updatingSelectAll = false;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
