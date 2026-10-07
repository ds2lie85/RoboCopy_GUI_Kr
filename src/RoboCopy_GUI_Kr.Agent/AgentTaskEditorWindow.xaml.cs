using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RoboCopy_GUI_Kr.Core.ContinuousSync;
using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Performance;
using RoboCopy_GUI_Kr.Core.Validation;

namespace RoboCopy_GUI_Kr.Agent;

public partial class AgentTaskEditorWindow : Window
{
    private readonly ContinuousSyncConfiguration? _existing;
    private readonly AgentTaskEditMode _mode;
    private readonly MtRecommendationService _mtRecommendationService = new();
    private readonly MtBenchmarkService _mtBenchmarkService = new();
    private IReadOnlyList<string> _excludedDirectories = [];
    private IReadOnlyList<string> _excludedFiles = [];
    private CancellationTokenSource? _benchmarkCancellation;

    public AgentTaskEditorWindow(ContinuousSyncConfiguration? existing, AgentTaskEditMode mode)
    {
        InitializeComponent();
        _existing = existing;
        _mode = mode;
        HeadingTextBlock.Text = FindResource(mode == AgentTaskEditMode.Add
            ? "AgentTaskAddHeading"
            : "AgentTaskEditHeading") as string ?? string.Empty;

        if (existing is not null)
        {
            var job = existing.JobOptions;
            SourceTextBox.Text = job.SourceDirectory;
            DestinationTextBox.Text = job.DestinationDirectory;
            IntervalTextBox.Text = existing.MonitorIntervalMinutes.ToString(CultureInfo.InvariantCulture);
            IncludeEmptyCheckBox.IsChecked = job.IncludeEmptyDirectories;
            ExcludeZeroByteCheckBox.IsChecked = job.ExcludeZeroByteFiles;
            _excludedDirectories = job.ExcludedDirectories;
            _excludedFiles = job.ExcludedFiles;
            SelectSymbolicLinkPolicy(job.SymbolicLinkPolicy);
            DeleteRadioButton.IsChecked = existing.DestinationOnlyPolicy == DestinationOnlyPolicy.Delete;
            AllowRadioButton.IsChecked = existing.DestinationOnlyPolicy == DestinationOnlyPolicy.Allow;
            AskRadioButton.IsChecked = existing.DestinationOnlyPolicy == DestinationOnlyPolicy.Ask;
            AlwaysScheduleRadioButton.IsChecked = !existing.LimitToSchedule;
            ScheduledRadioButton.IsChecked = existing.LimitToSchedule;
            ScheduleStartTextBox.Text = FormatMinute(existing.ScheduleStartMinute);
            ScheduleEndTextBox.Text = FormatMinute(existing.ScheduleEndMinute == 1440 ? 0 : existing.ScheduleEndMinute);
            PauseOnBatteryCheckBox.IsChecked = existing.PauseOnBattery;
            PauseOnMeteredCheckBox.IsChecked = existing.PauseOnMeteredNetwork;
            QuarantineCheckBox.IsChecked = existing.QuarantineDeletedItems;
        }

        SelectMultiThreadMode(existing?.MultiThreadMode ?? ContinuousSyncMultiThreadMode.Recommended, existing?.JobOptions.MultiThreadCount ?? 8);
        RefreshMultiThreadChoices();
        UpdateExclusionSummary();
        Closed += (_, _) => _benchmarkCancellation?.Cancel();
    }

    public ContinuousSyncConfiguration? Configuration { get; private set; }

    private void BrowseSource_Click(object sender, RoutedEventArgs e) => BrowseInto(SourceTextBox);
    private void BrowseDestination_Click(object sender, RoutedEventArgs e) => BrowseInto(DestinationTextBox);

    private void BrowseInto(System.Windows.Controls.TextBox textBox)
    {
        var dialog = new OpenFolderDialog { Multiselect = false, InitialDirectory = Directory.Exists(textBox.Text) ? textBox.Text : null };
        if (dialog.ShowDialog(this) == true)
        {
            textBox.Text = dialog.FolderName;
            RefreshMultiThreadChoices();
        }
    }

