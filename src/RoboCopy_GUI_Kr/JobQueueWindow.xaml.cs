using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RoboCopy_GUI_Kr.Core.Jobs;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr;

public partial class JobQueueWindow : Window
{
    private readonly JobQueue _queue;
    private readonly Func<RobocopyJobOptions> _templateFactory;
    private readonly ObservableCollection<JobQueueRow> _rows = [];
    private bool _isDragSelecting;

    public JobQueueWindow(JobQueue queue, Func<RobocopyJobOptions> templateFactory)
    {
        InitializeComponent();
        _queue = queue;
        _templateFactory = templateFactory;
        QueueDataGrid.ItemsSource = _rows;
        _queue.Changed += Queue_Changed;
        Closed += Window_Closed;
        RefreshRows();
    }

    public event EventHandler? RunnableJobAdded;

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var editor = new QueueJobEditorWindow(_templateFactory()) { Owner = this };
        if (editor.ShowDialog() != true || editor.ResultOptions is null)
        {
            return;
        }

        _queue.Enqueue(editor.ResultOptions);
        RunnableJobAdded?.Invoke(this, EventArgs.Empty);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var selectedRows = QueueDataGrid.SelectedItems.Cast<JobQueueRow>().ToArray();
        if (selectedRows.Length == 0)
        {
            MessageBox.Show(this, GetText("SelectQueueJob"), GetText("JobList"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (selectedRows.Length != 1)
        {
            MessageBox.Show(this, GetText("SelectSingleQueueJob"), GetText("JobList"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var row = selectedRows[0];

        var entry = _queue.Items.FirstOrDefault(item => item.Id == row.Id);
        if (entry is null)
        {
            RefreshRows();
            return;
        }

        if (entry.State == JobQueueEntryState.Running)
        {
            MessageBox.Show(this, GetText("RunningQueueJobLocked"), GetText("JobList"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editor = new QueueJobEditorWindow(entry.Options) { Owner = this };
        if (editor.ShowDialog() != true || editor.ResultOptions is null)
        {
            return;
        }

        if (_queue.Update(entry.Id, editor.ResultOptions))
        {
            RunnableJobAdded?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var selectedRows = QueueDataGrid.SelectedItems.Cast<JobQueueRow>().ToArray();
        if (selectedRows.Length == 0)
        {
            MessageBox.Show(this, GetText("SelectQueueJob"), GetText("JobList"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selectedIds = selectedRows.Select(row => row.Id).ToHashSet();
        if (_queue.Items.Any(item => selectedIds.Contains(item.Id) && item.State == JobQueueEntryState.Running))
        {
            MessageBox.Show(this, GetText("RunningQueueJobLocked"), GetText("JobList"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var prompt = selectedRows.Length == 1
            ? string.Format(
                CultureInfo.CurrentCulture,
                GetText("DeleteQueueJobPromptFormat"),
                Environment.NewLine,
                selectedRows[0].SourceDirectory,
                selectedRows[0].DestinationDirectory)
            : string.Format(
                CultureInfo.CurrentCulture,
                GetText("DeleteQueueJobsPromptFormat"),
                selectedRows.Length);
        if (MessageBox.Show(this, prompt, GetText("DeleteQueueJobTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var row in selectedRows)
        {
            _queue.Remove(row.Id);
        }
    }

    private void QueueDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row is not null && !row.IsSelected)
        {
            QueueDataGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }
    }

    private void QueueDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragSelecting = true;
        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row is null)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && !row.IsSelected)
        {
            QueueDataGrid.SelectedItems.Clear();
        }

        row.IsSelected = true;
    }

    private void QueueDataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragSelecting || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var hit = QueueDataGrid.InputHitTest(e.GetPosition(QueueDataGrid)) as DependencyObject;
        var row = FindVisualParent<DataGridRow>(hit);
        if (row is not null)
        {
            row.IsSelected = true;
        }
    }

    private void QueueDataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragSelecting = false;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Queue_Changed(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            RefreshRows();
        }
        else
        {
            _ = Dispatcher.InvokeAsync(RefreshRows);
        }
    }

    private void RefreshRows()
    {
        var selectedIds = QueueDataGrid.SelectedItems
            .Cast<JobQueueRow>()
            .Select(row => row.Id)
            .ToHashSet();
        _rows.Clear();

        var items = _queue.Items;
        for (var index = 0; index < items.Count; index++)
        {
            var entry = items[index];
            _rows.Add(new JobQueueRow(
                entry.Id,
                index + 1,
                GetText(GetStateResourceKey(entry.State)),
                entry.Options.SourceDirectory,
                entry.Options.DestinationDirectory));
        }

        foreach (var row in _rows.Where(row => selectedIds.Contains(row.Id)))
        {
            QueueDataGrid.SelectedItems.Add(row);
        }
        QueueSummaryTextBlock.Text = string.Format(
            CultureInfo.CurrentCulture,
            GetText("QueueSummaryFormat"),
            items.Count,
            items.Count(item => item.State == JobQueueEntryState.Waiting));
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _queue.Changed -= Queue_Changed;
    }

    private string GetText(string resourceKey)
    {
        return FindResource(resourceKey) as string ?? resourceKey;
    }

    private static string GetStateResourceKey(JobQueueEntryState state)
    {
        return state switch
        {
            JobQueueEntryState.Waiting => "QueueStateWaiting",
            JobQueueEntryState.Running => "QueueStateRunning",
            JobQueueEntryState.Completed => "QueueStateCompleted",
            JobQueueEntryState.Failed => "QueueStateFailed",
            JobQueueEntryState.Paused => "QueueStatePaused",
            JobQueueEntryState.Stopped => "QueueStateStopped",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
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

    private sealed record JobQueueRow(
        Guid Id,
        int Order,
        string State,
        string SourceDirectory,
        string DestinationDirectory);
}
