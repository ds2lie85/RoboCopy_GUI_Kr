using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using RoboCopy_GUI_Kr.Core.ContinuousSync;
using RoboCopy_GUI_Kr.Core.Errors;
using RoboCopy_GUI_Kr.Core.Jobs;
using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Moves;
using RoboCopy_GUI_Kr.Core.Performance;
using RoboCopy_GUI_Kr.Core.Paths;
using RoboCopy_GUI_Kr.Core.Progress;
using RoboCopy_GUI_Kr.Core.Profiles;
using RoboCopy_GUI_Kr.Core.Robocopy;
using RoboCopy_GUI_Kr.Core.Shell;
using RoboCopy_GUI_Kr.Core.Sync;
using RoboCopy_GUI_Kr.Core.Validation;

namespace RoboCopy_GUI_Kr;

public partial class MainWindow : Window
{
    private const int MaximumVisibleFileProgressItems = 1000;
    private const int MaximumPendingOutputLines = 4000;
    private const int MaximumPendingFileUpdates = 1000;
    private const int MaximumOutputCharacters = 1_500_000;
    private const int RetainedOutputCharacters = 1_000_000;
    private const int OutputLinesPerUiTick = 250;
    private const int FileUpdatesPerUiTick = 200;

    private readonly RobocopyCommandBuilder _commandBuilder = new();
    private readonly RobocopyRunner _runner = new();
    private readonly JobStateRepository _jobRepository = new();
    private readonly WorkProfileRepository _profileRepository = new();
    private readonly ContinuousSyncConfigurationRepository _continuousSyncRepository = new();
    private readonly ContinuousSyncWorkflow _continuousSyncWorkflow = new();
    private readonly VerifiedMoveService _verifiedMoveService = new();
    private readonly FailedMoveCleanupService _failedMoveCleanupService = new();
    private readonly OneWaySyncPlanner _oneWaySyncPlanner = new();
    private readonly OneWaySyncDeletionService _oneWaySyncDeletionService = new();
    private readonly JobQueue _jobQueue = new();
    private readonly MtRecommendationService _mtRecommendationService = new();
    private readonly MtBenchmarkService _mtBenchmarkService = new();
    private readonly ElevatedWorkerClient _elevatedWorkerClient = new();
    private readonly SystemPerformanceProfileDetector _systemProfileDetector = new();
    private readonly PhysicalStorageProfileDetector _physicalStorageProfileDetector = new();
    private readonly FileWorkloadAnalyzer _fileWorkloadAnalyzer = new();
    private readonly NamedFileCopyService _namedFileCopyService = new();
    private readonly RobocopyUpdateBuffer _uiUpdateBuffer = new(MaximumPendingOutputLines, MaximumPendingFileUpdates);
    private readonly DispatcherTimer _uiFlushTimer;
    private readonly StringBuilder _displayLog = new();
    private readonly List<string> _excludedDirectories = [];
    private readonly List<string> _excludedFiles = [];
    private readonly ObservableCollection<FileCopyProgressItem> _fileProgressItems = [];
    private readonly ObservableCollection<ErrorFileRow> _errorItems = [];
    private readonly ObservableCollection<ProfileSelectionItem> _profileItems = [];
    private readonly Dictionary<Guid, ErrorFileRow> _retryRowsByJobId = [];
    private readonly Dictionary<Guid, IReadOnlyList<OneWaySyncExtraItem>> _syncDeletionSelections = [];
    private readonly Dictionary<Guid, RobocopyJobOptions> _syncReviewedOptions = [];
    private readonly Dictionary<ErrorFileRow, MoveFailureContext> _moveFailureContexts = [];
    private readonly Dictionary<ErrorFileRow, MoveDeletionRetryContext> _moveDeletionRetryOptions = [];
    private readonly Dictionary<string, FileCopyProgressItem> _fileProgressBySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _completedFileProgressSources = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cancellationTokenSource;
    private PersistedJob? _currentJob;
    private JobQueueWindow? _jobQueueWindow;
    private LogWindow? _logWindow;
    private ErrorFilesWindow? _errorFilesWindow;
    private MtRecommendation? _lastMtRecommendation;
    private Guid? _pausedQueueEntryId;
    private PostCopyOperation? _activePostCopyOperation;
    private PostCopyOperation? _pausedPostCopyOperation;
    private bool _isProcessingQueue;
    private bool _isPreviewRunning;
    private bool _pauseRequested;
    private bool _stopRequested;
    private bool _isLoadingProfiles;
    private bool _isOpeningContinuousSyncSettings;
    private readonly IReadOnlyList<ShellCopyRequest> _startupRequests;
    private readonly bool _shellConflictsAlreadyReviewed;
    private readonly bool _preferMiniUi;
    private RobocopySummaryAggregator? _activeQueueSummary;
    private MiniCopyWindow? _miniCopyWindow;
    private RobocopyJobOptions? _miniCurrentOptions;
    private int _miniTotalFiles;
    private int _miniProcessedFilesBeforeCurrent;
    private int _miniCurrentJobTotalFiles;
    private int _miniCurrentCompletedFiles;
    private long _miniTotalWeight;
    private long _miniProcessedWeightBeforeCurrent;
    private long _miniCurrentJobWeight;
    private bool _miniWorkStarted;
    private bool _startupInitialized;
    private string? _miniBatchSourceDirectory;
    private string? _miniBatchDestinationDirectory;

