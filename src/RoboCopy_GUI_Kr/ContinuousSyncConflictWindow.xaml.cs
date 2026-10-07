using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RoboCopy_GUI_Kr.Core.ContinuousSync;

namespace RoboCopy_GUI_Kr;

public partial class ContinuousSyncConflictWindow : Window
{
    private readonly ContinuousSyncConfigurationRepository _repository;

    public ContinuousSyncConflictWindow(
        IReadOnlyList<ContinuousSyncConfiguration> conflicts,
        IReadOnlyList<ContinuousSyncConfiguration> candidates,
        ContinuousSyncConfigurationRepository repository)
    {
        InitializeComponent();
        _repository = repository;
        Rows = new ObservableCollection<ContinuousSyncConflictRow>(
            conflicts.DistinctBy(item => item.Id).Select(item => CreateRow(item, false))
                .Concat(candidates.DistinctBy(item => item.Id).Select(item => CreateRow(item, true))));
        DataContext = this;
    }

    public ObservableCollection<ContinuousSyncConflictRow> Rows { get; }

    public ContinuousSyncConflictAction Action { get; private set; }

    public IReadOnlyList<ContinuousSyncConfiguration> SelectedConfigurations { get; private set; } = [];

    public event EventHandler<ContinuousSyncConfigureRequestEventArgs>? ConfigureRequested;

    private void UseSelected_Click(object sender, RoutedEventArgs e) => CompleteUseSelection(null);

    private void UseCurrentTask_Click(object sender, RoutedEventArgs e) =>
        CompleteUseSelection(GetContextRow(sender));

    private void ChangeSettings_Click(object sender, RoutedEventArgs e)
    {
        var row = GetContextRow(sender);
        if (row is null)
        {
            return;
        }

        var request = new ContinuousSyncConfigureRequestEventArgs(row.Configuration, row.IsCandidate);
        ConfigureRequested?.Invoke(this, request);
        if (request.Applied)
        {
            Action = ContinuousSyncConflictAction.ConfigurationApplied;
            DialogResult = true;
        }
    }

    private void ShowRegistered_Click(object sender, RoutedEventArgs e)
    {
        var window = new ContinuousSyncRegisteredTasksWindow(_repository) { Owner = this };
        window.ShowDialog();
        var remainingIds = _repository.LoadAll().Select(item => item.Id).ToHashSet();
        foreach (var row in Rows.Where(row => !row.IsCandidate && !remainingIds.Contains(row.Configuration.Id)).ToArray())
        {
            Rows.Remove(row);
        }

        if (Rows.All(row => row.IsCandidate))
        {
            Action = ContinuousSyncConflictAction.RegisteredListChanged;
            DialogResult = true;
        }
    }

    private void CompleteUseSelection(ContinuousSyncConflictRow? contextRow)
    {
        if (contextRow is not null)
        {
            contextRow.IsSelected = true;
        }

        SelectedConfigurations = Rows.Where(row => row.IsSelected)
            .Select(row => row.Configuration)
            .ToArray();
        if (SelectedConfigurations.Count == 0)
        {
            MessageBox.Show(this, GetText("ContinuousSyncSelectTask"), GetText("ContinuousSyncConflictTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (SelectedConfigurations.SelectMany((configuration, index) =>
                SelectedConfigurations.Skip(index + 1)
                    .Where(other => ContinuousSyncConfigurationRepository.HasPathConflict(configuration, other)))
            .Any())
        {
            MessageBox.Show(this, GetText("ContinuousSyncConflictingSelection"), GetText("ContinuousSyncConflictTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Action = ContinuousSyncConflictAction.UseSelected;
        DialogResult = true;
    }

    private ContinuousSyncConflictRow? GetContextRow(object sender)
    {
        var row = ((sender as MenuItem)?.Parent as ContextMenu)?.PlacementTarget is DataGrid grid
            ? grid.SelectedItem as ContinuousSyncConflictRow
            : null;
        return row ?? ConflictGrid.SelectedItem as ContinuousSyncConflictRow;
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid
            && e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(grid, source) is DataGridRow row)
        {
            grid.SelectedItem = row.Item;
        }
    }

    private ContinuousSyncConflictRow CreateRow(ContinuousSyncConfiguration configuration, bool isCandidate) => new()
    {
        Configuration = configuration,
        IsCandidate = isCandidate,
        Type = GetText(isCandidate ? "ContinuousSyncNewItem" : "ContinuousSyncRegisteredItem"),
        Source = configuration.JobOptions.SourceDirectory,
        Destination = configuration.JobOptions.DestinationDirectory,
        Interval = string.Format(CultureInfo.CurrentCulture, GetText("ContinuousSyncIntervalFormat"), configuration.MonitorIntervalMinutes),
    };

    private string GetText(string key) => FindResource(key) as string ?? key;
}

public enum ContinuousSyncConflictAction
{
    None,
    UseSelected,
    ConfigurationApplied,
    RegisteredListChanged,
}

public sealed class ContinuousSyncConfigureRequestEventArgs(
    ContinuousSyncConfiguration configuration,
    bool isCandidate) : EventArgs
{
    public ContinuousSyncConfiguration Configuration { get; } = configuration;
    public bool IsCandidate { get; } = isCandidate;
    public bool Applied { get; set; }
}

public sealed class ContinuousSyncConflictRow
{
    public required ContinuousSyncConfiguration Configuration { get; init; }
    public bool IsCandidate { get; init; }
    public bool IsSelected { get; set; }
    public required string Type { get; init; }
    public required string Source { get; init; }
    public required string Destination { get; init; }
    public required string Interval { get; init; }
}
