using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RoboCopy_GUI_Kr.Core.ContinuousSync;

namespace RoboCopy_GUI_Kr.Agent;

public partial class AgentTaskConflictWindow : Window
{
    private readonly ContinuousSyncConfigurationRepository _repository;
    private readonly Action _configurationChanged;

    public AgentTaskConflictWindow(
        IReadOnlyList<ContinuousSyncConfiguration> conflicts,
        ContinuousSyncConfiguration candidate,
        ContinuousSyncConfigurationRepository repository,
        Action configurationChanged)
    {
        InitializeComponent();
        _repository = repository;
        _configurationChanged = configurationChanged;
        Rows = new ObservableCollection<AgentTaskConflictRow>(
            conflicts.DistinctBy(item => item.Id).Select(item => CreateRow(item, false))
                .Append(CreateRow(candidate, true)));
        DataContext = this;
    }

    public ObservableCollection<AgentTaskConflictRow> Rows { get; }
    public AgentConflictAction Action { get; private set; }
    public IReadOnlyList<ContinuousSyncConfiguration> SelectedConfigurations { get; private set; } = [];
    public ContinuousSyncConfiguration? ConfigurationToEdit { get; private set; }
    public bool ConfigurationToEditIsCandidate { get; private set; }

    private void UseSelected_Click(object sender, RoutedEventArgs e) => CompleteSelection(null);
    private void UseCurrent_Click(object sender, RoutedEventArgs e) => CompleteSelection(ContextRow(sender));

    private void ChangeSettings_Click(object sender, RoutedEventArgs e)
    {
        if (ContextRow(sender) is not { } row) return;
        ConfigurationToEdit = row.Configuration;
        ConfigurationToEditIsCandidate = row.IsCandidate;
        Action = AgentConflictAction.ChangeSettings;
        DialogResult = true;
    }

    private void ShowRegistered_Click(object sender, RoutedEventArgs e)
    {
        new AgentRegisteredTasksWindow(_repository, _configurationChanged) { Owner = this }.ShowDialog();
        var remainingIds = _repository.LoadAll().Select(item => item.Id).ToHashSet();
        foreach (var row in Rows.Where(row => !row.IsCandidate && !remainingIds.Contains(row.Configuration.Id)).ToArray()) Rows.Remove(row);
        if (Rows.All(row => row.IsCandidate))
        {
            Action = AgentConflictAction.RegisteredListChanged;
            DialogResult = true;
        }
    }

    private void CompleteSelection(AgentTaskConflictRow? contextRow)
    {
        if (contextRow is not null) contextRow.IsSelected = true;
        SelectedConfigurations = Rows.Where(row => row.IsSelected).Select(row => row.Configuration).ToArray();
        if (SelectedConfigurations.Count == 0)
        {
            System.Windows.MessageBox.Show(this, Text("AgentSelectTask"), Text("AgentConflictTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (SelectedConfigurations.SelectMany((configuration, index) =>
                SelectedConfigurations.Skip(index + 1)
                    .Where(other => ContinuousSyncConfigurationRepository.HasPathConflict(configuration, other)))
            .Any())
        {
            System.Windows.MessageBox.Show(this, Text("AgentConflictingSelection"), Text("AgentConflictTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Action = AgentConflictAction.UseSelected;
        DialogResult = true;
    }

    private AgentTaskConflictRow? ContextRow(object sender) =>
        (((sender as MenuItem)?.Parent as ContextMenu)?.PlacementTarget as DataGrid)?.SelectedItem as AgentTaskConflictRow
        ?? ConflictGrid.SelectedItem as AgentTaskConflictRow;

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid
            && e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(grid, source) is DataGridRow row)
        {
            grid.SelectedItem = row.Item;
        }
    }

    private AgentTaskConflictRow CreateRow(ContinuousSyncConfiguration configuration, bool isCandidate) => new()
    {
        Configuration = configuration,
        IsCandidate = isCandidate,
        Type = Text(isCandidate ? "AgentConflictNew" : "AgentConflictRegistered"),
        Source = configuration.JobOptions.SourceDirectory,
        Destination = configuration.JobOptions.DestinationDirectory,
        Interval = string.Format(CultureInfo.CurrentCulture, Text("AgentConflictIntervalFormat"), configuration.MonitorIntervalMinutes),
    };
    private string Text(string key) => FindResource(key) as string ?? key;
}

public enum AgentConflictAction { None, UseSelected, ChangeSettings, RegisteredListChanged }

public sealed class AgentTaskConflictRow
{
    public required ContinuousSyncConfiguration Configuration { get; init; }
    public bool IsCandidate { get; init; }
    public bool IsSelected { get; set; }
    public required string Type { get; init; }
    public required string Source { get; init; }
    public required string Destination { get; init; }
    public required string Interval { get; init; }
}
