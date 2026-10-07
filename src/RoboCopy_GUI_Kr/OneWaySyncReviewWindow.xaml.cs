using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RoboCopy_GUI_Kr.Core.Sync;

namespace RoboCopy_GUI_Kr;

public partial class OneWaySyncReviewWindow : Window
{
    private readonly ObservableCollection<OneWaySyncExtraRow> _rows;
    private bool _updatingSelectAll;
    private bool _updatingHierarchy;

    public OneWaySyncReviewWindow(IEnumerable<OneWaySyncExtraItem> items)
    {
        InitializeComponent();
        _rows = new ObservableCollection<OneWaySyncExtraRow>(items.Select(item =>
            new OneWaySyncExtraRow(
                item,
                FindResource(item.IsDirectory ? "DirectoryItem" : "FileItem") as string ?? string.Empty,
                FormatSize(item))));
        foreach (var row in _rows)
        {
            row.PropertyChanged += Row_PropertyChanged;
        }

        ExtraItemsDataGrid.ItemsSource = _rows;
        UpdateSelectAllState();
    }

    public IReadOnlyList<OneWaySyncExtraItem> SelectedForDeletion => _rows
        .Where(row => row.Delete)
        .Select(row => row.Item)
        .ToArray();

    private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e) => SetAll(delete: true);

    private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e) => SetAll(delete: false);

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
        if (e.PropertyName == nameof(OneWaySyncExtraRow.Delete))
        {
            if (_updatingHierarchy)
            {
                return;
            }

            if (sender is OneWaySyncExtraRow { Item.IsDirectory: true } directoryRow)
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

            if (sender is OneWaySyncExtraRow changedRow)
            {
                UpdateAncestorSelections(changedRow.RelativePath);
            }

            UpdateSelectAllState();
        }
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

    private void ExtraItemsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell?.Column.DisplayIndex != 0 || cell.DataContext is not OneWaySyncExtraRow row)
        {
            return;
        }

        row.Delete = !row.Delete;
        ExtraItemsDataGrid.SelectedItem = row;
        e.Handled = true;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
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
                : _rows.All(row => !row.Delete)
                    ? false
                    : null;
        }
        finally
        {
            _updatingSelectAll = false;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;

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
}
