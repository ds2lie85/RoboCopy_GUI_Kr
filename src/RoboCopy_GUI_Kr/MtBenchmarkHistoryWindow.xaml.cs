using System.Globalization;
using System.Windows;
using RoboCopy_GUI_Kr.Core.Performance;

namespace RoboCopy_GUI_Kr;

public partial class MtBenchmarkHistoryWindow : Window
{
    private readonly MtRecommendationService _recommendationService;
    private readonly string _sourcePath;
    private readonly string _destinationPath;

    public MtBenchmarkHistoryWindow(
        MtRecommendationService recommendationService,
        string sourcePath,
        string destinationPath)
    {
        InitializeComponent();
        _recommendationService = recommendationService;
        _sourcePath = sourcePath;
        _destinationPath = destinationPath;
        RefreshHistory();
    }

    public event EventHandler? HistoryChanged;

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = HistoryDataGrid.SelectedItems.Cast<MtBenchmarkHistoryRow>().ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        var prompt = string.Format(
            CultureInfo.CurrentCulture,
            GetText("DeleteMtBenchmarkHistoryPromptFormat"),
            selected.Length);
        if (MessageBox.Show(
                this,
                prompt,
                GetText("DeleteMtBenchmarkHistoryTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var changed = false;
        foreach (var row in selected)
        {
            changed |= _recommendationService.RemoveBenchmarkHistory(
                _sourcePath,
                _destinationPath,
                row.ThreadCount);
        }

        if (changed)
        {
            RefreshHistory();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RefreshHistory()
    {
        var rows = _recommendationService
            .GetBenchmarkHistory(_sourcePath, _destinationPath)
            .Select(entry => new MtBenchmarkHistoryRow(
                entry.ThreadCount,
                string.Format(
                    CultureInfo.CurrentCulture,
                    GetText("MtBenchmarkSpeedDisplayFormat"),
                    entry.AverageBytesPerSecond / 1024 / 1024,
                    (entry.LatestBytesPerSecond > 0
                        ? entry.LatestBytesPerSecond
                        : entry.AverageBytesPerSecond) / 1024 / 1024),
                entry.SampleCount,
                entry.TestedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)))
            .ToArray();
        HistoryDataGrid.ItemsSource = rows;
        SummaryTextBlock.Text = rows.Length == 0
            ? GetText("MtBenchmarkHistoryEmpty")
            : string.Format(
                CultureInfo.CurrentCulture,
                GetText("MtBenchmarkHistoryCountFormat"),
                rows.Length);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private string GetText(string resourceKey)
    {
        return FindResource(resourceKey) as string ?? resourceKey;
    }

    private sealed record MtBenchmarkHistoryRow(
        int ThreadCount,
        string SpeedDisplay,
        int SampleCount,
        string TestedAt);
}
