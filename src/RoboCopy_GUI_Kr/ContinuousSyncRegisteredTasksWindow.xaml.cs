using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RoboCopy_GUI_Kr.Core.ContinuousSync;

namespace RoboCopy_GUI_Kr;

public partial class ContinuousSyncRegisteredTasksWindow : Window
{
    private readonly ContinuousSyncConfigurationRepository _repository;
    private readonly ContinuousSyncAgentLauncher _launcher = new();

    public ContinuousSyncRegisteredTasksWindow(ContinuousSyncConfigurationRepository repository)
    {
        InitializeComponent();
        _repository = repository;
        Rows = new ObservableCollection<ContinuousSyncConflictRow>(repository.LoadAll().Select(CreateRow));
        DataContext = this;
    }

    public ObservableCollection<ContinuousSyncConflictRow> Rows { get; }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem && TaskGrid.SelectedItem is ContinuousSyncConflictRow contextRow)
        {
            contextRow.IsSelected = true;
        }

        var selected = Rows.Where(row => row.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            MessageBox.Show(this, GetText("ContinuousSyncSelectDelete"), GetText("ContinuousSyncRegisteredTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, GetText("ContinuousSyncDeleteSelectedConfirmFormat"), selected.Length),
                GetText("ContinuousSyncRegisteredTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _repository.Delete(selected.Select(row => row.Configuration.Id));
        _launcher.ReloadIfRunning();
        foreach (var row in selected)
        {
            Rows.Remove(row);
        }
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

    private ContinuousSyncConflictRow CreateRow(ContinuousSyncConfiguration configuration) => new()
    {
        Configuration = configuration,
        Type = GetText("ContinuousSyncRegisteredItem"),
        Source = configuration.JobOptions.SourceDirectory,
        Destination = configuration.JobOptions.DestinationDirectory,
        Interval = string.Format(CultureInfo.CurrentCulture, GetText("ContinuousSyncIntervalFormat"), configuration.MonitorIntervalMinutes),
    };

    private string GetText(string key) => FindResource(key) as string ?? key;
}
