using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using RoboCopy_GUI_Kr.Core.ContinuousSync;
using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Performance;

namespace RoboCopy_GUI_Kr;

public partial class ContinuousSyncSettingsWindow : Window
{
    private readonly RobocopyJobOptions _jobOptions;
    private readonly Guid _configurationId;
    private readonly IReadOnlyList<string> _retainedDestinationOnlyPaths;
    private readonly MtRecommendationService _mtRecommendationService = new();
    private readonly MtBenchmarkService _mtBenchmarkService = new();
    private IReadOnlyList<string> _excludedDirectories;
    private IReadOnlyList<string> _excludedFiles;
    private CancellationTokenSource? _benchmarkCancellation;

    public ContinuousSyncSettingsWindow(
        RobocopyJobOptions jobOptions,
        ContinuousSyncConfiguration? existingConfiguration)
    {
        InitializeComponent();
        _jobOptions = (existingConfiguration?.JobOptions ?? jobOptions) with
        {
            OperationMode = JobOperationMode.UpdateSync,
            RetryCount = 0,
            RetryWaitSeconds = 0,
            Restartable = true,
            DryRun = false,
            LogFilePath = null,
        };
        _configurationId = existingConfiguration?.Id ?? Guid.NewGuid();
        _retainedDestinationOnlyPaths = existingConfiguration?.RetainedDestinationOnlyPaths ?? [];
        _excludedDirectories = _jobOptions.ExcludedDirectories;
        _excludedFiles = _jobOptions.ExcludedFiles;
        SourceTextBox.Text = _jobOptions.SourceDirectory;
        DestinationTextBox.Text = _jobOptions.DestinationDirectory;
        IncludeEmptyCheckBox.IsChecked = _jobOptions.IncludeEmptyDirectories;
        ExcludeZeroByteCheckBox.IsChecked = _jobOptions.ExcludeZeroByteFiles;
        SelectSymbolicLinkPolicy(_jobOptions.SymbolicLinkPolicy);

        if (existingConfiguration is not null)
        {
            IntervalTextBox.Text = existingConfiguration.MonitorIntervalMinutes.ToString(CultureInfo.InvariantCulture);
            DeleteDestinationOnlyRadioButton.IsChecked = existingConfiguration.DestinationOnlyPolicy == DestinationOnlyPolicy.Delete;
            AllowDestinationOnlyRadioButton.IsChecked = existingConfiguration.DestinationOnlyPolicy == DestinationOnlyPolicy.Allow;
            AskDestinationOnlyRadioButton.IsChecked = existingConfiguration.DestinationOnlyPolicy == DestinationOnlyPolicy.Ask;
            AlwaysScheduleRadioButton.IsChecked = !existingConfiguration.LimitToSchedule;
            ScheduledRadioButton.IsChecked = existingConfiguration.LimitToSchedule;
            ScheduleStartTextBox.Text = FormatMinute(existingConfiguration.ScheduleStartMinute);
            ScheduleEndTextBox.Text = FormatMinute(existingConfiguration.ScheduleEndMinute == 1440 ? 0 : existingConfiguration.ScheduleEndMinute);
            PauseOnBatteryCheckBox.IsChecked = existingConfiguration.PauseOnBattery;
            PauseOnMeteredCheckBox.IsChecked = existingConfiguration.PauseOnMeteredNetwork;
            QuarantineCheckBox.IsChecked = existingConfiguration.QuarantineDeletedItems;
        }

        SelectMultiThreadMode(existingConfiguration?.MultiThreadMode ?? ContinuousSyncMultiThreadMode.Recommended, _jobOptions.MultiThreadCount);
        RefreshMultiThreadChoices();
        UpdateExclusionSummary();
        Closed += (_, _) => _benchmarkCancellation?.Cancel();
    }

    public ContinuousSyncConfiguration? Configuration { get; private set; }

