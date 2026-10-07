using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RoboCopy_GUI_Kr.Core.ContinuousSync;
using RoboCopy_GUI_Kr.Core.Sync;

namespace RoboCopy_GUI_Kr.Agent;

public sealed record AgentQuarantineRow(OneWaySyncQuarantineItem Item, string SizeText)
{
    public string FileName => Path.GetFileName(Item.RelativePath);
    public string QuarantinedAtText => Item.QuarantinedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string ByteText => Item.Size.ToString(CultureInfo.InvariantCulture);
}

public partial class AgentQuarantineWindow : Window
{
    private readonly ContinuousSyncConfiguration _configuration;
    private readonly OneWaySyncQuarantineService _service = new();
    private readonly HashSet<string> _restored = new(StringComparer.OrdinalIgnoreCase);

    public AgentQuarantineWindow(ContinuousSyncConfiguration configuration)
    {
        InitializeComponent();
        _configuration = configuration;
        RefreshItems();
    }

    public IReadOnlyList<string> RestoredRelativePaths => _restored.ToArray();

    public event EventHandler<QuarantineFilesRestoredEventArgs>? FilesRestored;

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var selected = ItemsDataGrid.SelectedItems.Cast<AgentQuarantineRow>().Select(row => row.Item).ToArray();
        if (selected.Length == 0)
        {
            Show(Text("SelectQuarantineFiles"), MessageBoxImage.Information);
            return;
        }

        if (System.Windows.MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Text("RestoreConfirmFormat"), selected.Length),
                Text("QuarantineTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await _service.RestoreAsync(
            _configuration.JobOptions.DestinationDirectory,
            _configuration.Id,
            selected);
        foreach (var path in result.RestoredRelativePaths)
        {
            _restored.Add(path);
        }

        if (result.RestoredRelativePaths.Count > 0)
        {
            FilesRestored?.Invoke(this, new QuarantineFilesRestoredEventArgs(result.RestoredRelativePaths));
        }

        RefreshItems();
        if (!result.IsSuccess)
        {
            Show(string.Join(Environment.NewLine, result.Failures.Select(failure => $"{failure.Path}: {failure.Reason}")), MessageBoxImage.Warning);
        }
        else
        {
            Show(string.Format(CultureInfo.CurrentCulture, Text("RestoreCompletedFormat"), result.RestoredRelativePaths.Count), MessageBoxImage.Information);
        }
    }

    private void RefreshItems()
    {
        ItemsDataGrid.ItemsSource = _service
            .GetItems(_configuration.JobOptions.DestinationDirectory, _configuration.Id)
            .Select(item => new AgentQuarantineRow(item, FormatSize(item.Size)))
            .ToArray();
    }

    private string FormatSize(long size)
    {
        var units = new[] { "SizeUnitBytes", "SizeUnitKb", "SizeUnitMb", "SizeUnitGb", "SizeUnitTb" };
        var value = (double)size;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        var human = index == 0
            ? string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", value, FindResource(units[index]))
            : string.Format(CultureInfo.CurrentCulture, "{0:N1} {1}", value, FindResource(units[index]));
        return human;
    }

    private async void PermanentDelete_Click(object sender, RoutedEventArgs e)
    {
        var selected = ItemsDataGrid.SelectedItems.Cast<AgentQuarantineRow>().Select(row => row.Item).ToArray();
        if (selected.Length == 0)
        {
            Show(Text("SelectQuarantineFilesToDelete"), MessageBoxImage.Information);
            return;
        }
        if (System.Windows.MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Text("PermanentDeleteConfirmFormat"), selected.Length),
                Text("PermanentDelete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        var result = await _service.DeletePermanentlyAsync(_configuration.JobOptions.DestinationDirectory, _configuration.Id, selected);
        RefreshItems();
        if (!result.IsSuccess)
        {
            Show(string.Join(Environment.NewLine, result.Failures.Select(failure => $"{failure.Path}: {failure.Reason}")), MessageBoxImage.Warning);
        }
        else
        {
            Show(string.Format(CultureInfo.CurrentCulture, Text("PermanentDeleteCompletedFormat"), result.DeletedFiles), MessageBoxImage.Information);
        }
    }

    private void ItemsDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row is not null && !row.IsSelected)
        {
            ItemsDataGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T value)
            {
                return value;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void Show(string message, MessageBoxImage image) => System.Windows.MessageBox.Show(
        this, message, Text("QuarantineTitle"), MessageBoxButton.OK, image);

    private string Text(string key) => FindResource(key) as string ?? key;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class QuarantineFilesRestoredEventArgs(IReadOnlyList<string> relativePaths) : EventArgs
{
    public IReadOnlyList<string> RelativePaths { get; } = relativePaths;
}
