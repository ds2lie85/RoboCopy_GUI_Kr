using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Windows;
using RoboCopy_GUI_Kr.Core.Errors;

namespace RoboCopy_GUI_Kr;

public partial class ErrorFilesWindow : Window
{
    private readonly RestartManagerService _restartManager = new();
    private readonly ObservableCollection<ErrorFileRow> _rows;

    public ErrorFilesWindow(ObservableCollection<ErrorFileRow> rows)
    {
        InitializeComponent();
        _rows = rows;
        ErrorDataGrid.ItemsSource = _rows;
        _rows.CollectionChanged += Rows_CollectionChanged;
        foreach (var row in _rows)
        {
            row.PropertyChanged += Row_PropertyChanged;
        }

        Closed += Window_Closed;
        RefreshSummary();
    }

    public event EventHandler<RetryErrorsRequestedEventArgs>? RetryRequested;

    public event EventHandler<RetryErrorsRequestedEventArgs>? ElevatedRetryRequested;

    private void RetrySelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedRows().Where(row => row.CanRetry).ToArray();
        if (selected.Length == 0)
        {
            ShowSelectItemsMessage();
            return;
        }

        RetryRequested?.Invoke(this, new RetryErrorsRequestedEventArgs(selected));
        MessageBox.Show(this, GetText("RetryQueued"), GetText("ErrorFilesTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RetrySelectedAsAdministrator_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedRows()
            .Where(row => row.CanRetry && row.Item.Category == RobocopyErrorCategory.AccessDenied)
            .ToArray();
        if (selected.Length == 0)
        {
            MessageBox.Show(this, GetText("SelectAccessDeniedFiles"), GetText("ErrorFilesTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ElevatedRetryRequested?.Invoke(this, new RetryErrorsRequestedEventArgs(selected));
    }

    private void CheckLockingProcesses_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedRows();
        if (selected.Length == 0)
        {
            ShowSelectItemsMessage();
            return;
        }

        try
        {
            var paths = selected
                .Select(row => File.Exists(row.SourcePath) ? row.SourcePath : row.DestinationPath)
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var processes = _restartManager.GetLockingProcesses(paths);
            if (processes.Count == 0)
            {
                MessageBox.Show(this, GetText("NoLockingProcesses"), GetText("LockingProcessesTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new LockingProcessesWindow(processes, paths) { Owner = this };
            _ = window.ShowDialog();
            if (window.RetryRequested)
            {
                var retryRows = selected.Where(row => row.CanRetry).ToArray();
                if (retryRows.Length > 0)
                {
                    RetryRequested?.Invoke(this, new RetryErrorsRequestedEventArgs(retryRows));
                }
            }
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("LockingProcessLookupFailedFormat"), exception.Message),
                GetText("LockingProcessesTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenSourceLocation_Click(object sender, RoutedEventArgs e) => OpenSinglePath(useDestination: false);

    private void OpenDestinationLocation_Click(object sender, RoutedEventArgs e) => OpenSinglePath(useDestination: true);

    private void OpenSinglePath(bool useDestination)
    {
        var selected = SelectedRows();
        if (selected.Length != 1)
        {
            MessageBox.Show(this, GetText("SelectSingleErrorFile"), GetText("ErrorFilesTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var path = useDestination ? selected[0].DestinationPath : selected[0].SourcePath;
        try
        {
            ExplorerLocationOpener.Open(path);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("OpenLocationFailedFormat"), exception.Message),
                GetText("ErrorFilesTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private ErrorFileRow[] SelectedRows() => ErrorDataGrid.SelectedItems.Cast<ErrorFileRow>().ToArray();

    private void ShowSelectItemsMessage() =>
        MessageBox.Show(this, GetText("SelectErrorFiles"), GetText("ErrorFilesTitle"), MessageBoxButton.OK, MessageBoxImage.Information);

    private void Rows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var row in e.OldItems.Cast<ErrorFileRow>())
            {
                row.PropertyChanged -= Row_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var row in e.NewItems.Cast<ErrorFileRow>())
            {
                row.PropertyChanged += Row_PropertyChanged;
            }
        }

        RefreshSummary();
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshSummary();

    private void RefreshSummary()
    {
        var resolved = _rows.Count(row => row.RetryState == ErrorFileRetryState.Resolved);
        SummaryTextBlock.Text = string.Format(
            CultureInfo.CurrentCulture,
            GetText("ErrorFilesStatusFormat"),
            _rows.Count,
            _rows.Count - resolved,
            resolved);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _rows.CollectionChanged -= Rows_CollectionChanged;
        foreach (var row in _rows)
        {
            row.PropertyChanged -= Row_PropertyChanged;
        }
    }

    private string GetText(string resourceKey) => FindResource(resourceKey) as string ?? resourceKey;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class RetryErrorsRequestedEventArgs(IReadOnlyList<ErrorFileRow> rows) : EventArgs
{
    public IReadOnlyList<ErrorFileRow> Rows { get; } = rows;
}