    private void Exclusions_Click(object sender, RoutedEventArgs e)
    {
        var window = new ExclusionWindow(SourceTextBox.Text, _excludedDirectories, _excludedFiles) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _excludedDirectories = window.ExcludedDirectories;
            _excludedFiles = window.ExcludedFiles;
            UpdateExclusionSummary();
        }
    }

    private void StartAgent_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalTextBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var minutes)
            || minutes is < 1 or > 1440)
        {
            ShowWarning("MonitorIntervalInvalid");
            return;
        }

        if (!TryGetMultiThreadSelection(out var mtMode, out var manualThreads))
        {
            ShowWarning("InvalidThreadCount");
            return;
        }

        var threads = ResolveMultiThreadCount(mtMode, manualThreads);
        if (threads is < 1 or > 128)
        {
            ShowWarning("InvalidThreadCount");
            return;
        }

        if (!TryParseMinute(ScheduleStartTextBox.Text, out var startMinute)
            || !TryParseMinute(ScheduleEndTextBox.Text, out var endMinute))
        {
            ShowWarning("InvalidSchedule");
            return;
        }

        var policy = AllowDestinationOnlyRadioButton.IsChecked == true
            ? DestinationOnlyPolicy.Allow
            : AskDestinationOnlyRadioButton.IsChecked == true
                ? DestinationOnlyPolicy.Ask
                : DestinationOnlyPolicy.Delete;
        if (policy == DestinationOnlyPolicy.Delete
            && QuarantineCheckBox.IsChecked != true
            && MessageBox.Show(
                this,
                FindResource("PermanentDeleteConfirm") as string ?? string.Empty,
                FindResource("ContinuousSyncSettingsTitle") as string ?? string.Empty,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        Configuration = new ContinuousSyncConfiguration
        {
            Id = _configurationId,
            JobOptions = _jobOptions with
            {
                MultiThreadCount = threads,
                RetryCount = 0,
                RetryWaitSeconds = 0,
                Restartable = true,
                IncludeEmptyDirectories = IncludeEmptyCheckBox.IsChecked == true,
                ExcludeZeroByteFiles = ExcludeZeroByteCheckBox.IsChecked == true,
                SymbolicLinkPolicy = GetSymbolicLinkPolicy(),
                ExcludedDirectories = _excludedDirectories,
                ExcludedFiles = _excludedFiles,
            },
            MonitorIntervalMinutes = minutes,
            MultiThreadMode = mtMode,
            DestinationOnlyPolicy = policy,
            LimitToSchedule = ScheduledRadioButton.IsChecked == true,
            ScheduleStartMinute = startMinute,
            ScheduleEndMinute = endMinute,
            PauseOnBattery = PauseOnBatteryCheckBox.IsChecked == true,
            PauseOnMeteredNetwork = PauseOnMeteredCheckBox.IsChecked == true,
            QuarantineDeletedItems = QuarantineCheckBox.IsChecked == true,
            RetainedDestinationOnlyPaths = _retainedDestinationOnlyPaths,
        };
        DialogResult = true;
    }

    private void UpdateExclusionSummary() => ExclusionSummaryTextBlock.Text = string.Format(
        CultureInfo.CurrentCulture,
        FindResource("ExclusionSummaryFormat") as string ?? string.Empty,
        _excludedDirectories.Count,
        _excludedFiles.Count);

    private void SelectSymbolicLinkPolicy(SymbolicLinkPolicy policy)
    {
        SymbolicLinkComboBox.SelectedItem = SymbolicLinkComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, policy.ToString(), StringComparison.Ordinal));
    }

    private SymbolicLinkPolicy GetSymbolicLinkPolicy() =>
        Enum.TryParse<SymbolicLinkPolicy>((SymbolicLinkComboBox.SelectedItem as ComboBoxItem)?.Tag as string, out var policy)
            ? policy
            : SymbolicLinkPolicy.Exclude;

    private void SelectMultiThreadMode(ContinuousSyncMultiThreadMode mode, int manualThreads)
    {
        if (mode == ContinuousSyncMultiThreadMode.Manual)
        {
            MultiThreadComboBox.SelectedItem = null;
            MultiThreadComboBox.Text = manualThreads.ToString(CultureInfo.InvariantCulture);
            return;
        }

        MultiThreadComboBox.SelectedItem = MultiThreadComboBox.Items.OfType<ComboBoxItem>()
            .First(item => string.Equals(item.Tag as string, mode.ToString(), StringComparison.Ordinal));
    }

    private bool TryGetMultiThreadSelection(out ContinuousSyncMultiThreadMode mode, out int threads)
    {
        var text = MultiThreadComboBox.Text.Trim();
        var recommendedText = (MultiThreadComboBox.Items[0] as ComboBoxItem)?.Content?.ToString();
        var maximumText = (MultiThreadComboBox.Items[1] as ComboBoxItem)?.Content?.ToString();
        if (string.Equals(text, recommendedText, StringComparison.CurrentCulture))
        {
            mode = ContinuousSyncMultiThreadMode.Recommended; threads = 8; return true;
        }
        if (string.Equals(text, maximumText, StringComparison.CurrentCulture))
        {
            mode = ContinuousSyncMultiThreadMode.MaximumPerformance; threads = 8; return true;
        }
        mode = ContinuousSyncMultiThreadMode.Manual;
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out threads) && threads is >= 1 and <= 128;
    }

    private int ResolveMultiThreadCount(ContinuousSyncMultiThreadMode mode, int manualThreads) => mode switch
    {
        ContinuousSyncMultiThreadMode.Recommended => _mtRecommendationService
            .RecommendResourceConscious(SourceTextBox.Text, DestinationTextBox.Text).ThreadCount,
        ContinuousSyncMultiThreadMode.MaximumPerformance => _mtRecommendationService
            .RecommendHighestPerformance(SourceTextBox.Text, DestinationTextBox.Text).ThreadCount,
        _ => manualThreads,
    };

    private void RefreshMultiThreadChoices()
    {
        var recommended = _mtRecommendationService.RecommendResourceConscious(SourceTextBox.Text, DestinationTextBox.Text).ThreadCount;
        var maximum = _mtRecommendationService.RecommendHighestPerformance(SourceTextBox.Text, DestinationTextBox.Text).ThreadCount;
        foreach (var item in MultiThreadComboBox.Items.OfType<ComboBoxItem>())
        {
            item.Content = (item.Tag as string) switch
            {
                nameof(ContinuousSyncMultiThreadMode.Recommended) => string.Format(
                    CultureInfo.CurrentCulture, FindResource("MtRecommendedFormat") as string ?? string.Empty, recommended),
                nameof(ContinuousSyncMultiThreadMode.MaximumPerformance) => string.Format(
                    CultureInfo.CurrentCulture, FindResource("MtMaximumPerformanceFormat") as string ?? string.Empty, maximum),
                _ => item.Tag,
            };
        }
    }

    private void OpenMtBenchmarkHistory_Click(object sender, RoutedEventArgs e)
    {
        var window = new MtBenchmarkHistoryWindow(_mtRecommendationService, SourceTextBox.Text, DestinationTextBox.Text) { Owner = this };
        window.HistoryChanged += (_, _) => RefreshMultiThreadChoices();
        window.ShowDialog();
    }

    private async void ReevaluateMt_Click(object sender, RoutedEventArgs e)
    {
        if (_benchmarkCancellation is not null)
        {
            return;
        }

        if (MessageBox.Show(this, FindResource("AgentMtBenchmarkConfirm") as string ?? string.Empty,
                FindResource("ReevaluateAutoMt") as string ?? string.Empty,
                MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
        {
            return;
        }

        _benchmarkCancellation = new CancellationTokenSource();
        ReevaluateMtButton.IsEnabled = false;
        MtBenchmarkProgressBar.Visibility = Visibility.Visible;
        MtBenchmarkProgressBar.Value = 0;
        MtBenchmarkStatusTextBlock.Text = FindResource("MtBenchmarkPreparing") as string ?? string.Empty;
        try
        {
            var progress = new Progress<MtBenchmarkProgress>(update =>
            {
                MtBenchmarkProgressBar.Value = update.CandidateIndex * 100d / update.CandidateCount;
                MtBenchmarkStatusTextBlock.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    FindResource("AgentMtBenchmarkProgressFormat") as string ?? string.Empty,
                    update.ThreadCount,
                    update.CandidateIndex,
                    update.CandidateCount);
            });
            var result = await _mtBenchmarkService.RunAsync(
                SourceTextBox.Text,
                DestinationTextBox.Text,
                progress,
                _benchmarkCancellation.Token);
            _mtRecommendationService.RecordBenchmarkMeasurements(SourceTextBox.Text, DestinationTextBox.Text, result.Measurements);
            RefreshMultiThreadChoices();
            MtBenchmarkProgressBar.Value = 100;
            MtBenchmarkStatusTextBlock.Text = string.Format(
                CultureInfo.CurrentCulture,
                FindResource("AgentMtBenchmarkCompletedFormat") as string ?? string.Empty,
                _mtRecommendationService.RecommendResourceConscious(SourceTextBox.Text, DestinationTextBox.Text).ThreadCount,
                _mtRecommendationService.RecommendHighestPerformance(SourceTextBox.Text, DestinationTextBox.Text).ThreadCount);
            ShowBenchmarkHistoryHint();
        }
        catch (OperationCanceledException)
        {
            MtBenchmarkStatusTextBlock.Text = FindResource("MtBenchmarkCancelled") as string ?? string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MtBenchmarkStatusTextBlock.Text = string.Format(
                CultureInfo.CurrentCulture,
                FindResource("MtBenchmarkFailedFormat") as string ?? string.Empty,
                exception.Message);
        }
        finally
        {
            _benchmarkCancellation.Dispose();
            _benchmarkCancellation = null;
            ReevaluateMtButton.IsEnabled = true;
        }
    }

    private async void ShowBenchmarkHistoryHint()
    {
        var hint = new ToolTip
        {
            Content = FindResource("MtBenchmarkHistoryRightClickHint") as string ?? string.Empty,
            PlacementTarget = ReevaluateMtButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            IsOpen = true,
        };
        ReevaluateMtButton.ToolTip = hint;
        await Task.Delay(TimeSpan.FromSeconds(5));
        hint.IsOpen = false;
    }

    private static bool TryParseMinute(string text, out int minute)
    {
        if (TimeOnly.TryParseExact(text.Trim(), "HH:mm", CultureInfo.CurrentCulture, DateTimeStyles.None, out var time))
        {
            minute = (time.Hour * 60) + time.Minute;
            return true;
        }

        minute = 0;
        return false;
    }

    private static string FormatMinute(int minute) => $"{minute / 60:00}:{minute % 60:00}";

    private void ShowWarning(string key) => MessageBox.Show(
        this,
        FindResource(key) as string ?? string.Empty,
        FindResource("ErrorsTitle") as string ?? string.Empty,
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
}