    public MainWindow(
        IReadOnlyList<ShellCopyRequest>? startupRequests = null,
        bool shellConflictsAlreadyReviewed = false,
        bool preferMiniUi = false)
    {
        InitializeComponent();
        _startupRequests = startupRequests ?? [];
        _shellConflictsAlreadyReviewed = shellConflictsAlreadyReviewed;
        _preferMiniUi = preferMiniUi;
        if (_preferMiniUi)
        {
            Opacity = 0;
            ShowActivated = false;
            ShowInTaskbar = false;
        }
        _uiFlushTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _uiFlushTimer.Tick += UiFlushTimer_Tick;
        FileProgressDataGrid.ItemsSource = _fileProgressItems;
        ProfileComboBox.ItemsSource = _profileItems;
        UpdateAutoMtDisplay();
    }

    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        BrowseInto(SourceTextBox, GetText("SourceFolder"), SourceTextBox.Text);
    }

    private void MiniUi_Click(object sender, RoutedEventArgs e) => ShowMiniUi();

    private void ShowMiniUi()
    {
        if (_miniCopyWindow is not null)
        {
            _miniCopyWindow.Activate();
            return;
        }

        _miniCopyWindow = new MiniCopyWindow(this);
        _miniCopyWindow.Show();
        if (IsVisible)
        {
            Hide();
        }
    }

    internal async Task StartWithMiniUiAsync()
    {
        ShowMiniUi();
        await InitializeStartupAsync();
    }

    internal MiniUiState GetMiniUiState()
    {
        var source = _miniBatchSourceDirectory ?? _miniCurrentOptions?.SourceDirectory ?? SourceTextBox.Text;
        var destination = _miniBatchDestinationDirectory ?? _miniCurrentOptions?.DestinationDirectory ?? DestinationTextBox.Text;
        var isConfigured = !string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(destination);
        static string Name(string path) => string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path;
        var completedFiles = Math.Clamp(
            _miniProcessedFilesBeforeCurrent + _miniCurrentCompletedFiles,
            0,
            _miniTotalFiles);
        var completedWeight = _miniProcessedWeightBeforeCurrent
            + (long)Math.Round(_miniCurrentJobWeight * ProgressBar.Value / 100d);
        var percent = _miniTotalWeight > 0
            ? Math.Clamp(completedWeight * 100d / _miniTotalWeight, 0, 100)
            : ProgressBar.Value;
        var operationMode = _miniCurrentOptions?.OperationMode
            ?? _startupRequests.FirstOrDefault()?.OperationMode
            ?? JobOperationMode.Copy;
        var operationName = GetText(operationMode switch
        {
            JobOperationMode.Move => "MiniUiOperationMove",
            JobOperationMode.UpdateSync => "MiniUiOperationSync",
            _ => "MiniUiOperationCopy",
        });
        var canPauseOrResume = _cancellationTokenSource is not null || _pausedQueueEntryId is not null;
        var hasFailedJobs = _jobQueue.Items.Any(item => item.State == JobQueueEntryState.Failed);
        return new MiniUiState(
            Name(source),
            Name(destination),
            operationName,
            isConfigured,
            _miniTotalFiles,
            completedFiles,
            percent,
            ProgressBar.IsIndeterminate,
            _pausedQueueEntryId is not null,
            canPauseOrResume,
            _miniWorkStarted && !canPauseOrResume && !_isProcessingQueue && _jobQueue.WaitingCount == 0 && percent >= 100 && !hasFailedJobs,
            MiniUiCompletionPolicy.RequiresErrorManagement(_miniWorkStarted,
                _isProcessingQueue || _cancellationTokenSource is not null,
                _pausedQueueEntryId is not null, _jobQueue.WaitingCount, hasFailedJobs,
                _errorItems.Any(row => row.RetryState != ErrorFileRetryState.Resolved)));
    }

    internal void TogglePauseFromMiniUi()
    {
        if (_cancellationTokenSource is not null)
        {
            Pause_Click(this, new RoutedEventArgs());
        }
        else if (_pausedQueueEntryId is not null)
        {
            Start_Click(this, new RoutedEventArgs());
        }
    }

    internal void StopFromMiniUi()
    {
        if (_cancellationTokenSource is not null)
        {
            Stop_Click(this, new RoutedEventArgs());
        }
    }

    internal void ShowAdvancedUi()
    {
        Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Opacity = 1;
        ShowActivated = true;
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    internal void NotifyMiniUiClosed() => _miniCopyWindow = null;

    internal void ShowErrorManagementUi()
    {
        ShowAdvancedUi();
        if (_errorFilesWindow is not null)
        {
            _errorFilesWindow.Owner = this;
        }
        ErrorFiles_Click(this, new RoutedEventArgs());
    }

    private Window? GetDialogOwner() => WindowOwnerSelector.Select(this, _miniCopyWindow);

    internal void CloseFromMiniUi()
    {
        _miniCopyWindow = null;
        Application.Current.Shutdown();
    }

    private void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        BrowseInto(
            DestinationTextBox,
            GetText("DestinationFolder"),
            FolderDialogStartPath.ResolveDestinationStartPath(SourceTextBox.Text));
    }

    private void CopyPath_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateAutoMtDisplay();
        }
    }

    private void OperationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || !IsContinuousSyncSelected() || _isOpeningContinuousSyncSettings)
        {
            return;
        }

        Dispatcher.BeginInvoke(OpenContinuousSyncSettings);
    }

    private void BrowseInto(TextBox target, string title, string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null,
        };

        if (dialog.ShowDialog(this) == true)
        {
            target.Text = dialog.FolderName;
        }
    }

    private void ConfigureExclusions_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(SourceTextBox.Text))
        {
            MessageBox.Show(this, GetText("SelectSourceBeforeExclusions"), GetText("ErrorsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new ExclusionWindow(SourceTextBox.Text, _excludedDirectories, _excludedFiles)
        {
            Owner = GetDialogOwner(),
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _excludedDirectories.Clear();
        _excludedDirectories.AddRange(dialog.ExcludedDirectories);
        _excludedFiles.Clear();
        _excludedFiles.AddRange(dialog.ExcludedFiles);
        UpdateExclusionSummary();
    }

    private void LoadProfiles(Guid? selectedProfileId = null)
    {
        try
        {
            _isLoadingProfiles = true;
            _profileItems.Clear();
            _profileItems.Add(new ProfileSelectionItem(GetText("DefaultProfile"), ProfileSelectionKind.Default));
            foreach (var profile in _profileRepository.LoadAll())
            {
                _profileItems.Add(new ProfileSelectionItem(profile.Name, ProfileSelectionKind.Saved, profile));
            }

            _profileItems.Add(new ProfileSelectionItem(GetText("NewProfile"), ProfileSelectionKind.New));
            ProfileComboBox.SelectedItem = selectedProfileId is Guid id
                ? _profileItems.FirstOrDefault(item => item.Profile?.Id == id) ?? _profileItems[0]
                : _profileItems[0];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("ProfileLoadFailedFormat"), exception.Message),
                GetText("Profile"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _isLoadingProfiles = false;
        }
    }

    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingProfiles || ProfileComboBox.SelectedItem is not ProfileSelectionItem selected)
        {
            return;
        }

        if (selected.Kind == ProfileSelectionKind.Saved && selected.Profile is not null)
        {
            ApplyProfile(selected.Profile);
            SetStatus(string.Format(CultureInfo.CurrentCulture, GetText("ProfileLoadedFormat"), selected.Profile.Name));
            return;
        }

        if (selected.Kind == ProfileSelectionKind.Default)
        {
            ApplyDefaultProfileSettings();
            SetStatus(GetText("DefaultProfileApplied"));
            return;
        }

        if (selected.Kind == ProfileSelectionKind.New)
        {
            _isLoadingProfiles = true;
            ProfileComboBox.SelectedIndex = 0;
            _isLoadingProfiles = false;
            SaveProfileAsNew();
        }
    }

    private void ProfileItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ComboBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    private void ApplyDefaultProfileSettings()
    {
        OperationComboBox.SelectedIndex = 0;
        MultiThreadComboBox.SelectedItem = AutoMtComboBoxItem;
        UpdateAutoMtDisplay();
        RetryCountTextBox.Text = "3";
        RetryWaitTextBox.Text = "5";
        RestartableCheckBox.IsChecked = true;
        IncludeEmptyDirectoriesCheckBox.IsChecked = false;
        ExcludeZeroByteFilesCheckBox.IsChecked = false;
        SaveLogCheckBox.IsChecked = true;
        LinkPolicyComboBox.SelectedIndex = 0;
        _excludedDirectories.Clear();
        _excludedFiles.Clear();
        UpdateExclusionSummary();
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedSavedProfile();
        if (selected is null)
        {
            SaveProfileAsNew();
            return;
        }

        if (MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("ProfileOverwritePromptFormat"), selected.Name),
                GetText("Profile"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        if (TryCreateProfile(selected.Id, selected.Name, out var profile) && SaveProfile(profile))
        {
            LoadProfiles(profile.Id);
        }
    }

    private void SaveProfileAsNew(string initialName = "")
    {
        var dialog = new ProfileNameWindow(initialName) { Owner = GetDialogOwner() };
        if (dialog.ShowDialog() != true
            || !TryCreateProfile(Guid.NewGuid(), dialog.ProfileName, out var profile)
            || !SaveProfile(profile))
        {
            return;
        }

        LoadProfiles(profile.Id);
    }

    private bool SaveProfile(WorkProfile profile)
    {
        try
        {
            _profileRepository.Save(profile);
            SetStatus(string.Format(CultureInfo.CurrentCulture, GetText("ProfileSavedFormat"), profile.Name));
            return true;
        }
        catch (InvalidOperationException)
        {
            MessageBox.Show(this, GetText("ProfileDuplicateName"), GetText("Profile"), MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("ProfileSaveFailedFormat"), exception.Message),
                GetText("Profile"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    private bool TryCreateProfile(Guid id, string name, out WorkProfile profile)
    {
        profile = null!;
        if (!TryReadNumberOptions(out var multiThreadCount, out var retryCount, out var retryWaitSeconds))
        {
            MessageBox.Show(this, GetText("InvalidNumber"), GetText("ErrorsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var automaticMt = ReferenceEquals(MultiThreadComboBox.SelectedItem, AutoMtComboBoxItem)
            || IsAutoMtDisplayText(MultiThreadComboBox.Text);
        var operationMode = (OperationComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Copy";
        profile = new WorkProfile
        {
            Id = id,
            Name = name,
            SourceDirectory = SourceTextBox.Text,
            DestinationDirectory = DestinationTextBox.Text,
            OperationMode = operationMode,
            UseAutomaticMultiThread = automaticMt,
            MultiThreadCount = multiThreadCount,
            RetryCount = retryCount,
            RetryWaitSeconds = retryWaitSeconds,
            Restartable = RestartableCheckBox.IsChecked == true,
            IncludeEmptyDirectories = IncludeEmptyDirectoriesCheckBox.IsChecked == true,
            ExcludeZeroByteFiles = ExcludeZeroByteFilesCheckBox.IsChecked == true,
            SaveLog = SaveLogCheckBox.IsChecked == true,
            SymbolicLinkPolicy = GetLinkPolicy(),
            ExcludedDirectories = _excludedDirectories.ToArray(),
            ExcludedFiles = _excludedFiles.ToArray(),
        };
        return true;
    }

    private void ApplyProfile(WorkProfile profile)
    {
        SourceTextBox.Text = profile.SourceDirectory;
        DestinationTextBox.Text = profile.DestinationDirectory;
        foreach (var item in OperationComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), profile.OperationMode, StringComparison.Ordinal))
            {
                OperationComboBox.SelectedItem = item;
                break;
            }
        }

        if (profile.UseAutomaticMultiThread)
        {
            MultiThreadComboBox.SelectedItem = AutoMtComboBoxItem;
            UpdateAutoMtDisplay();
        }
        else
        {
            MultiThreadComboBox.SelectedItem = null;
            MultiThreadComboBox.Text = profile.MultiThreadCount.ToString(CultureInfo.InvariantCulture);
        }

        RetryCountTextBox.Text = profile.RetryCount.ToString(CultureInfo.InvariantCulture);
        RetryWaitTextBox.Text = profile.RetryWaitSeconds.ToString(CultureInfo.InvariantCulture);
        RestartableCheckBox.IsChecked = profile.Restartable;
        IncludeEmptyDirectoriesCheckBox.IsChecked = profile.IncludeEmptyDirectories;
        ExcludeZeroByteFilesCheckBox.IsChecked = profile.ExcludeZeroByteFiles;
        SaveLogCheckBox.IsChecked = profile.SaveLog;
        foreach (var item in LinkPolicyComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), profile.SymbolicLinkPolicy.ToString(), StringComparison.Ordinal))
            {
                LinkPolicyComboBox.SelectedItem = item;
                break;
            }
        }

        _excludedDirectories.Clear();
        _excludedDirectories.AddRange(profile.ExcludedDirectories);
        _excludedFiles.Clear();
        _excludedFiles.AddRange(profile.ExcludedFiles);
        UpdateExclusionSummary();
    }

    private WorkProfile? GetSelectedSavedProfile() =>
        (ProfileComboBox.SelectedItem as ProfileSelectionItem)?.Profile;

    private void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedSavedProfile();
        if (selected is null)
        {
            ShowSelectSavedProfile();
            return;
        }

        var dialog = new ProfileNameWindow(selected.Name) { Owner = GetDialogOwner() };
        if (dialog.ShowDialog() == true && SaveProfile(selected with { Name = dialog.ProfileName }))
        {
            LoadProfiles(selected.Id);
        }
    }

    private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedSavedProfile();
        if (selected is null)
        {
            ShowSelectSavedProfile();
            return;
        }

        var suggestedName = string.Format(CultureInfo.CurrentCulture, GetText("DuplicateProfileNameFormat"), selected.Name);
        var dialog = new ProfileNameWindow(suggestedName) { Owner = GetDialogOwner() };
        if (dialog.ShowDialog() == true)
        {
            var duplicate = selected with { Id = Guid.NewGuid(), Name = dialog.ProfileName };
            if (SaveProfile(duplicate))
            {
                LoadProfiles(duplicate.Id);
            }
        }
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedSavedProfile();
        if (selected is null)
        {
            ShowSelectSavedProfile();
            return;
        }

        if (MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("DeleteProfilePromptFormat"), selected.Name),
                GetText("DeleteProfile"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _profileRepository.Delete(selected.Id);
            LoadProfiles();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("ProfileSaveFailedFormat"), exception.Message),
                GetText("Profile"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ShowSelectSavedProfile() => MessageBox.Show(
        this,
        GetText("SelectSavedProfile"),
        GetText("Profile"),
        MessageBoxButton.OK,
        MessageBoxImage.Information);

    private void JobList_Click(object sender, RoutedEventArgs e)
    {
        if (_jobQueueWindow is not null)
        {
            _jobQueueWindow.Activate();
            return;
        }

        _jobQueueWindow = new JobQueueWindow(_jobQueue, CreateQueueTemplate)
        {
            Owner = GetDialogOwner(),
        };
        _jobQueueWindow.RunnableJobAdded += QueueWindow_RunnableJobAdded;
        _jobQueueWindow.Closed += (_, _) => _jobQueueWindow = null;
        _jobQueueWindow.Show();
    }

    private void ExplorerIntegration_Click(object sender, RoutedEventArgs e)
    {
        new ExplorerIntegrationWindow { Owner = GetDialogOwner() }.ShowDialog();
    }

    private async void QueueWindow_RunnableJobAdded(object? sender, EventArgs e)
    {
        await ProcessQueueAsync();
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (IsContinuousSyncSelected())
        {
            OpenContinuousSyncSettings();
            return;
        }

        if (_isProcessingQueue || _isPreviewRunning || !TryCreateJobOptions(dryRun: true, out var options))
        {
            return;
        }

        _isPreviewRunning = true;
        SetRunningState(isRunning: true, dryRun: true);
        ClearOutputLog();

        try
        {
            await ExecuteJobAsync(options, dryRun: true, queueEntryId: null);
        }
        finally
        {
            _isPreviewRunning = false;
            SetRunningState(isRunning: false, dryRun: true);
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (IsContinuousSyncSelected())
        {
            OpenContinuousSyncSettings();
            return;
        }

        if (_isPreviewRunning)
        {
            return;
        }

        if (!_isProcessingQueue && _pausedQueueEntryId is Guid pausedId)
        {
            if (_pausedPostCopyOperation is not null)
            {
                await ResumePostCopyOperationAsync(pausedId);
                return;
            }

            _jobQueue.SetState(pausedId, JobQueueEntryState.Waiting);
            _pausedQueueEntryId = null;
            await ProcessQueueAsync();
            return;
        }

        if (!_isProcessingQueue && _jobQueue.WaitingCount > 0)
        {
            await ProcessQueueAsync();
            return;
        }

        if (!TryCreateJobOptions(dryRun: false, out var options))
        {
            return;
        }

        _jobQueue.Enqueue(options);
        await ProcessQueueAsync();
    }

    private void AddJob_Click(object sender, RoutedEventArgs e)
    {
        if (IsContinuousSyncSelected())
        {
            OpenContinuousSyncSettings();
            return;
        }

        if (_isPreviewRunning || !TryCreateJobOptions(dryRun: false, out var options))
        {
            return;
        }

        _jobQueue.Enqueue(options);
        SetStatus(string.Format(CultureInfo.CurrentCulture, GetText("QueuedFormat"), _jobQueue.WaitingCount));
        SetRunningState(_isProcessingQueue, dryRun: false);
    }

    private bool IsContinuousSyncSelected() => string.Equals(
        (OperationComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
        JobOperationMode.ContinuousSync.ToString(),
        StringComparison.Ordinal);

    private void OpenContinuousSyncSettings()
    {
        if (_isOpeningContinuousSyncSettings)
        {
            return;
        }

        _isOpeningContinuousSyncSettings = true;
        try
        {
            if (!TryCreateJobOptions(dryRun: false, out var options))
            {
                return;
            }

            ContinuousSyncConfiguration? existing = null;
            try
            {
                existing = _continuousSyncRepository.LoadAll().FirstOrDefault(item =>
                    string.Equals(item.JobOptions.SourceDirectory, options.SourceDirectory, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.JobOptions.DestinationDirectory, options.DestinationDirectory, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            {
                MessageBox.Show(
                    this,
                    string.Format(CultureInfo.CurrentCulture, GetText("ContinuousSyncAgentStartFailedFormat"), exception.Message),
                    GetText("ContinuousSyncSettingsTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            ContinuousSyncConfiguration configuration;
            while (true)
            {
                var window = new ContinuousSyncSettingsWindow(options, existing) { Owner = GetDialogOwner() };
                if (window.ShowDialog() != true || window.Configuration is not { } edited)
                {
                    return;
                }

                if (edited.DestinationOnlyPolicy == DestinationOnlyPolicy.Delete
                    && MessageBox.Show(
                        this,
                        GetText("ContinuousSyncDeleteConfirm"),
                        GetText("ContinuousSyncSettingsTitle"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    existing = edited;
                    continue;
                }

                configuration = edited;
                break;
            }

            if (!_continuousSyncWorkflow.SaveOrUseExisting(this, configuration))
            {
                return;
            }

            SetStatus(GetText("ContinuousSyncAgentStarted"));
        }
        catch (ContinuousSyncTaskConflictException)
        {
            MessageBox.Show(
                this,
                GetText("ContinuousSyncPathConflict"),
                GetText("ContinuousSyncSettingsTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception exception) when (exception is IOException
                                               or UnauthorizedAccessException
                                               or InvalidOperationException
                                               or ArgumentException
                                               or NotSupportedException)
        {
            MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("ContinuousSyncAgentStartFailedFormat"), exception.Message),
                GetText("ContinuousSyncSettingsTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isOpeningContinuousSyncSettings = false;
        }
    }

    private void Log_Click(object sender, RoutedEventArgs e)
    {
        if (_logWindow is not null)
        {
            _logWindow.Activate();
            return;
        }

        _logWindow = new LogWindow(_displayLog.ToString()) { Owner = GetDialogOwner() };
        _logWindow.Closed += (_, _) => _logWindow = null;
        _logWindow.Show();
    }

    private void ErrorFiles_Click(object sender, RoutedEventArgs e)
    {
        if (_errorFilesWindow is not null)
        {
            _errorFilesWindow.Activate();
            return;
        }

        _errorFilesWindow = new ErrorFilesWindow(_errorItems) { Owner = GetDialogOwner() };
        _errorFilesWindow.RetryRequested += ErrorFilesWindow_RetryRequested;
        _errorFilesWindow.ElevatedRetryRequested += ErrorFilesWindow_ElevatedRetryRequested;
        _errorFilesWindow.Closed += (_, _) => _errorFilesWindow = null;
        _errorFilesWindow.Show();
    }

    private async void ErrorFilesWindow_RetryRequested(object? sender, RetryErrorsRequestedEventArgs e)
    {
        var template = CreateQueueTemplate();
        var queued = 0;
        foreach (var row in e.Rows)
        {
            if (_moveDeletionRetryOptions.TryGetValue(row, out var deletionContext))
            {
                await RetryMoveDeletionAsync(row, deletionContext);
                continue;
            }

            var error = row.Item;
            var sourceIsDirectory = Directory.Exists(error.SourcePath);
            var sourceDirectory = sourceIsDirectory ? error.SourcePath : Path.GetDirectoryName(error.SourcePath);
            var destinationDirectory = sourceIsDirectory ? error.DestinationPath : Path.GetDirectoryName(error.DestinationPath);
            var fileName = sourceIsDirectory ? string.Empty : Path.GetFileName(error.SourcePath);
            if (string.IsNullOrWhiteSpace(sourceDirectory)
                || string.IsNullOrWhiteSpace(destinationDirectory)
                || (!sourceIsDirectory && (string.IsNullOrWhiteSpace(fileName) || !File.Exists(error.SourcePath))))
            {
                continue;
            }

            var retryOperation = _moveFailureContexts.TryGetValue(row, out var moveContext)
                && moveContext.Policy == MoveFailurePolicy.MoveVerifiedItemsFirst
                    ? JobOperationMode.Move
                    : JobOperationMode.Copy;

            var entry = _jobQueue.Enqueue(template with
            {
                OperationMode = retryOperation,
                SourceDirectory = sourceDirectory,
                DestinationDirectory = destinationDirectory,
                RecurseSubdirectories = sourceIsDirectory,
                FileSpecifications = sourceIsDirectory ? [] : [fileName],
                IncludeEmptyDirectories = sourceIsDirectory,
                ExcludedDirectories = [],
                ExcludedFiles = [],
                LogFilePath = null,
            });
            _retryRowsByJobId[entry.Id] = row;
            UpdateRetryState(row, ErrorFileRetryState.Queued);
            queued++;
        }

        if (queued > 0 && !_isProcessingQueue && !_isPreviewRunning)
        {
            await ProcessQueueAsync();
        }
    }

    private async void ErrorFilesWindow_ElevatedRetryRequested(object? sender, RetryErrorsRequestedEventArgs e)
    {
        var template = CreateQueueTemplate();
        var requestRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboCopy_GUI_Kr",
            "elevated-requests",
            Guid.NewGuid().ToString("N"));
        var request = new ElevatedRetryRequest
        {
            Files = e.Rows.Select(row =>
            {
                var isDeletionRetry = _moveDeletionRetryOptions.TryGetValue(row, out var deletionContext);
                return new ElevatedRetryFile(row.SourcePath, row.DestinationPath)
                {
                    MoveAfterCopy = isDeletionRetry
                        || (_moveFailureContexts.TryGetValue(row, out var context)
                            && context.Policy == MoveFailurePolicy.MoveVerifiedItemsFirst),
                    MoveSourceRoot = isDeletionRetry ? deletionContext!.SourceRoot : null,
                    MoveDestinationRoot = isDeletionRetry ? deletionContext!.DestinationRoot : null,
                };
            }).ToArray(),
            ResultPath = Path.Combine(requestRoot, "result.json"),
            RetryCount = template.RetryCount,
            RetryWaitSeconds = template.RetryWaitSeconds,
            Restartable = template.Restartable,
        };

        foreach (var row in e.Rows)
        {
            UpdateRetryState(row, ErrorFileRetryState.Queued);
        }

        try
        {
            foreach (var row in e.Rows)
            {
                UpdateRetryState(row, ErrorFileRetryState.Running);
            }

            var clientResult = await _elevatedWorkerClient.RunAsync(request);
            if (clientResult.WasCanceled)
            {
                foreach (var row in e.Rows)
                {
                    UpdateRetryState(row, ErrorFileRetryState.Failed);
                }

                MessageBox.Show(this, GetText("ElevatedRetryCanceled"), GetText("ErrorFilesTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var row in e.Rows)
            {
                var isMoveRetry = _moveDeletionRetryOptions.ContainsKey(row)
                    || _moveFailureContexts.ContainsKey(row);
                var fileResult = clientResult.Result?.Files.FirstOrDefault(result =>
                    string.Equals(result.SourcePath, row.SourcePath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(result.DestinationPath, row.DestinationPath, StringComparison.OrdinalIgnoreCase));
                UpdateRetryState(
                    row,
                    fileResult?.IsSuccess == true ? ErrorFileRetryState.Resolved : ErrorFileRetryState.RetryFailed,
                    isMoveRetry ? "ErrorStatusMoveCompleted" : "ErrorStatusCompleted");
                if (fileResult?.IsSuccess == true)
                {
                    _moveDeletionRetryOptions.Remove(row);
                    await CompleteMoveFailureRetryAsync(row);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or Win32Exception)
        {
            foreach (var row in e.Rows)
            {
                UpdateRetryState(row, ErrorFileRetryState.RetryFailed);
            }

            MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("ElevatedRetryFailedFormat"), exception.Message),
                GetText("ErrorFilesTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        _pauseRequested = true;
        _stopRequested = false;
        SetStatus(GetText("Pausing"));
        PauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        _cancellationTokenSource.Cancel();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        _stopRequested = true;
        _pauseRequested = false;
        SetStatus(GetText("Stopping"));
        PauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        _cancellationTokenSource.Cancel();
    }

    private async Task ProcessQueueAsync()
    {
        if (_isProcessingQueue || _isPreviewRunning)
        {
            return;
        }

        if (_pausedQueueEntryId is Guid pausedId
            && _jobQueue.Items.Any(item => item.Id == pausedId && item.State == JobQueueEntryState.Paused))
        {
            SetStatus(string.Format(CultureInfo.CurrentCulture, GetText("QueuedFormat"), _jobQueue.WaitingCount));
            return;
        }

        _pausedQueueEntryId = null;
        _isProcessingQueue = true;
        _miniWorkStarted = true;
        var queueSummary = new RobocopySummaryAggregator();
        _activeQueueSummary = queueSummary;
        SetRunningState(isRunning: true, dryRun: false);
        ClearOutputLog();

        try
        {
            while (_jobQueue.TryStartNext(out var entry) && entry is not null)
            {
                if (_retryRowsByJobId.TryGetValue(entry.Id, out var retryRow))
                {
                    UpdateRetryState(retryRow, ErrorFileRetryState.Running);
                }

                var outcome = await ExecuteJobAsync(entry.Options, dryRun: false, entry.Id);
                _jobQueue.SetState(entry.Id, outcome);
                if (outcome != JobQueueEntryState.Paused)
                {
                    _syncDeletionSelections.Remove(entry.Id);
                    _syncReviewedOptions.Remove(entry.Id);
                }
                if (outcome == JobQueueEntryState.Completed)
                {
                    _miniProcessedFilesBeforeCurrent = Math.Min(
                        _miniTotalFiles,
                        _miniProcessedFilesBeforeCurrent + _miniCurrentJobTotalFiles);
                    _miniProcessedWeightBeforeCurrent = Math.Min(
                        _miniTotalWeight,
                        _miniProcessedWeightBeforeCurrent + _miniCurrentJobWeight);
                    _miniCurrentJobTotalFiles = 0;
                    _miniCurrentCompletedFiles = 0;
                    _miniCurrentJobWeight = 0;
                }

                if (retryRow is not null)
                {
                    var retryState = ErrorFileRetryStateResolver.FromQueueOutcome(outcome);
                    UpdateRetryState(retryRow, retryState);
                    if (retryState == ErrorFileRetryState.Resolved)
                    {
                        await CompleteMoveFailureRetryAsync(retryRow);
                    }

                    if (_pausedQueueEntryId is not null || _stopRequested)
                    {
                        break;
                    }

                    if (outcome != JobQueueEntryState.Paused)
                    {
                        _retryRowsByJobId.Remove(entry.Id);
                    }
                }

                if (outcome == JobQueueEntryState.Paused)
                {
                    _pausedQueueEntryId = entry.Id;
                    break;
                }

                if (outcome == JobQueueEntryState.Stopped)
                {
                    break;
                }
            }
        }
        finally
        {
            _activeQueueSummary = null;
            _isProcessingQueue = false;
            SetRunningState(isRunning: false, dryRun: false);
        }

        var aggregate = queueSummary.Build();
        if (_pausedQueueEntryId is null
            && !_stopRequested
            && _jobQueue.WaitingCount == 0
            && aggregate.OperationCount > 1)
        {
            ShowAggregateCopySummary(aggregate);
        }
        if (_jobQueue.Items.Any(item => item.State == JobQueueEntryState.Failed))
            SetStatus(GetText("MiniUiFailedJobs"));
    }

    private async Task ResumePostCopyOperationAsync(Guid pausedEntryId)
    {
        var operation = _pausedPostCopyOperation;
        if (operation is null || _isProcessingQueue || _isPreviewRunning)
        {
            return;
        }

        _pausedPostCopyOperation = null;
        _activePostCopyOperation = operation;
        _pausedQueueEntryId = null;
        _pauseRequested = false;
        _stopRequested = false;
        _isProcessingQueue = true;
        _cancellationTokenSource = new CancellationTokenSource();
        _jobQueue.SetState(pausedEntryId, JobQueueEntryState.Running);
        SetRunningState(isRunning: true, dryRun: false);

        var outcome = operation.CompletionState;
        try
        {
            switch (operation)
            {
                case MovePostCopyOperation move:
                    var moveResult = await ExecuteMoveVerificationAsync(move.Options, move.CompletionState);
                    if (!moveResult.IsSuccess)
                    {
                        outcome = JobQueueEntryState.Failed;
                    }

                    break;

                case DeletePostCopyOperation delete:
                    _ = await ExecuteDeleteOperationAsync(delete);
                    break;

                case OneWaySyncDeletePostCopyOperation syncDelete:
                    var deletionResult = await ExecuteOneWaySyncDeletionAsync(syncDelete);
                    if (!deletionResult.IsSuccess)
                    {
                        outcome = JobQueueEntryState.Failed;
                    }

                    break;
            }

            _jobQueue.SetState(pausedEntryId, outcome);
            if (outcome == JobQueueEntryState.Completed)
            {
                DeletePersistedJob();
            }
            else
            {
                SaveCurrentJob(JobExecutionState.Failed);
            }
        }
        catch (OperationCanceledException)
        {
            _activePostCopyOperation = null;
            if (_pauseRequested)
            {
                _pausedPostCopyOperation = operation;
                _pausedQueueEntryId = pausedEntryId;
                _jobQueue.SetState(pausedEntryId, JobQueueEntryState.Paused);
                SaveCurrentJob(JobExecutionState.Paused);
                SetStatus(GetText("Paused"));
                AppendOutput(GetText("Paused"));
            }
            else
            {
                _jobQueue.SetState(pausedEntryId, JobQueueEntryState.Stopped);
                DeletePersistedJob();
                SetStatus(GetText("Stopped"));
                AppendOutput(GetText("Stopped"));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _activePostCopyOperation = null;
            outcome = JobQueueEntryState.Failed;
            _jobQueue.SetState(pausedEntryId, outcome);
            SaveCurrentJob(JobExecutionState.Failed);
            SetStatus(GetText("UnexpectedError"));
            AppendOutput($"{GetText("UnexpectedError")} {exception.Message}");
        }
        finally
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            _isProcessingQueue = false;
            SetRunningState(isRunning: false, dryRun: false);
        }

        if (_pausedQueueEntryId is null && !_stopRequested && _jobQueue.WaitingCount > 0)
        {
            await ProcessQueueAsync();
        }
    }

    private async Task<JobQueueEntryState> ExecuteJobAsync(
        RobocopyJobOptions options,
        bool dryRun,
        Guid? queueEntryId)
    {
        if (!string.IsNullOrWhiteSpace(options.DestinationFileName))
        {
            return await ExecuteNamedFileJobAsync(options, dryRun, queueEntryId);
        }

        RobocopyCommand command;
        try
        {
            command = _commandBuilder.Build(options);
        }
        catch (ArgumentException exception)
        {
            MessageBox.Show(this, exception.Message, GetText("ErrorsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return JobQueueEntryState.Failed;
        }

        CommandPreviewTextBlock.Text = $"{GetText("CommandLabel")}: {command.ToDisplayString()}";
        AppendOutput(dryRun ? GetText("PreviewNotice") : GetText("CopyNotice"));
        AppendOutput(command.ToDisplayString());
        AppendOutput(string.Empty);
        ProgressBar.Value = 0;
        ProgressTextBlock.Text = "0%";
        SetProgressPhase("ProgressCopy", null);
        ClearFileProgress();

        if (_lastMtRecommendation is not null)
        {
            AppendOutput(string.Format(
                CultureInfo.CurrentCulture,
                GetText("AutoMtRecommendationFormat"),
                _lastMtRecommendation.ThreadCount,
                GetText(GetRecommendationReasonResourceKey(_lastMtRecommendation.Reason))));
        }

        if (!dryRun && !TrySaveRunningJob(options, queueEntryId))
        {
            return JobQueueEntryState.Failed;
        }

        _pauseRequested = false;
        _stopRequested = false;
        _cancellationTokenSource = new CancellationTokenSource();
        _uiUpdateBuffer.Clear();
        _uiFlushTimer.Start();
        SetAnalysisProgress();
        SetStatus(GetText("AnalyzingWorkload"));

        try
        {
            var workload = await _fileWorkloadAnalyzer.AnalyzeAsync(options, _cancellationTokenSource.Token);
            _miniCurrentOptions = options;
            _miniCurrentJobTotalFiles = workload.TotalFiles;
            _miniCurrentCompletedFiles = 0;
            _miniCurrentJobWeight = CalculateProgressWeight(workload);
            if (_miniTotalFiles == 0)
            {
                _miniTotalFiles = workload.TotalFiles;
                _miniTotalWeight = _miniCurrentJobWeight;
            }
            IReadOnlyList<OneWaySyncExtraItem> selectedSyncDeletions = [];
            if (options.OperationMode == JobOperationMode.UpdateSync)
            {
                if (dryRun)
                {
                    var syncExtras = await Task.Run(
                        () => _oneWaySyncPlanner.FindDestinationOnlyItems(options, _cancellationTokenSource.Token),
                        _cancellationTokenSource.Token);
                    AppendOutput(string.Format(
                        CultureInfo.CurrentCulture,
                        GetText("OneWaySyncPreviewExtrasFormat"),
                        syncExtras.Count));
                }
                else if (queueEntryId is Guid syncJobId)
                {
                    if (!await ReviewSyncBatchAsync(syncJobId, _cancellationTokenSource.Token))
                    {
                        SetStatus(GetText("OneWaySyncCanceled"));
                        AppendOutput(GetText("OneWaySyncCanceled"));
                        return JobQueueEntryState.Stopped;
                    }
                    selectedSyncDeletions = _syncDeletionSelections[syncJobId];
                }
            }

            var destinationsExistingBeforeRun = !dryRun && options.OperationMode == JobOperationMode.Move
                ? CaptureExistingDestinationFiles(options, workload)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceDirectoryCandidates = !dryRun && options.OperationMode == JobOperationMode.Move
                ? CaptureSourceDirectories(options)
                : [];
            var destinationDirectoriesExistingBeforeRun = !dryRun && options.OperationMode == JobOperationMode.Move
                ? CaptureExistingDestinationDirectories(options, sourceDirectoryCandidates)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var progressAggregator = new JobProgressAggregator(workload);
            UpdateProgress(0);
            SetStatus(dryRun ? GetText("PreviewRunning") : GetText("Running"));
            var summaryCollector = new RobocopySummaryCollector();
            var errorCollector = new RobocopyErrorCollector(options.SourceDirectory, options.DestinationDirectory);
            RobocopyRunResult result;
            using (var destinationMonitor = dryRun
                       ? null
                       : TryCreateDestinationMonitor(options, workload))
            {
                result = await _runner.RunAsync(
                    command,
                    line =>
                    {
                        summaryCollector.ProcessLine(line);
                        if (!dryRun)
                        {
                            errorCollector.ProcessLine(line);
                        }
                        _uiUpdateBuffer.EnqueueOutput(line);
                    },
                    progressChanged: null,
                    cancellationToken: _cancellationTokenSource.Token,
                    fileProgressChanged: fileProgress =>
                    {
                        _uiUpdateBuffer.EnqueueFileProgress(fileProgress);
                        _uiUpdateBuffer.EnqueueOverallProgress(progressAggregator.Report(fileProgress));
                    },
                    listedFilesAreComplete: dryRun);
            }

            FlushUiUpdates(drainAll: true);
            ClearFileProgress();
            _ = RobocopySummaryParser.TryParse(summaryCollector.GetSummary(), out var parsedSummary);
            if (!dryRun && parsedSummary is not null)
            {
                _activeQueueSummary?.Add(
                    parsedSummary,
                    result.Duration,
                    includeDirectories: options.RecurseSubdirectories);
            }
            ShowCopySummary(parsedSummary, result.Duration);

            var interpreted = RobocopyExitCodeInterpreter.Interpret(result.ExitCode);
            IReadOnlyList<ErrorFileRow> mergedErrorRows = [];
            if (!interpreted.IsSuccess)
            {
                mergedErrorRows = MergeErrors(errorCollector.Items);
                if (!dryRun
                    && options.OperationMode == JobOperationMode.Move
                    && mergedErrorRows.Count > 0)
                {
                    var decisionWindow = new MoveFailureDecisionWindow(mergedErrorRows.Count) { Owner = GetDialogOwner() };
                    _ = decisionWindow.ShowDialog();
                    switch (decisionWindow.Decision)
                    {
                        case MoveFailureDecision.MoveVerifiedItemsFirst:
                            {
                                var context = RegisterMoveFailureContext(
                                    options,
                                    MoveFailurePolicy.MoveVerifiedItemsFirst,
                                    mergedErrorRows);
                                AppendOutput(GetText("MoveErrorPartialStarted"));
                                await ExecuteMoveVerificationAsync(
                                    CreateMoveOptionsExcludingErrors(options, context.Rows),
                                    JobQueueEntryState.Failed);
                                _ = Dispatcher.BeginInvoke(
                                    () => ErrorFiles_Click(this, new RoutedEventArgs()),
                                    DispatcherPriority.Background);
                                break;
                            }

                        case MoveFailureDecision.DeferUntilAllResolved:
                            RegisterMoveFailureContext(options, MoveFailurePolicy.DeferUntilAllResolved, mergedErrorRows);
                            AppendOutput(GetText("MoveErrorDeferred"));
                            _ = Dispatcher.BeginInvoke(
                                () => ErrorFiles_Click(this, new RoutedEventArgs()),
                                DispatcherPriority.Background);
                            break;

                        case MoveFailureDecision.DeleteCopiedFiles:
                            if (MessageBox.Show(
                                    this,
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        GetText("MoveErrorDeleteConfirmFormat"),
                                        Environment.NewLine,
                                        options.DestinationDirectory),
                                    GetText("MoveErrorPolicyTitle"),
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Warning) == MessageBoxResult.Yes)
                            {
                                var deleteOperation = new DeletePostCopyOperation(
                                    options,
                                    workload.FileSizes.Keys.ToArray(),
                                    sourceDirectoryCandidates,
                                    destinationsExistingBeforeRun,
                                    destinationDirectoriesExistingBeforeRun,
                                    JobQueueEntryState.Failed);
                                var cleanup = await ExecuteDeleteOperationAsync(deleteOperation);
                                AppendOutput(string.Format(
                                    CultureInfo.CurrentCulture,
                                    GetText("MoveErrorDeleteCompletedFormat"),
                                    cleanup.DeletedFiles,
                                    cleanup.DeletedDirectories,
                                    cleanup.PreservedExistingFiles,
                                    cleanup.SkippedFiles));
                            }

                            break;

                        case MoveFailureDecision.Stop:
                        default:
                            AppendOutput(GetText("MoveErrorStopped"));
                            break;
                    }
                }
            }

            if (interpreted.IsSuccess
                && !dryRun
                && options.OperationMode == JobOperationMode.Move)
            {
                var moveResult = await ExecuteMoveVerificationAsync(options, JobQueueEntryState.Completed);
                if (!moveResult.IsSuccess)
                {
                    SaveCurrentJob(JobExecutionState.Failed);
                    return JobQueueEntryState.Failed;
                }
            }

            if (interpreted.IsSuccess
                && !dryRun
                && options.OperationMode == JobOperationMode.UpdateSync
                && selectedSyncDeletions.Count > 0)
            {
                var deletionResult = await ExecuteOneWaySyncDeletionAsync(new OneWaySyncDeletePostCopyOperation(
                    options,
                    selectedSyncDeletions,
                    JobQueueEntryState.Completed));
                if (!deletionResult.IsSuccess)
                {
                    SaveCurrentJob(JobExecutionState.Failed);
                    return JobQueueEntryState.Failed;
                }
            }

            var status = interpreted.IsSuccess
                ? string.Format(CultureInfo.CurrentCulture, GetText("CompletedFormat"), result.ExitCode, result.Duration.TotalSeconds)
                : string.Format(CultureInfo.CurrentCulture, GetText("FailedFormat"), result.ExitCode);
            SetStatus(status);
            AppendOutput(string.Empty);
            AppendOutput(status);

            if (interpreted.IsSuccess)
            {
                UpdateProgress(100);
                if (!dryRun)
                {
                    if (TryGetCopiedBytes(parsedSummary, out var copiedBytes))
                    {
                        _mtRecommendationService.RecordSuccessfulRun(
                            options.SourceDirectory,
                            options.DestinationDirectory,
                            options.MultiThreadCount,
                            copiedBytes,
                            result.Duration);
                    }

                    DeletePersistedJob();
                }

                return JobQueueEntryState.Completed;
            }

            if (!dryRun)
            {
                SaveCurrentJob(JobExecutionState.Failed);
            }

            return JobQueueEntryState.Failed;
        }
        catch (OperationCanceledException)
        {
            FlushUiUpdates(drainAll: true);
            ClearFileProgress();
            if (!dryRun && _pauseRequested)
            {
                _pausedPostCopyOperation = _activePostCopyOperation;
                _activePostCopyOperation = null;
                SaveCurrentJob(JobExecutionState.Paused);
                SetStatus(GetText("Paused"));
                AppendOutput(GetText("Paused"));
                return JobQueueEntryState.Paused;
            }

            if (!dryRun && _stopRequested)
            {
                _activePostCopyOperation = null;
                _pausedPostCopyOperation = null;
                DeletePersistedJob();
            }

            SetStatus(GetText("Stopped"));
            AppendOutput(GetText("Stopped"));
            return JobQueueEntryState.Stopped;
        }
        catch (Exception exception)
        {
            _activePostCopyOperation = null;
            FlushUiUpdates(drainAll: true);
            ClearFileProgress();
            if (!dryRun)
            {
                SaveCurrentJob(JobExecutionState.Failed);
            }

            SetStatus(GetText("UnexpectedError"));
            AppendOutput($"{GetText("UnexpectedError")} {exception.Message}");
            MessageBox.Show(this, exception.Message, GetText("UnexpectedError"), MessageBoxButton.OK, MessageBoxImage.Error);
            return JobQueueEntryState.Failed;
        }
        finally
        {
            FlushUiUpdates(drainAll: true);
            _uiFlushTimer.Stop();
            ProgressBar.IsIndeterminate = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async Task<JobQueueEntryState> ExecuteNamedFileJobAsync(
        RobocopyJobOptions options,
        bool dryRun,
        Guid? queueEntryId)
    {
        var sourceName = options.FileSpecifications.SingleOrDefault();
        if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(options.DestinationFileName))
        {
            return JobQueueEntryState.Failed;
        }

        var sourcePath = Path.Combine(options.SourceDirectory, sourceName);
        var destinationPath = Path.Combine(options.DestinationDirectory, options.DestinationFileName);
        var sourceLength = File.Exists(sourcePath) ? new FileInfo(sourcePath).Length : 0;
        _miniCurrentOptions = options;
        _miniCurrentJobTotalFiles = 1;
        _miniCurrentCompletedFiles = 0;
        _miniCurrentJobWeight = Math.Max(1, sourceLength);
        if (_miniTotalFiles == 0)
        {
            _miniTotalFiles = 1;
            _miniTotalWeight = _miniCurrentJobWeight;
        }
        CommandPreviewTextBlock.Text = $"{GetText("CommandLabel")}: {sourcePath} → {destinationPath}";
        AppendOutput(dryRun ? GetText("PreviewNotice") : GetText("CopyNotice"));
        AppendOutput($"{sourcePath} → {destinationPath}");
        ProgressBar.Value = 0;
        ProgressTextBlock.Text = "0%";
        SetProgressPhase("ProgressCopy", null);
        ClearFileProgress();

        if (dryRun)
        {
            UpdateProgress(100);
            return JobQueueEntryState.Completed;
        }

        if (!TrySaveRunningJob(options, queueEntryId))
        {
            return JobQueueEntryState.Failed;
        }

        _pauseRequested = false;
        _stopRequested = false;
        _cancellationTokenSource = new CancellationTokenSource();
        SetStatus(GetText("Running"));
        var startedAt = DateTimeOffset.Now;
        try
        {
            var progress = new Progress<double>(UpdateProgress);
            await _namedFileCopyService.CopyAsync(
                sourcePath,
                destinationPath,
                options.OperationMode == JobOperationMode.Move,
                options.OverwriteDestination,
                progress,
                _cancellationTokenSource.Token);
            UpdateProgress(100);
            _activeQueueSummary?.AddCopiedFile(sourceLength, DateTimeOffset.Now - startedAt);
            DeletePersistedJob();
            SetStatus(string.Format(CultureInfo.CurrentCulture, GetText("CompletedFormat"), 1, 0));
            return JobQueueEntryState.Completed;
        }
        catch (OperationCanceledException)
        {
            if (_pauseRequested)
            {
                SaveCurrentJob(JobExecutionState.Paused);
                SetStatus(GetText("Paused"));
                return JobQueueEntryState.Paused;
            }

            DeletePersistedJob();
            SetStatus(GetText("Stopped"));
            return JobQueueEntryState.Stopped;
        }
        catch (MoveSourceDeletionException exception)
        {
            SaveCurrentJob(JobExecutionState.Failed);
            var rows = RegisterMoveVerificationFailures(options,
            [
                new MoveVerificationFailure(
                    exception.SourcePath,
                    exception.DestinationPath,
                    exception.ErrorCode,
                    exception.Message),
            ]);
            SetStatus(GetText("UnexpectedError"));
            AppendOutput($"{GetText("UnexpectedError")} {exception.Message}");
            if (rows.Count > 0)
            {
                _ = Dispatcher.BeginInvoke(() => ErrorFiles_Click(this, new RoutedEventArgs()));
            }

            return JobQueueEntryState.Failed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SaveCurrentJob(JobExecutionState.Failed);
            _activeQueueSummary?.AddFailedFile(sourceLength, DateTimeOffset.Now - startedAt);
            SetStatus(GetText("UnexpectedError"));
            AppendOutput($"{GetText("UnexpectedError")} {exception.Message}");
            MessageBox.Show(this, exception.Message, GetText("UnexpectedError"), MessageBoxButton.OK, MessageBoxImage.Error);
            return JobQueueEntryState.Failed;
        }
        finally
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private bool TryCreateJobOptions(bool dryRun, out RobocopyJobOptions options)
    {
        options = null!;
        var pathErrors = PathSafetyValidator.ValidateCopyDirectories(SourceTextBox.Text, DestinationTextBox.Text);
        if (pathErrors.Count != 0)
        {
            ShowValidationErrors(pathErrors);
            return false;
        }

        if (!TryReadNumberOptions(out var multiThreadCount, out var retryCount, out var retryWaitSeconds))
        {
            MessageBox.Show(this, GetText("InvalidNumber"), GetText("ErrorsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        options = CreateOptions(dryRun, multiThreadCount, retryCount, retryWaitSeconds);
        return true;
    }

    private RobocopyJobOptions CreateQueueTemplate()
    {
        var multiThreadCount = 8;
        var retryCount = 3;
        var retryWaitSeconds = 5;
        _ = TryReadNumberOptions(out multiThreadCount, out retryCount, out retryWaitSeconds);
        multiThreadCount = multiThreadCount is >= 1 and <= 128 ? multiThreadCount : 8;
        retryCount = retryCount is >= 0 and <= 999 ? retryCount : 3;
        retryWaitSeconds = retryWaitSeconds is >= 0 and <= 3600 ? retryWaitSeconds : 5;
        return CreateOptions(dryRun: false, multiThreadCount, retryCount, retryWaitSeconds);
    }

    private RobocopyJobOptions CreateOptions(bool dryRun, int multiThreadCount, int retryCount, int retryWaitSeconds)
    {
        return new RobocopyJobOptions
        {
            OperationMode = Enum.TryParse<JobOperationMode>(
                (OperationComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
                out var operationMode)
                ? operationMode
                : JobOperationMode.Copy,
            SourceDirectory = SourceTextBox.Text,
            DestinationDirectory = DestinationTextBox.Text,
            MultiThreadCount = multiThreadCount,
            RetryCount = retryCount,
            RetryWaitSeconds = retryWaitSeconds,
            Restartable = RestartableCheckBox.IsChecked == true,
            IncludeEmptyDirectories = IncludeEmptyDirectoriesCheckBox.IsChecked == true,
            ExcludeZeroByteFiles = ExcludeZeroByteFilesCheckBox.IsChecked == true,
            SymbolicLinkPolicy = GetLinkPolicy(),
            ExcludedDirectories = _excludedDirectories.ToArray(),
            ExcludedFiles = _excludedFiles.ToArray(),
            DryRun = dryRun,
            LogFilePath = BuildLogFilePath(dryRun),
        };
    }

    private bool TrySaveRunningJob(RobocopyJobOptions options, Guid? queueEntryId)
    {
        _currentJob = new PersistedJob
        {
            Id = queueEntryId ?? Guid.NewGuid(),
            Options = options,
            State = JobExecutionState.Running,
            UpdatedAt = DateTimeOffset.Now,
        };

        try
        {
            _jobRepository.Save(_currentJob);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            MessageBox.Show(this, $"{GetText("JobStateError")} {exception.Message}", GetText("UnexpectedError"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void SaveCurrentJob(JobExecutionState state)
    {
        if (_currentJob is null)
        {
            return;
        }

        try
        {
            _currentJob = _currentJob with { State = state, UpdatedAt = DateTimeOffset.Now };
            _jobRepository.Save(_currentJob);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppendOutput($"{GetText("JobStateError")} {exception.Message}");
        }
    }

    private void DeletePersistedJob()
    {
        try
        {
            _jobRepository.Delete();
            _currentJob = null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppendOutput($"{GetText("JobStateError")} {exception.Message}");
        }
    }

    private bool TryReadNumberOptions(out int multiThreadCount, out int retryCount, out int retryWaitSeconds)
    {
        multiThreadCount = 0;
        retryCount = 0;
        retryWaitSeconds = 0;
        _lastMtRecommendation = null;

        var isAuto = ReferenceEquals(MultiThreadComboBox.SelectedItem, AutoMtComboBoxItem)
            || IsAutoMtDisplayText(MultiThreadComboBox.Text);
        var multiThreadValid = isAuto
            ? TryRecommendMultiThreadCount(out multiThreadCount)
            : int.TryParse(MultiThreadComboBox.Text, out multiThreadCount)
                && multiThreadCount is >= 1 and <= 128;
        var retryCountValid = int.TryParse(RetryCountTextBox.Text, out retryCount)
            && retryCount is >= 0 and <= 999;
        var retryWaitValid = int.TryParse(RetryWaitTextBox.Text, out retryWaitSeconds)
            && retryWaitSeconds is >= 0 and <= 3600;

        return multiThreadValid && retryCountValid && retryWaitValid;
    }

    private bool TryRecommendMultiThreadCount(out int multiThreadCount)
    {
        _lastMtRecommendation = _mtRecommendationService.Recommend(SourceTextBox.Text, DestinationTextBox.Text);
        multiThreadCount = _lastMtRecommendation.ThreadCount;
        return multiThreadCount is >= 1 and <= 128;
    }

    private void UpdateAutoMtDisplay()
    {
        var autoWasSelected = ReferenceEquals(MultiThreadComboBox.SelectedItem, AutoMtComboBoxItem)
            || IsAutoMtDisplayText(MultiThreadComboBox.Text);
        var threadCount = 8;

        if (!string.IsNullOrWhiteSpace(SourceTextBox.Text)
            && !string.IsNullOrWhiteSpace(DestinationTextBox.Text))
        {
            threadCount = _mtRecommendationService
                .Recommend(SourceTextBox.Text, DestinationTextBox.Text)
                .ThreadCount;
        }

        var displayText = string.Format(
            CultureInfo.CurrentCulture,
            GetText("AutoMtDisplayFormat"),
            threadCount);
        AutoMtComboBoxItem.Content = displayText;

        if (autoWasSelected)
        {
            MultiThreadComboBox.SelectedItem = AutoMtComboBoxItem;
            MultiThreadComboBox.Text = displayText;
        }
    }

    private async void ReevaluateAutoMt_Click(object sender, RoutedEventArgs e)
    {
        if (_isProcessingQueue || _isPreviewRunning)
        {
            return;
        }

        var pathErrors = PathSafetyValidator.ValidateCopyDirectories(
            SourceTextBox.Text,
            DestinationTextBox.Text);
        if (pathErrors.Count != 0)
        {
            ShowValidationErrors(pathErrors);
            return;
        }

        if (MessageBox.Show(
                this,
                GetText("MtBenchmarkConfirmation"),
                GetText("ReevaluateAutoMt"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Information) != MessageBoxResult.Yes)
        {
            return;
        }

        _isPreviewRunning = true;
        SetRunningState(isRunning: true, dryRun: true);
        _cancellationTokenSource = new CancellationTokenSource();
        ProgressBar.IsIndeterminate = true;
        ProgressTextBlock.Text = GetText("AnalyzingProgress");
        SetStatus(GetText("MtBenchmarkPreparing"));
        AppendOutput(GetText("MtBenchmarkPreparing"));

        try
        {
            var profile = _systemProfileDetector.Detect(SourceTextBox.Text, DestinationTextBox.Text);
            var physicalStorage = await _physicalStorageProfileDetector.DetectAsync(
                SourceTextBox.Text,
                DestinationTextBox.Text,
                _cancellationTokenSource.Token);
            var systemSummary = string.Format(
                CultureInfo.CurrentCulture,
                GetText("SystemProfileFormat"),
                string.IsNullOrWhiteSpace(profile.CpuName) ? GetText("UnknownValue") : profile.CpuName,
                profile.LogicalProcessorCount,
                profile.TotalPhysicalMemoryBytes == 0
                    ? GetText("UnknownValue")
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        GetText("MemorySizeFormat"),
                        profile.TotalPhysicalMemoryBytes / 1024d / 1024d / 1024d));
            var storageSummary = string.Format(
                CultureInfo.CurrentCulture,
                GetText("StorageProfileFormat"),
                string.IsNullOrWhiteSpace(profile.SourceVolume) ? GetText("UnknownValue") : profile.SourceVolume,
                GetText(GetStorageKindResourceKey(profile.SourceStorageKind)),
                string.IsNullOrWhiteSpace(profile.DestinationVolume) ? GetText("UnknownValue") : profile.DestinationVolume,
                GetText(GetStorageKindResourceKey(profile.DestinationStorageKind)),
                GetText(profile.SameVolume ? "Yes" : "No"));
            AppendOutput(systemSummary);
            AppendOutput(storageSummary);
            AppendOutput(string.Format(
                CultureInfo.CurrentCulture,
                GetText("PhysicalStorageProfileFormat"),
                FormatPhysicalStorageDevice(physicalStorage.SourceDevice),
                FormatPhysicalStorageDevice(physicalStorage.DestinationDevice),
                physicalStorage.SamePhysicalDisk is bool samePhysicalDisk
                    ? GetText(samePhysicalDisk ? "Yes" : "No")
                    : GetText("UnknownValue")));
            var progress = new Progress<MtBenchmarkProgress>(update =>
            {
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = (update.CandidateIndex - 1d) / update.CandidateCount * 100;
                ProgressTextBlock.Text = $"{ProgressBar.Value:F0}%";
                SetStatus(update.Stage == MtBenchmarkStage.Sequential
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        GetText("MtBenchmarkSequentialProgressFormat"),
                        update.CandidateIndex,
                        update.CandidateCount)
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        GetText("MtBenchmarkProgressFormat"),
                        update.ThreadCount,
                        update.CandidateIndex,
                        update.CandidateCount));
            });
            var result = await _mtBenchmarkService.RunAsync(
                SourceTextBox.Text,
                DestinationTextBox.Text,
                progress,
                _cancellationTokenSource.Token);

            AppendOutput(string.Format(
                CultureInfo.CurrentCulture,
                GetText("MtBenchmarkSequentialMeasurementFormat"),
                result.SequentialMeasurement.CopiedBytes / 1024d / 1024d,
                result.SequentialMeasurement.BytesPerSecond / 1024 / 1024,
                result.SequentialMeasurement.Duration.TotalSeconds));
            _mtRecommendationService.RecordBenchmarkMeasurements(
                SourceTextBox.Text,
                DestinationTextBox.Text,
                result.Measurements);
            foreach (var measurement in result.Measurements)
            {
                AppendOutput(string.Format(
                    CultureInfo.CurrentCulture,
                    GetText("MtBenchmarkMeasurementFormat"),
                    measurement.ThreadCount,
                    measurement.BytesPerSecond / 1024 / 1024,
                    measurement.Duration.TotalSeconds));
            }

            _lastMtRecommendation = _mtRecommendationService.Recommend(
                SourceTextBox.Text,
                DestinationTextBox.Text);
            var displayText = string.Format(
                CultureInfo.CurrentCulture,
                GetText("AutoMtDisplayFormat"),
                _lastMtRecommendation.ThreadCount);
            AutoMtComboBoxItem.Content = displayText;
            MultiThreadComboBox.SelectedItem = AutoMtComboBoxItem;
            MultiThreadComboBox.Text = displayText;
            UpdateProgress(100);

            var status = string.Format(
                CultureInfo.CurrentCulture,
                GetText("MtBenchmarkCompletedFormat"),
                _lastMtRecommendation.ThreadCount,
                result.SampleBytes / 1024d / 1024d);
            SetStatus(status);
            AppendOutput(status);
            ShowMainBenchmarkHistoryHint();
        }
        catch (OperationCanceledException)
        {
            ProgressBar.IsIndeterminate = false;
            SetStatus(GetText("MtBenchmarkCancelled"));
            AppendOutput(GetText("MtBenchmarkCancelled"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ProgressBar.IsIndeterminate = false;
            var message = string.Format(
                CultureInfo.CurrentCulture,
                GetText("MtBenchmarkFailedFormat"),
                exception.Message);
            SetStatus(message);
            AppendOutput(message);
            MessageBox.Show(this, message, GetText("UnexpectedError"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProgressBar.IsIndeterminate = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            _isPreviewRunning = false;
            SetRunningState(isRunning: false, dryRun: true);
        }
    }

    private async void ShowMainBenchmarkHistoryHint()
    {
        MtBenchmarkHistoryHintPopup.IsOpen = true;
        await Task.Delay(TimeSpan.FromSeconds(5));
        MtBenchmarkHistoryHintPopup.IsOpen = false;
    }

    private void OpenMtBenchmarkHistory_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SourceTextBox.Text)
            || string.IsNullOrWhiteSpace(DestinationTextBox.Text))
        {
            MessageBox.Show(
                this,
                GetText("MtBenchmarkHistoryPathRequired"),
                GetText("ErrorsTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var historyWindow = new MtBenchmarkHistoryWindow(
            _mtRecommendationService,
            SourceTextBox.Text,
            DestinationTextBox.Text)
        {
            Owner = GetDialogOwner(),
        };
        historyWindow.HistoryChanged += (_, _) => UpdateAutoMtDisplay();
        historyWindow.Show();
    }

    private bool IsAutoMtDisplayText(string text)
    {
        var prefix = $"{GetText("Auto")}(";
        return string.Equals(text.Trim(), GetText("Auto"), StringComparison.CurrentCultureIgnoreCase)
            || text.Trim().StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string GetRecommendationReasonResourceKey(MtRecommendationReason reason)
    {
        return reason switch
        {
            MtRecommendationReason.SameVolume => "MtReasonSameVolume",
            MtRecommendationReason.Network => "MtReasonNetwork",
            MtRecommendationReason.Removable => "MtReasonRemovable",
            MtRecommendationReason.SeparateFixedVolumes => "MtReasonSeparateFixedVolumes",
            MtRecommendationReason.BenchmarkCache => "MtReasonBenchmarkCache",
            _ => "MtReasonUnknown",
        };
    }

    private static string GetStorageKindResourceKey(StorageKind storageKind)
    {
        return storageKind switch
        {
            StorageKind.Fixed => "StorageKindFixed",
            StorageKind.Removable => "StorageKindRemovable",
            StorageKind.Network => "StorageKindNetwork",
            _ => "StorageKindUnknown",
        };
    }

    private string FormatPhysicalStorageDevice(PhysicalStorageDevice? device)
    {
        if (device is null)
        {
            return GetText("UnknownValue");
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            GetText("PhysicalStorageDeviceFormat"),
            string.IsNullOrWhiteSpace(device.FriendlyName) ? GetText("UnknownValue") : device.FriendlyName,
            string.IsNullOrWhiteSpace(device.BusType) ? GetText("UnknownValue") : device.BusType,
            device.DiskNumber);
    }

    private SymbolicLinkPolicy GetLinkPolicy()
    {
        var selected = LinkPolicyComboBox.SelectedItem as ComboBoxItem;
        return Enum.TryParse<SymbolicLinkPolicy>(selected?.Tag?.ToString(), out var policy)
            ? policy
            : SymbolicLinkPolicy.Exclude;
    }

    private string? BuildLogFilePath(bool dryRun)
    {
        if (SaveLogCheckBox.IsChecked != true)
        {
            return null;
        }

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboCopy_GUI_Kr",
            "logs");
        Directory.CreateDirectory(root);
        var prefix = dryRun ? "preview" : "copy";
        return Path.Combine(root, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.log");
    }

    private void ShowValidationErrors(IEnumerable<string> errorKeys)
    {
        var message = string.Join(Environment.NewLine, errorKeys.Select(GetText));
        MessageBox.Show(this, message, GetText("ErrorsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void UpdateExclusionSummary()
    {
        ExclusionSummaryTextBlock.Text = _excludedDirectories.Count == 0 && _excludedFiles.Count == 0
            ? GetText("NoExclusions")
            : string.Format(
                CultureInfo.CurrentCulture,
                GetText("ExclusionSummaryFormat"),
                _excludedDirectories.Count,
                _excludedFiles.Count);
    }

    private void UpdateProgress(double percent)
    {
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = percent;
        ProgressTextBlock.Text = $"{percent:F0}%";
    }

    private void SetAnalysisProgress()
    {
        ProgressBar.Value = 0;
        ProgressBar.IsIndeterminate = true;
        ProgressTextBlock.Text = GetText("AnalyzingProgress");
    }

    private DestinationFileProgressMonitor? TryCreateDestinationMonitor(
        RobocopyJobOptions options,
        FileWorkloadSnapshot workload)
    {
        try
        {
            return new DestinationFileProgressMonitor(
                options.SourceDirectory,
                options.DestinationDirectory,
                workload,
                _uiUpdateBuffer.EnqueueFileProgress);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppendOutput(exception.Message);
            return null;
        }
    }

    private FileCopyProgressItem? UpdateFileProgress(RobocopyFileProgress progress)
    {
        if (progress.Percent >= 100)
        {
            if (_completedFileProgressSources.Add(progress.SourcePath))
            {
                _miniCurrentCompletedFiles = Math.Min(_miniCurrentJobTotalFiles, _miniCurrentCompletedFiles + 1);
            }
            if (_fileProgressBySource.Remove(progress.SourcePath, out var completedItem))
            {
                _fileProgressItems.Remove(completedItem);
            }

            return null;
        }

        if (_completedFileProgressSources.Contains(progress.SourcePath))
        {
            return null;
        }

        if (_fileProgressBySource.TryGetValue(progress.SourcePath, out var existing))
        {
            existing.Update(progress.Percent, progress.DestinationPath);
            return existing;
        }

        if (_fileProgressItems.Count >= MaximumVisibleFileProgressItems)
        {
            var removed = _fileProgressItems[0];
            _fileProgressItems.RemoveAt(0);
            _fileProgressBySource.Remove(removed.SourcePath);
        }

        var item = new FileCopyProgressItem(
            progress.FileName,
            progress.SourcePath,
            progress.DestinationPath,
            progress.Percent);
        _fileProgressBySource.Add(progress.SourcePath, item);
        _fileProgressItems.Add(item);
        return item;
    }

    private void ClearFileProgress()
    {
        _fileProgressBySource.Clear();
        _fileProgressItems.Clear();
        _completedFileProgressSources.Clear();
        CopySummaryDataGrid.ItemsSource = null;
        SummaryTimeTextBlock.Text = string.Empty;
        SummarySpeedFirstTextBlock.Text = string.Empty;
        SummarySpeedSecondTextBlock.Text = string.Empty;
        SummaryEndedTextBlock.Text = string.Empty;
        CopySummaryPanel.Visibility = Visibility.Collapsed;
        FileProgressDataGrid.Visibility = Visibility.Visible;
    }

    private void ShowCopySummary(RobocopySummaryData? parsed, TimeSpan elapsed)
    {
        if (parsed is null)
        {
            return;
        }

        CopySummaryDataGrid.ItemsSource = new[]
        {
            CopySummaryRow.Create(GetText("SummaryDirectories"), parsed.Directories),
            CopySummaryRow.Create(GetText("SummaryFiles"), parsed.Files),
            CopySummaryRow.Create(GetText("SummaryBytes"), parsed.Bytes),
        };
        SummaryTimeTextBlock.Text = FormatSummaryTime(parsed, elapsed);
        SummarySpeedFirstTextBlock.Text = parsed.Speeds.ElementAtOrDefault(0) ?? string.Empty;
        SummarySpeedSecondTextBlock.Text = parsed.Speeds.ElementAtOrDefault(1) ?? string.Empty;
        SummaryEndedTextBlock.Text = parsed.Ended;
        FileProgressDataGrid.Visibility = Visibility.Collapsed;
        CopySummaryPanel.Visibility = Visibility.Visible;
    }

    private void ShowAggregateCopySummary(RobocopySummaryAggregate aggregate)
    {
        var bytesPerSecond = aggregate.BytesPerSecond;
        var summary = new RobocopySummaryData(
            aggregate.Directories.Select(FormatSummaryNumber).ToArray(),
            aggregate.Files.Select(FormatSummaryNumber).ToArray(),
            aggregate.Bytes.Select(FormatSummaryNumber).ToArray(),
            aggregate.Times.Select(FormatElapsedTime).ToArray(),
            [
                string.Format(CultureInfo.CurrentCulture, GetText("SummaryBytesPerSecondFormat"), bytesPerSecond),
                string.Format(CultureInfo.CurrentCulture, GetText("SummaryMegabytesPerMinuteFormat"), bytesPerSecond * 60d / 1_048_576d),
            ],
            DateTime.Now.ToString("F", CultureInfo.CurrentCulture));
        ShowCopySummary(summary, aggregate.Elapsed);
    }

    private static string FormatSummaryNumber(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FormatElapsedTime(TimeSpan elapsed)
    {
        var totalHours = (long)Math.Floor(elapsed.TotalHours);
        return $"{totalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }

    private string FormatSummaryTime(RobocopySummaryData summary, TimeSpan elapsed)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            GetText("SummaryTimeDetailsFormat"),
            FormatElapsedTime(elapsed),
            summary.Times.ElementAtOrDefault(0) ?? "-",
            summary.Times.ElementAtOrDefault(1) ?? "-",
            summary.Times.ElementAtOrDefault(4) ?? "-",
            summary.Times.ElementAtOrDefault(5) ?? "-");
    }

    private static bool TryGetCopiedBytes(RobocopySummaryData? summary, out long copiedBytes)
    {
        copiedBytes = 0;
        return summary is not null
            && summary.Bytes.Count > 1
            && long.TryParse(
                summary.Bytes[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out copiedBytes);
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { CommandParameter: string requestedPath }
            || string.IsNullOrWhiteSpace(requestedPath))
        {
            return;
        }

        try
        {
            ExplorerLocationOpener.Open(requestedPath);
        }
        catch (Exception exception) when (exception is Win32Exception
                                          or InvalidOperationException
                                          or ArgumentException
                                          or NotSupportedException
                                          or IOException)
        {
            MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("OpenLocationFailedFormat"), exception.Message),
                GetText("ErrorsTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AppendOutput(string line)
    {
        AppendOutputBatch([line]);
    }

    private void AppendOutputBatch(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var text = new StringBuilder();
        foreach (var line in lines)
        {
            text.AppendLine(line);
        }

        var appendedText = text.ToString();
        _displayLog.Append(appendedText);
        TrimOutputLog();
        _logWindow?.AppendText(appendedText, MaximumOutputCharacters, RetainedOutputCharacters);
    }

    private void TrimOutputLog()
    {
        if (_displayLog.Length <= MaximumOutputCharacters)
        {
            return;
        }

        var currentText = _displayLog.ToString();
        var startIndex = currentText.Length - RetainedOutputCharacters;
        var lineBreakIndex = currentText.IndexOf(Environment.NewLine, startIndex, StringComparison.Ordinal);
        startIndex = lineBreakIndex >= 0
            ? lineBreakIndex + Environment.NewLine.Length
            : startIndex;
        _displayLog.Clear();
        _displayLog.Append(currentText.AsSpan(startIndex));
        _logWindow?.ReplaceText(_displayLog.ToString());
    }

    private void ClearOutputLog()
    {
        _displayLog.Clear();
        _logWindow?.ReplaceText(string.Empty);
    }

    private void UiFlushTimer_Tick(object? sender, EventArgs e)
    {
        if (_uiUpdateBuffer.HasPendingUpdates)
        {
            FlushUiUpdates(drainAll: false);
        }
    }

    private void FlushUiUpdates(bool drainAll)
    {
        var batch = _uiUpdateBuffer.Drain(
            drainAll ? int.MaxValue : OutputLinesPerUiTick,
            drainAll ? int.MaxValue : FileUpdatesPerUiTick);
        var outputLines = batch.OutputLines;
        if (batch.UpdatesWereDropped)
        {
            outputLines = [GetText("LiveOutputTruncated"), .. outputLines];
        }

        AppendOutputBatch(outputLines);

        if (batch.OverallProgress is double overallProgress)
        {
            UpdateProgress(overallProgress);
        }

        FileCopyProgressItem? lastUpdatedItem = null;
        foreach (var progress in batch.FileProgressUpdates)
        {
            lastUpdatedItem = UpdateFileProgress(progress);
        }

        if (lastUpdatedItem is not null)
        {
            FileProgressDataGrid.ScrollIntoView(lastUpdatedItem);
        }
    }

    private void SetStatus(string status)
    {
        FooterStatusTextBlock.Text = status;
    }

    private void SetRunningState(bool isRunning, bool dryRun)
    {
        ProfileComboBox.IsEnabled = !isRunning;
        SaveProfileButton.IsEnabled = !isRunning;
        PreviewButton.IsEnabled = !isRunning;
        ReevaluateAutoMtButton.IsEnabled = !isRunning;
        StartButton.IsEnabled = !isRunning;
        AddJobButton.IsEnabled = !isRunning || !dryRun;
        PauseButton.IsEnabled = isRunning && !dryRun;
        StopButton.IsEnabled = isRunning;
        ErrorFilesButton.IsEnabled = _errorItems.Count > 0;

        if (!isRunning)
        {
            StartButton.Content = _pausedQueueEntryId is not null
                ? GetText("Resume")
                : _jobQueue.WaitingCount > 0
                    ? GetText("StartQueuedJobs")
                    : GetText("StartCopy");
        }
    }

    private async Task<VerifiedMoveResult> ExecuteMoveVerificationAsync(
        RobocopyJobOptions options,
        JobQueueEntryState completionState)
    {
        _activePostCopyOperation = new MovePostCopyOperation(options, completionState);
        SetProgressPhase("ProgressMove", "MoveInProgressNotice");
        UpdateProgress(0);
        var moveProgress = new Progress<VerifiedMoveProgress>(update =>
        {
            var percent = update.TotalFiles == 0 ? 100 : update.CompletedFiles * 100d / update.TotalFiles;
            UpdateProgress(percent);
            SetStatus(string.Format(
                CultureInfo.CurrentCulture,
                GetText("MoveVerifyingFormat"),
                update.CompletedFiles,
                update.TotalFiles));
        });
        SetStatus(string.Format(CultureInfo.CurrentCulture, GetText("MoveVerifyingFormat"), 0, 0));
        AppendOutput(GetText("MoveVerificationStarted"));
        var cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
        var moveResult = await Task.Run(
            () => _verifiedMoveService.ExecuteAsync(options, moveProgress, cancellationToken),
            cancellationToken);
        _activePostCopyOperation = null;
        if (!moveResult.IsSuccess)
        {
            var errorRows = RegisterMoveVerificationFailures(options, moveResult.Failures);
            foreach (var failure in moveResult.Failures)
            {
                AppendOutput(string.Format(
                    CultureInfo.CurrentCulture,
                    GetText("MoveVerificationFailureFormat"),
                    failure.SourcePath,
                    failure.Reason));
            }

            var status = string.Format(
                CultureInfo.CurrentCulture,
                GetText("MoveVerificationFailedFormat"),
                moveResult.Failures.Count);
            SetStatus(status);
            AppendOutput(status);
            if (errorRows.Count > 0)
            {
                _ = Dispatcher.BeginInvoke(() => ErrorFiles_Click(this, new RoutedEventArgs()));
            }
        }
        else
        {
            UpdateProgress(100);
            SetProgressPhase("ProgressMove", "MoveCompletedNotice");
            AppendOutput(string.Format(
                CultureInfo.CurrentCulture,
                GetText("MoveCompletedFormat"),
                moveResult.DeletedFiles,
                moveResult.DeletedDirectories));
        }

        return moveResult;
    }

    private IReadOnlyList<ErrorFileRow> RegisterMoveVerificationFailures(
        RobocopyJobOptions originalOptions,
        IReadOnlyList<MoveVerificationFailure> failures)
    {
        var rows = MergeErrors(failures.Select(failure => new RobocopyErrorItem(
            failure.ErrorCode,
            RobocopyErrorClassifier.Classify(failure.ErrorCode),
            failure.SourcePath,
            failure.DestinationPath,
            failure.Reason)));
        for (var index = 0; index < Math.Min(rows.Count, failures.Count); index++)
        {
            var failure = failures[index];
            _moveDeletionRetryOptions[rows[index]] = new MoveDeletionRetryContext(
                originalOptions with
                {
                    SourceDirectory = Path.GetDirectoryName(failure.SourcePath)!,
                    DestinationDirectory = Path.GetDirectoryName(failure.DestinationPath)!,
                    FileSpecifications = [Path.GetFileName(failure.SourcePath)],
                    RecurseSubdirectories = false,
                    ExcludedDirectories = [],
                    ExcludedFiles = [],
                    SkipExistingFiles = false,
                    DestinationFileName = null,
                    OverwriteDestination = false,
                },
                originalOptions.SourceDirectory,
                originalOptions.DestinationDirectory);
        }

        return rows;
    }

    private async Task RetryMoveDeletionAsync(ErrorFileRow row, MoveDeletionRetryContext context)
    {
        UpdateRetryState(row, ErrorFileRetryState.Running);
        try
        {
            var result = await _verifiedMoveService.ExecuteAsync(context.RetryOptions);
            if (result.IsSuccess)
            {
                var sourceDirectory = Path.GetDirectoryName(row.SourcePath);
                if (!string.IsNullOrWhiteSpace(sourceDirectory))
                {
                    _verifiedMoveService.DeleteVerifiedEmptyAncestors(
                        sourceDirectory,
                        context.SourceRoot,
                        context.DestinationRoot);
                }

                _moveDeletionRetryOptions.Remove(row);
                UpdateRetryState(row, ErrorFileRetryState.Resolved, "ErrorStatusMoveCompleted");
                return;
            }

            var failure = result.Failures[0];
            var category = RobocopyErrorClassifier.Classify(failure.ErrorCode);
            var item = new RobocopyErrorItem(
                failure.ErrorCode,
                category,
                failure.SourcePath,
                failure.DestinationPath,
                failure.Reason,
                row.Occurrences + 1);
            var categoryText = GetText($"ErrorCategory{category}");
            row.UpdateError(item, categoryText, CreateErrorCodeToolTip(item, categoryText));
            UpdateRetryState(row, ErrorFileRetryState.RetryFailed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var errorCode = exception is UnauthorizedAccessException ? 5 : exception.HResult & 0xFFFF;
            var category = RobocopyErrorClassifier.Classify(errorCode);
            var item = new RobocopyErrorItem(
                errorCode,
                category,
                row.SourcePath,
                row.DestinationPath,
                exception.Message,
                row.Occurrences + 1);
            var categoryText = GetText($"ErrorCategory{category}");
            row.UpdateError(item, categoryText, CreateErrorCodeToolTip(item, categoryText));
            UpdateRetryState(row, ErrorFileRetryState.RetryFailed);
        }
    }

    private async Task<FailedMoveCleanupResult> ExecuteDeleteOperationAsync(DeletePostCopyOperation operation)
    {
        _activePostCopyOperation = operation;
        SetProgressPhase("ProgressDelete", "DeleteInProgressNotice");
        UpdateProgress(0);
        var cleanupProgress = new Progress<FailedMoveCleanupProgress>(update =>
        {
            var percent = update.TotalItems == 0
                ? 100
                : update.CompletedItems * 100d / update.TotalItems;
            UpdateProgress(percent);
        });
        var cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
        var cleanup = await Task.Run(
            () => _failedMoveCleanupService.DeleteNewCopiedFilesAsync(
                operation.Options,
                operation.SourceFiles,
                operation.SourceDirectories,
                operation.ExistingDestinationFiles,
                operation.ExistingDestinationDirectories,
                cleanupProgress,
                cancellationToken),
            cancellationToken);
        _activePostCopyOperation = null;
        UpdateProgress(100);
        SetProgressPhase("ProgressDelete", "DeleteCompletedNotice");
        return cleanup;
    }

    private async Task<OneWaySyncDeletionResult> ExecuteOneWaySyncDeletionAsync(
        OneWaySyncDeletePostCopyOperation operation)
    {
        _activePostCopyOperation = operation;
        SetProgressPhase("ProgressDelete", "DeleteInProgressNotice");
        UpdateProgress(0);
        var deletionProgress = new Progress<OneWaySyncDeletionProgress>(update =>
        {
            var percent = update.TotalItems == 0
                ? 100
                : update.CompletedItems * 100d / update.TotalItems;
            UpdateProgress(percent);
        });
        var cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
        var result = await _oneWaySyncDeletionService.DeleteSelectedAsync(
            operation.Options.SourceDirectory,
            operation.Options.DestinationDirectory,
            operation.SelectedItems,
            deletionProgress,
            cancellationToken);
        _activePostCopyOperation = null;
        if (result.IsSuccess)
        {
            UpdateProgress(100);
            SetProgressPhase("ProgressDelete", "DeleteCompletedNotice");
            AppendOutput(string.Format(
                CultureInfo.CurrentCulture,
                GetText("OneWaySyncDeletionCompletedFormat"),
                result.DeletedFiles,
                result.DeletedDirectories));
        }
        else
        {
            foreach (var failure in result.Failures)
            {
                AppendOutput(string.Format(
                    CultureInfo.CurrentCulture,
                    GetText("OneWaySyncDeletionFailureFormat"),
                    failure.Path,
                    failure.Reason));
            }
        }

        return result;
    }

    private void SetProgressPhase(string labelResourceKey, string? noticeResourceKey)
    {
        ProgressLabelTextBlock.Text = GetText(labelResourceKey);
        if (noticeResourceKey is null)
        {
            PhaseNoticeTextBlock.Text = string.Empty;
            PhaseNoticeTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        PhaseNoticeTextBlock.Text = GetText(noticeResourceKey);
        PhaseNoticeTextBlock.Visibility = Visibility.Visible;
    }

    private MoveFailureContext RegisterMoveFailureContext(
        RobocopyJobOptions options,
        MoveFailurePolicy policy,
        IEnumerable<ErrorFileRow> rows)
    {
        var context = new MoveFailureContext(options, policy);
        foreach (var row in rows)
        {
            context.Rows.Add(row);
            _moveFailureContexts[row] = context;
        }

        return context;
    }

    private static RobocopyJobOptions CreateMoveOptionsExcludingErrors(
        RobocopyJobOptions options,
        IEnumerable<ErrorFileRow> rows)
    {
        var excludedDirectories = options.ExcludedDirectories.ToList();
        var excludedFiles = options.ExcludedFiles.ToList();
        foreach (var row in rows)
        {
            if (Directory.Exists(row.SourcePath))
            {
                excludedDirectories.Add(row.SourcePath);
            }
            else
            {
                excludedFiles.Add(row.SourcePath);
            }
        }

        return options with
        {
            ExcludedDirectories = excludedDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ExcludedFiles = excludedFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        };
    }

    private async Task CompleteMoveFailureRetryAsync(ErrorFileRow row)
    {
        if (!_moveFailureContexts.TryGetValue(row, out var context))
        {
            return;
        }

        if (context.Policy == MoveFailurePolicy.MoveVerifiedItemsFirst)
        {
            UpdateRetryState(row, ErrorFileRetryState.Resolved, "ErrorStatusMoveCompleted");
            _moveFailureContexts.Remove(row);
            return;
        }

        if (context.Rows.Any(item => item.RetryState != ErrorFileRetryState.Resolved))
        {
            return;
        }

        AppendOutput(GetText("MoveErrorFinalVerificationStarted"));
        var ownsCancellationSource = _cancellationTokenSource is null;
        if (ownsCancellationSource)
        {
            _pauseRequested = false;
            _stopRequested = false;
            _cancellationTokenSource = new CancellationTokenSource();
            if (!_isProcessingQueue)
            {
                SetRunningState(isRunning: true, dryRun: false);
            }
        }

        VerifiedMoveResult result;
        try
        {
            result = await ExecuteMoveVerificationAsync(
                context.OriginalOptions,
                JobQueueEntryState.Completed);
        }
        catch (OperationCanceledException) when (ownsCancellationSource)
        {
            foreach (var contextRow in context.Rows)
            {
                _moveFailureContexts.Remove(contextRow);
            }

            if (_pauseRequested)
            {
                _pausedPostCopyOperation = _activePostCopyOperation;
                _activePostCopyOperation = null;
                var pausedEntry = _jobQueue.AddPaused(context.OriginalOptions, Guid.NewGuid());
                _pausedQueueEntryId = pausedEntry.Id;
                SetStatus(GetText("Paused"));
                AppendOutput(GetText("Paused"));
            }
            else
            {
                _activePostCopyOperation = null;
                _pausedPostCopyOperation = null;
                SetStatus(GetText("Stopped"));
                AppendOutput(GetText("Stopped"));
            }

            return;
        }
        finally
        {
            if (ownsCancellationSource)
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                if (!_isProcessingQueue)
                {
                    SetRunningState(isRunning: false, dryRun: false);
                }
            }
        }

        if (!result.IsSuccess)
        {
            foreach (var contextRow in context.Rows)
            {
                UpdateRetryState(contextRow, ErrorFileRetryState.RetryFailed);
            }

            return;
        }

        foreach (var contextRow in context.Rows)
        {
            UpdateRetryState(contextRow, ErrorFileRetryState.Resolved, "ErrorStatusMoveCompleted");
            _moveFailureContexts.Remove(contextRow);
        }
    }

    private static HashSet<string> CaptureExistingDestinationFiles(
        RobocopyJobOptions options,
        FileWorkloadSnapshot workload)
    {
        var sourceRoot = Path.GetFullPath(options.SourceDirectory);
        var destinationRoot = Path.GetFullPath(options.DestinationDirectory);
        return workload.FileSizes.Keys
            .Select(sourcePath => Path.GetFullPath(Path.Combine(
                destinationRoot,
                Path.GetRelativePath(sourceRoot, sourcePath))))
            .Where(File.Exists)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string[] CaptureSourceDirectories(RobocopyJobOptions options)
    {
        if (!options.RecurseSubdirectories || !Directory.Exists(options.SourceDirectory))
        {
            return [];
        }

        var excludedDirectories = options.ExcludedDirectories
            .Where(Path.IsPathFullyQualified)
            .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
            .ToArray();
        var pending = new Stack<string>();
        var discovered = new List<string>();
        pending.Push(Path.GetFullPath(options.SourceDirectory));
        while (pending.Count > 0)
        {
            var parent = pending.Pop();
            foreach (var child in Directory.EnumerateDirectories(parent, "*", SearchOption.TopDirectoryOnly))
            {
                var fullChild = Path.TrimEndingDirectorySeparator(Path.GetFullPath(child));
                var isExcluded = excludedDirectories.Any(excluded =>
                    fullChild.Equals(excluded, StringComparison.OrdinalIgnoreCase)
                    || fullChild.StartsWith(excluded + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                if (isExcluded || (File.GetAttributes(fullChild) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                discovered.Add(fullChild);
                pending.Push(fullChild);
            }
        }

        return discovered.ToArray();
    }

    private static HashSet<string> CaptureExistingDestinationDirectories(
        RobocopyJobOptions options,
        IEnumerable<string> sourceDirectories)
    {
        var sourceRoot = Path.GetFullPath(options.SourceDirectory);
        var destinationRoot = Path.GetFullPath(options.DestinationDirectory);
        return sourceDirectories
            .Select(sourceDirectory => Path.GetFullPath(Path.Combine(
                destinationRoot,
                Path.GetRelativePath(sourceRoot, sourceDirectory))))
            .Where(Directory.Exists)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<ErrorFileRow> MergeErrors(IEnumerable<RobocopyErrorItem> errors)
    {
        var mergedRows = new List<ErrorFileRow>();
        foreach (var error in errors)
        {
            var existing = _errorItems.FirstOrDefault(row =>
                row.ErrorCode == error.ErrorCode
                && string.Equals(row.SourcePath, error.SourcePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(row.DestinationPath, error.DestinationPath, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var merged = error with
                {
                    Occurrences = existing.Occurrences + error.Occurrences,
                };
                var categoryText = GetText($"ErrorCategory{merged.Category}");
                existing.UpdateError(merged, categoryText, CreateErrorCodeToolTip(merged, categoryText));
                if (existing.RetryState is not ErrorFileRetryState.Running and not ErrorFileRetryState.Paused)
                {
                    UpdateRetryState(existing, ErrorFileRetryState.Failed);
                }

                mergedRows.Add(existing);
            }
            else
            {
                var categoryText = GetText($"ErrorCategory{error.Category}");
                var row = new ErrorFileRow(
                    error,
                    categoryText,
                    CreateErrorCodeToolTip(error, categoryText),
                    ErrorFileRetryState.Failed,
                    GetText("ErrorRetryStateFailed"));
                _errorItems.Add(row);
                mergedRows.Add(row);
            }
        }

        RefreshErrorButton();
        return mergedRows;
    }

    private string CreateErrorCodeToolTip(RobocopyErrorItem item, string categoryText) => string.Format(
        CultureInfo.CurrentCulture,
        GetText("ErrorCodeTooltipFormat"),
        item.ErrorCode,
        RobocopyErrorDescription.GetSystemMessage(item.ErrorCode),
        categoryText,
        item.Message);

    private void UpdateRetryState(
        ErrorFileRow row,
        ErrorFileRetryState state,
        string completedStatusResourceKey = "ErrorStatusCompleted")
    {
        row.UpdateRetryState(
            state,
            GetText($"ErrorRetryState{state}"),
            state == ErrorFileRetryState.Resolved ? GetText(completedStatusResourceKey) : null);
        RefreshErrorButton();
    }

    private void RefreshErrorButton()
    {
        var unresolvedCount = _errorItems.Count(row => row.RetryState != ErrorFileRetryState.Resolved);
        ErrorFilesButton.Content = string.Format(
            CultureInfo.CurrentCulture,
            GetText("ErrorFilesButtonFormat"),
            unresolvedCount);
        ErrorFilesButton.IsEnabled = _errorItems.Count > 0;
    }

    private string GetText(string resourceKey)
    {
        return FindResource(resourceKey) as string ?? resourceKey;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await InitializeStartupAsync();

    private async Task InitializeStartupAsync()
    {
        if (_startupInitialized)
        {
            return;
        }

        _startupInitialized = true;
        LoadProfiles();

        if (_startupRequests.Count > 0)
        {
            ApplyDefaultProfileSettings();
            DeletePersistedJob();
            await ApplyShellStartupRequestsAsync();
            return;
        }

        PersistedJob? savedJob;
        try
        {
            savedJob = _jobRepository.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(this, $"{GetText("JobStateError")} {exception.Message}", GetText("PreviousJobTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (savedJob is null)
        {
            return;
        }

        var prompt = string.Format(
            CultureInfo.CurrentCulture,
            GetText("PreviousJobPromptFormat"),
            Environment.NewLine,
            savedJob.Options.SourceDirectory,
            savedJob.Options.DestinationDirectory);
        var result = MessageBox.Show(this, prompt, GetText("PreviousJobTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            RestoreJob(savedJob);
        }
        else
        {
            DeletePersistedJob();
        }
    }

    private async Task ApplyShellStartupRequestsAsync()
    {
        IReadOnlyList<ShellCopyRequest> startupRequests = _startupRequests;
        var conflictCandidates = startupRequests
            .Where(request => request.OperationMode is JobOperationMode.Copy or JobOperationMode.Move)
            .ToArray();
        if (!_shellConflictsAlreadyReviewed && conflictCandidates.Length > 0)
        {
            var scanWindow = new ShellConflictScanProgressWindow { Owner = GetDialogOwner() };
            ShellCopyConflictAnalysis analysis;
            try
            {
                scanWindow.Show();
                await Dispatcher.Yield(DispatcherPriority.Background);
                var progress = new Progress<ShellConflictScanProgress>(scanWindow.Report);
                analysis = await new ShellCopyConflictPlanner().AnalyzeAsync(conflictCandidates, progress);
            }
            finally
            {
                scanWindow.Close();
            }

            if (analysis.Groups.Count > 0)
            {
                var conflictWindow = new ShellCopyConflictWindow(analysis.Groups) { Owner = GetDialogOwner() };
                if (conflictWindow.ShowDialog() != true)
                {
                    SetStatus(GetText("Ready"));
                    return;
                }

                startupRequests = new ShellConflictResolutionPlanner().Apply(
                    startupRequests,
                    conflictWindow.Resolutions,
                    analysis.PlannedDestinationPaths);
            }
        }

        var first = startupRequests[0];
        _miniBatchSourceDirectory = FindCommonDirectory(startupRequests.Select(request => request.SourceDirectory));
        _miniBatchDestinationDirectory = FindCommonDirectory(startupRequests.Select(request => request.DestinationDirectory));
        SelectOperationMode(first.OperationMode);
        SourceTextBox.Text = first.SourceDirectory;
        DestinationTextBox.Text = first.DestinationDirectory;
        var template = CreateQueueTemplate();
        MergeShellSelectionExclusions(first);

        if (startupRequests.Count == 1 && first.FileSpecifications.Count == 0 && !first.StartImmediately)
        {
            SetStatus(GetText("ShellRequestReady"));
            return;
        }

        if (startupRequests.All(request => request.OperationMode == JobOperationMode.ContinuousSync && request.StartImmediately))
        {
            StartContinuousSyncWithDefaults(startupRequests.Select(request => ApplyShellRequest(template, request)));
            return;
        }

        var startupOptions = startupRequests.Select(request => ApplyShellRequest(template, request)).ToArray();
        if (_preferMiniUi)
        {
            var workload = await CountMiniBatchWorkloadAsync(startupOptions);
            _miniTotalFiles = workload.TotalFiles;
            _miniTotalWeight = workload.TotalWeight;
            _miniProcessedFilesBeforeCurrent = 0;
            _miniProcessedWeightBeforeCurrent = 0;
            _miniCurrentJobTotalFiles = 0;
            _miniCurrentCompletedFiles = 0;
            _miniCurrentJobWeight = 0;
        }

        foreach (var options in startupOptions)
        {
            _jobQueue.Enqueue(options);
        }

        SetStatus(string.Format(CultureInfo.CurrentCulture, GetText("ShellRequestsQueuedFormat"), startupRequests.Count));
        if (startupRequests.Any(request => request.StartImmediately))
        {
            await ProcessQueueAsync();
        }
        else
        {
            SetStatus(GetText("ShellRequestReady"));
        }
    }

    internal async Task<bool> ReviewSyncBatchAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var entries = _jobQueue.Items;
        // Edited or removed jobs must not inherit deletion approval for old paths.
        foreach (var id in _syncReviewedOptions.Keys.ToArray())
        {
            if (!entries.Any(entry => entry.Id == id && entry.Options == _syncReviewedOptions[id]))
            {
                _syncReviewedOptions.Remove(id);
                _syncDeletionSelections.Remove(id);
            }
        }
        if (_syncDeletionSelections.ContainsKey(jobId)) return true;
        var jobs = entries.Where(entry => entry.Options.OperationMode == JobOperationMode.UpdateSync
                && (entry.Id == jobId || entry.State == JobQueueEntryState.Waiting)
                && !_syncDeletionSelections.ContainsKey(entry.Id))
            .ToDictionary(entry => entry.Id, entry => entry.Options);
        var plan = await Task.Run(() => OneWaySyncBatchPlan.Create(jobs, cancellationToken), cancellationToken);
        IReadOnlyList<OneWaySyncExtraItem> selected = [];
        if (plan.Items.Count > 0)
        {
            var review = new OneWaySyncReviewWindow(plan.Items) { Owner = GetDialogOwner() };
            if (review.ShowDialog() != true) return false;
            selected = review.SelectedForDeletion;
        }
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var (id, items) in plan.ApplySelection(selected))
        {
            _syncDeletionSelections[id] = items;
            _syncReviewedOptions[id] = jobs[id];
        }
        return true;
    }

    private void MergeShellSelectionExclusions(ShellCopyRequest request)
    {
        foreach (var directory in request.ExcludedDirectories)
        {
            if (!_excludedDirectories.Contains(directory, StringComparer.OrdinalIgnoreCase))
            {
                _excludedDirectories.Add(directory);
            }
        }

        foreach (var file in request.ExcludedFiles)
        {
            if (!_excludedFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
            {
                _excludedFiles.Add(file);
            }
        }

        UpdateExclusionSummary();
    }

    private async Task<(int TotalFiles, long TotalWeight)> CountMiniBatchWorkloadAsync(IEnumerable<RobocopyJobOptions> options)
    {
        var totalFiles = 0;
        long totalWeight = 0;
        foreach (var item in options)
        {
            var workload = await _fileWorkloadAnalyzer.AnalyzeAsync(item, CancellationToken.None);
            totalFiles = checked(totalFiles + workload.TotalFiles);
            totalWeight = checked(totalWeight + CalculateProgressWeight(workload));
        }
        return (totalFiles, totalWeight);
    }

    private static long CalculateProgressWeight(FileWorkloadSnapshot workload) =>
        workload.FileSizes.Values.Sum(size => Math.Max(1L, size));

    private static string? FindCommonDirectory(IEnumerable<string> paths)
    {
        var normalized = paths.Select(Path.GetFullPath).Select(Path.TrimEndingDirectorySeparator).ToArray();
        if (normalized.Length == 0) return null;
        var candidate = normalized[0];
        while (!normalized.All(path => string.Equals(path, candidate, StringComparison.OrdinalIgnoreCase)
                                      || path.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = Path.GetDirectoryName(candidate) ?? candidate;
            if (string.Equals(candidate, Path.GetPathRoot(candidate), StringComparison.OrdinalIgnoreCase)) break;
        }
        return candidate;
    }

    private static RobocopyJobOptions ApplyShellRequest(RobocopyJobOptions template, ShellCopyRequest request) => template with
    {
        OperationMode = request.OperationMode,
        SourceDirectory = request.SourceDirectory,
        DestinationDirectory = request.DestinationDirectory,
        FileSpecifications = request.FileSpecifications,
        RecurseSubdirectories = request.RecurseSubdirectories,
        ExcludedDirectories = template.ExcludedDirectories.Concat(request.ExcludedDirectories)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        ExcludedFiles = template.ExcludedFiles.Concat(request.ExcludedFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        DestinationFileName = request.DestinationFileName,
        OverwriteDestination = request.OverwriteDestination,
        SkipExistingFiles = request.SkipExistingFiles,
    };

    private void SelectOperationMode(JobOperationMode operationMode)
    {
        _isOpeningContinuousSyncSettings = true;
        try
        {
            var item = OperationComboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Tag?.ToString(),
                    operationMode.ToString(),
                    StringComparison.Ordinal));
            if (item is not null)
            {
                OperationComboBox.SelectedItem = item;
            }
        }
        finally
        {
            _isOpeningContinuousSyncSettings = false;
        }
    }

    private void StartContinuousSyncWithDefaults(IEnumerable<RobocopyJobOptions> options)
    {
        try
        {
            if (!_continuousSyncWorkflow.StartWithDefaults(this, options))
            {
                return;
            }

            SetStatus(GetText("ContinuousSyncAgentStarted"));
        }
        catch (ContinuousSyncTaskConflictException)
        {
            MessageBox.Show(this, GetText("ContinuousSyncPathConflict"), GetText("ContinuousSyncSettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception exception) when (exception is IOException
                                               or UnauthorizedAccessException
                                               or InvalidOperationException
                                               or ArgumentException
                                               or NotSupportedException)
        {
            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, GetText("ContinuousSyncAgentStartFailedFormat"), exception.Message),
                GetText("ContinuousSyncSettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreJob(PersistedJob job)
    {
        _currentJob = job;
        var options = job.Options;
        foreach (var item in OperationComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), options.OperationMode.ToString(), StringComparison.Ordinal))
            {
                OperationComboBox.SelectedItem = item;
                break;
            }
        }

        SourceTextBox.Text = options.SourceDirectory;
        DestinationTextBox.Text = options.DestinationDirectory;
        MultiThreadComboBox.SelectedItem = null;
        MultiThreadComboBox.Text = options.MultiThreadCount.ToString(CultureInfo.InvariantCulture);
        RetryCountTextBox.Text = options.RetryCount.ToString(CultureInfo.InvariantCulture);
        RetryWaitTextBox.Text = options.RetryWaitSeconds.ToString(CultureInfo.InvariantCulture);
        RestartableCheckBox.IsChecked = options.Restartable;
        IncludeEmptyDirectoriesCheckBox.IsChecked = options.IncludeEmptyDirectories;
        ExcludeZeroByteFilesCheckBox.IsChecked = options.ExcludeZeroByteFiles;
        SaveLogCheckBox.IsChecked = !string.IsNullOrWhiteSpace(options.LogFilePath);

        foreach (var item in LinkPolicyComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), options.SymbolicLinkPolicy.ToString(), StringComparison.Ordinal))
            {
                LinkPolicyComboBox.SelectedItem = item;
                break;
            }
        }

        _excludedDirectories.Clear();
        _excludedDirectories.AddRange(options.ExcludedDirectories);
        _excludedFiles.Clear();
        _excludedFiles.AddRange(options.ExcludedFiles);
        UpdateExclusionSummary();

        _jobQueue.AddPaused(options, job.Id);
        _pausedQueueEntryId = job.Id;
        StartButton.Content = GetText("Resume");
        SetStatus(GetText("PreviousJobRestored"));
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _uiFlushTimer.Stop();
        if (_cancellationTokenSource is not null && _currentJob is not null)
        {
            _pauseRequested = true;
            SaveCurrentJob(JobExecutionState.Paused);
            _jobQueue.SetState(_currentJob.Id, JobQueueEntryState.Paused);
        }

        _cancellationTokenSource?.Cancel();
    }
}
