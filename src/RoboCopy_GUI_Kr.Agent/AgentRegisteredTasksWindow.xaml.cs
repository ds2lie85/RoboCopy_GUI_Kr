using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RoboCopy_GUI_Kr.Core.ContinuousSync;

namespace RoboCopy_GUI_Kr.Agent;

public partial class AgentRegisteredTasksWindow : Window
{
    private readonly ContinuousSyncConfigurationRepository _repository;
    private readonly Action _configurationChanged;
    public AgentRegisteredTasksWindow(ContinuousSyncConfigurationRepository repository, Action configurationChanged)
    {
        InitializeComponent();
        _repository = repository;
        _configurationChanged = configurationChanged;
        Rows = new ObservableCollection<AgentTaskConflictRow>(repository.LoadAll().Select(configuration => new AgentTaskConflictRow
        {
            Configuration = configuration,
            Type = Text("AgentConflictRegistered"),
            Source = configuration.JobOptions.SourceDirectory,
            Destination = configuration.JobOptions.DestinationDirectory,
            Interval = string.Format(CultureInfo.CurrentCulture, Text("AgentConflictIntervalFormat"), configuration.MonitorIntervalMinutes),
        }));
        DataContext = this;
    }
    public ObservableCollection<AgentTaskConflictRow> Rows { get; }
    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem && TaskGrid.SelectedItem is AgentTaskConflictRow contextRow) contextRow.IsSelected = true;
        var selected = Rows.Where(row => row.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            System.Windows.MessageBox.Show(this, Text("AgentSelectDelete"), Text("AgentRegisteredTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (System.Windows.MessageBox.Show(this, string.Format(CultureInfo.CurrentCulture, Text("AgentDeleteSelectedConfirmFormat"), selected.Length),
                Text("AgentRegisteredTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _repository.Delete(selected.Select(row => row.Configuration.Id));
        _configurationChanged();
        foreach (var row in selected) Rows.Remove(row);
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
    private string Text(string key) => FindResource(key) as string ?? key;
}