    private void Exclusions_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(SourceTextBox.Text))
        {
            ShowWarning("SelectSourceBeforeExclusions");
            return;
        }
        var window = new AgentExclusionWindow(SourceTextBox.Text, _excludedDirectories, _excludedFiles) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _excludedDirectories = window.ExcludedDirectories;
            _excludedFiles = window.ExcludedFiles;
            UpdateExclusionSummary();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (PathSafetyValidator.ValidateCopyDirectories(SourceTextBox.Text, DestinationTextBox.Text).Count != 0)
        {
            ShowWarning("InvalidPaths");
            return;
        }

        if (!int.TryParse(IntervalTextBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var interval) || interval is < 1 or > 1440)
        {
            ShowWarning("InvalidInterval");
            return;
        }

        if (!TryGetMultiThreadSelection(out var mtMode, out var manualThreads))
        {
            ShowWarning("InvalidThreadCount");
            return;
        }

        var threads = ResolveMultiThreadCount(mtMode, manualThreads);

        if (!TryParseMinute(ScheduleStartTextBox.Text, out var startMinute) || !TryParseMinute(ScheduleEndTextBox.Text, out var endMinute))
        {
            ShowWarning("InvalidSchedule");
            return;
        }

        var policy = AllowRadioButton.IsChecked == true ? DestinationOnlyPolicy.Allow
            : AskRadioButton.IsChecked == true ? DestinationOnlyPolicy.Ask : DestinationOnlyPolicy.Delete;
        if (policy == DestinationOnlyPolicy.Delete
            && (_existing?.DestinationOnlyPolicy != DestinationOnlyPolicy.Delete || _mode == AgentTaskEditMode.Add)
            && System.Windows.MessageBox.Show(this, FindResource("DeletePolicyConfirm") as string ?? string.Empty,
                FindResource("AgentTaskTitle") as string ?? string.Empty, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (policy == DestinationOnlyPolicy.Delete
            && QuarantineCheckBox.IsChecked != true
            && System.Windows.MessageBox.Show(this, FindResource("PermanentDeleteConfirm") as string ?? string.Empty,
                FindResource("AgentTaskTitle") as string ?? string.Empty, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var job = (_existing?.JobOptions ?? new RobocopyJobOptions
        {
            SourceDirectory = SourceTextBox.Text,
            DestinationDirectory = DestinationTextBox.Text,
        }) with
        {
            OperationMode = JobOperationMode.UpdateSync,
            SourceDirectory = SourceTextBox.Text,
            DestinationDirectory = DestinationTextBox.Text,
            MultiThreadCount = threads,
            RetryCount = 0,
            RetryWaitSeconds = 0,
            Restartable = true,
            IncludeEmptyDirectories = IncludeEmptyCheckBox.IsChecked == true,
            ExcludeZeroByteFiles = ExcludeZeroByteCheckBox.IsChecked == true,
            SymbolicLinkPolicy = GetSymbolicLinkPolicy(),
            ExcludedDirectories = _excludedDirectories,
            ExcludedFiles = _excludedFiles,
            DryRun = false,
            LogFilePath = null,
        };
        Configuration = new ContinuousSyncConfiguration
        {
            Id = _existing?.Id ?? Guid.NewGuid(),
            JobOptions = job,
            MonitorIntervalMinutes = interval,
            MultiThreadMode = mtMode,
            DestinationOnlyPolicy = policy,
            LimitToSchedule = ScheduledRadioButton.IsChecked == true,
            ScheduleStartMinute = startMinute,
            ScheduleEndMinute = endMinute,
            PauseOnBattery = PauseOnBatteryCheckBox.IsChecked == true,
            PauseOnMeteredNetwork = PauseOnMeteredCheckBox.IsChecked == true,
            QuarantineDeletedItems = QuarantineCheckBox.IsChecked == true,
            RetainedDestinationOnlyPaths = _existing is not null
                && string.Equals(_existing.JobOptions.SourceDirectory, SourceTextBox.Text, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_existing.JobOptions.DestinationDirectory, DestinationTextBox.Text, StringComparison.OrdinalIgnoreCase)
                    ? _existing.RetainedDestinationOnlyPaths
                    : [],
        };
        DialogResult = true;
    }

    private void UpdateExclusionSummary() => ExclusionSummaryTextBlock.Text = string.Format(
        CultureInfo.CurrentCulture, FindResource("ExclusionSummaryFormat") as string ?? string.Empty,
        _excludedDirectories.Count, _excludedFiles.Count);

    private void SelectSymbolicLinkPolicy(SymbolicLinkPolicy policy) => SymbolicLinkComboBox.SelectedItem =
        SymbolicLinkComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag as string, policy.ToString(), StringComparison.Ordinal));

    private SymbolicLinkPolicy GetSymbolicLinkPolicy() =>
        Enum.TryParse<SymbolicLinkPolicy>((SymbolicLinkComboBox.SelectedItem as ComboBoxItem)?.Tag as string, out var policy)
            ? policy : SymbolicLinkPolicy.Exclude;

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
        if (string.IsNullOrWhiteSpace(SourceTextBox.Text) || string.IsNullOrWhiteSpace(DestinationTextBox.Text))
        {
            SetMultiThreadChoiceLabels(8, 8);
            return;
        }

        SetMultiThreadChoiceLabels(
            _mtRecommendationService.RecommendResourceConscious(SourceTextBox.Text, DestinationTextBox.Text).ThreadCount,
            _mtRecommendationService.RecommendHighestPerformance(SourceTextBox.Text, DestinationTextBox.Text).ThreadCount);
    }

    private void SetMultiThreadChoiceLabels(int recommended, int maximum)
    {
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
        var window = new AgentMtBenchmarkHistoryWindow(_mtRecommendationService, SourceTextBox.Text, DestinationTextBox.Text) { Owner = this };
        window.HistoryChanged += (_, _) => RefreshMultiThreadChoices();
        window.ShowDialog();
    }

    private async void ReevaluateMt_Click(object sender, RoutedEventArgs e)
    {
        if (_benchmarkCancellation is not null)
        {
            return;
        }

        if (PathSafetyValidator.ValidateCopyDirectories(SourceTextBox.Text, DestinationTextBox.Text).Count != 0)
        {
            ShowWarning("InvalidPaths");
            return;
        }

        if (System.Windows.MessageBox.Show(this, FindResource("AgentMtBenchmarkConfirm") as string ?? string.Empty,
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
        var hint = new System.Windows.Controls.ToolTip
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

    private void ShowWarning(string resourceKey) => System.Windows.MessageBox.Show(
        this, FindResource(resourceKey) as string ?? string.Empty, FindResource("AgentTaskTitle") as string ?? string.Empty,
        MessageBoxButton.OK, MessageBoxImage.Warning);
}
