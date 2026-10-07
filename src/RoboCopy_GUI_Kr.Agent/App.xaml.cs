using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using RoboCopy_GUI_Kr.Core.ContinuousSync;
using RoboCopy_GUI_Kr.Core.Errors;
using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Performance;
using RoboCopy_GUI_Kr.Core.Robocopy;
using RoboCopy_GUI_Kr.Core.Sync;
using Forms = System.Windows.Forms;

namespace RoboCopy_GUI_Kr.Agent;

public partial class App : System.Windows.Application
{
    private readonly ContinuousSyncConfigurationRepository _repository = new();
    private readonly ContinuousSyncRunRequestRepository _runRequestRepository = new();
    private readonly ContinuousSyncNotificationRepository _notificationRepository = new();
    private readonly RobocopyCommandBuilder _commandBuilder = new();
    private readonly RobocopyRunner _runner = new();
    private readonly OneWaySyncPlanner _planner = new();
    private readonly OneWaySyncDeletionService _deletionService = new();
    private readonly OneWaySyncQuarantineService _quarantineService = new();
    private readonly MtRecommendationService _mtRecommendationService = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentDictionary<Guid, byte> _requestedTasks = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _nextRuns = new();
    private readonly ConcurrentDictionary<Guid, byte> _runningTasks = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningTaskCancellations = new();
    private readonly ConcurrentDictionary<Guid, string> _runningTaskFingerprints = new();
    private readonly ConcurrentDictionary<Guid, ContinuousSyncChangeMonitor> _changeMonitors = new();
    private readonly Dictionary<Guid, string> _knownConfigurationFingerprints = [];
    private readonly SemaphoreSlim _reviewGate = new(1, 1);
    private readonly object _requestGate = new();
    private Mutex? _instanceMutex;
    private EventWaitHandle? _reloadEvent;
    private Forms.NotifyIcon? _notifyIcon;
    private bool _syncAllRequested = true;
    private Guid? _latestNotificationId;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instanceMutex = new Mutex(true, ContinuousSyncAgentSignals.InstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _reloadEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ContinuousSyncAgentSignals.ReloadEventName);
        InitializeTrayIcon();
        try
        {
            AgentStartupRegistration.Register(Environment.ProcessPath
                ?? throw new InvalidOperationException("Agent executable path could not be resolved."));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            Log($"Startup registration failed: {exception.Message}");
        }

        _ = RunSchedulerAsync(_shutdown.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        foreach (var monitor in _changeMonitors.Values) monitor.Dispose();
        _notifyIcon?.Dispose();
        _reloadEvent?.Dispose();
        if (_instanceMutex is not null)
        {
            try
            {
                _instanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was not acquired after an early startup exit.
            }

            _instanceMutex.Dispose();
        }

        _shutdown.Dispose();
        base.OnExit(e);
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = Text("AgentName"),
            Visible = true,
        };
        _notifyIcon.BalloonTipClicked += (_, _) => OpenLatestNotification();
        RebuildTrayMenu([]);
    }

    private async Task RunSchedulerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<ContinuousSyncConfiguration> configurations;
            try
            {
                configurations = _repository.LoadAll();
                var externallyRequestedIds = _runRequestRepository.Consume();
                var isSelectiveStartup = _knownConfigurationFingerprints.Count == 0
                    && externallyRequestedIds.Count > 0;
                foreach (var requestedId in externallyRequestedIds)
                {
                    _requestedTasks[requestedId] = 0;
                }
                if (isSelectiveStartup)
                {
                    lock (_requestGate)
                    {
                        _syncAllRequested = false;
                    }
                    var requestedSet = externallyRequestedIds.ToHashSet();
                    foreach (var configuration in configurations.Where(item => !requestedSet.Contains(item.Id)))
                    {
                        _knownConfigurationFingerprints[configuration.Id] = CreateConfigurationFingerprint(configuration);
                        _nextRuns[configuration.Id] = DateTimeOffset.Now.AddMinutes(configuration.MonitorIntervalMinutes);
                    }
                }
                await Dispatcher.InvokeAsync(() => RebuildTrayMenu(configurations));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
            {
                Log($"Configuration load failed: {exception.Message}");
                ShowBalloon(Text("AgentName"), string.Format(CultureInfo.CurrentCulture, Text("AgentTaskFailedFormat"), exception.Message));
                await WaitForSignalAsync(TimeSpan.FromMinutes(1), cancellationToken);
                continue;
            }

            var configuredIds = configurations.Select(item => item.Id).ToHashSet();
            ReconcileConfigurationChanges(configurations, configuredIds);
            ReconcileChangeMonitors(configurations, configuredIds);
            foreach (var removedId in _nextRuns.Keys.Where(id => !configuredIds.Contains(id)).ToArray())
            {
                _nextRuns.TryRemove(removedId, out _);
            }

            var now = DateTimeOffset.Now;
            bool runAll;
            lock (_requestGate)
            {
                runAll = _syncAllRequested;
                _syncAllRequested = false;
            }

            foreach (var configuration in configurations)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (_runningTasks.ContainsKey(configuration.Id)) continue;
                var requested = _requestedTasks.TryRemove(configuration.Id, out _);
                var dueByTime = !_nextRuns.TryGetValue(configuration.Id, out var nextRun) || nextRun <= now;
                var monitor = _changeMonitors[configuration.Id];
                if (!runAll && !requested && (!dueByTime || !monitor.IsDirty))
                {
                    if (dueByTime) _nextRuns[configuration.Id] = now.AddMinutes(configuration.MonitorIntervalMinutes);
                    continue;
                }

                var pauseReason = ContinuousSyncExecutionPolicy.Evaluate(
                    configuration,
                    DateTimeOffset.Now,
                    Forms.SystemInformation.PowerStatus.PowerLineStatus == Forms.PowerLineStatus.Offline,
                    configuration.PauseOnMeteredNetwork && MeteredNetworkDetector.IsMetered());
                if (pauseReason != ContinuousSyncPauseReason.None)
                {
                    SetTrayStatus(Text(pauseReason switch
                    {
                        ContinuousSyncPauseReason.Battery => "AgentPausedBattery",
                        ContinuousSyncPauseReason.MeteredNetwork => "AgentPausedMetered",
                        _ => "AgentPausedSchedule",
                    }));
                    _nextRuns[configuration.Id] = DateTimeOffset.Now.AddMinutes(1);
                    continue;
                }

                if (_runningTasks.TryAdd(configuration.Id, 0))
                {
                    _nextRuns.TryRemove(configuration.Id, out _);
                    var taskCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    _runningTaskCancellations[configuration.Id] = taskCancellation;
                    _runningTaskFingerprints[configuration.Id] = CreateConfigurationFingerprint(configuration);
                    _ = ExecuteScheduledTaskAsync(
                        configuration,
                        monitor,
                        monitor.CaptureVersion(),
                        taskCancellation.Token);
                }
            }

            var delay = configurations.Count == 0
                ? TimeSpan.FromMinutes(1)
                : _nextRuns.Count == 0
                    ? TimeSpan.FromSeconds(10)
                    : _nextRuns.Values.Min() - DateTimeOffset.Now;
            if (delay < TimeSpan.FromSeconds(1))
            {
                delay = TimeSpan.FromSeconds(1);
            }

            await WaitForSignalAsync(delay, cancellationToken);
        }
    }

    private async Task ExecuteScheduledTaskAsync(
        ContinuousSyncConfiguration configuration,
        ContinuousSyncChangeMonitor monitor,
        long observedVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await ExecuteTaskAsync(configuration, cancellationToken))
            {
                monitor.MarkProcessed(observedVersion);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            _nextRuns[configuration.Id] = DateTimeOffset.Now.AddMinutes(configuration.MonitorIntervalMinutes);
            _runningTasks.TryRemove(configuration.Id, out _);
            _runningTaskFingerprints.TryRemove(configuration.Id, out _);
            if (_runningTaskCancellations.TryRemove(configuration.Id, out var taskCancellation))
            {
                taskCancellation.Dispose();
            }
            _reloadEvent?.Set();
        }
    }

    private void ReconcileConfigurationChanges(
        IReadOnlyList<ContinuousSyncConfiguration> configurations,
        IReadOnlySet<Guid> configuredIds)
    {
        var currentFingerprints = configurations.ToDictionary(
            configuration => configuration.Id,
            CreateConfigurationFingerprint);
        foreach (var configuration in configurations)
        {
            if (!_knownConfigurationFingerprints.TryGetValue(configuration.Id, out var known)
                || !string.Equals(known, currentFingerprints[configuration.Id], StringComparison.Ordinal))
            {
                _requestedTasks[configuration.Id] = 0;
            }
        }

        foreach (var running in _runningTaskFingerprints.ToArray())
        {
            var wasRemoved = !configuredIds.Contains(running.Key);
            var wasChanged = currentFingerprints.TryGetValue(running.Key, out var current)
                && !string.Equals(running.Value, current, StringComparison.Ordinal);
            if ((wasRemoved || wasChanged)
                && _runningTaskCancellations.TryGetValue(running.Key, out var cancellation))
            {
                cancellation.Cancel();
            }
        }

        _knownConfigurationFingerprints.Clear();
        foreach (var item in currentFingerprints)
        {
            _knownConfigurationFingerprints[item.Key] = item.Value;
        }
    }

    private static string CreateConfigurationFingerprint(ContinuousSyncConfiguration configuration) =>
        JsonSerializer.Serialize(configuration);

    private async Task<bool> ExecuteTaskAsync(ContinuousSyncConfiguration configuration, CancellationToken cancellationToken)
    {
        var source = configuration.JobOptions.SourceDirectory;
        var destination = configuration.JobOptions.DestinationDirectory;
        if (!Directory.Exists(source))
        {
            var message = string.Format(CultureInfo.CurrentCulture, Text("AgentSourceUnavailableFormat"), source);
            Log(message);
            SetTrayStatus(message);
            return false;
        }

        if (!TryEnsureDestination(destination))
        {
            var message = string.Format(CultureInfo.CurrentCulture, Text("AgentDestinationUnavailableFormat"), destination);
            Log(message);
            SetTrayStatus(message);
            return false;
        }

        try
        {
            SetTrayStatus($"{Path.GetFileName(source)} → {Path.GetFileName(destination)}");
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RoboCopy_GUI_Kr",
                "continuous-sync",
                "robocopy-logs");
            Directory.CreateDirectory(logDirectory);
            var options = configuration.JobOptions with
            {
                OperationMode = JobOperationMode.UpdateSync,
                MultiThreadCount = configuration.MultiThreadMode switch
                {
                    ContinuousSyncMultiThreadMode.Recommended => _mtRecommendationService
                        .RecommendResourceConscious(source, destination).ThreadCount,
                    ContinuousSyncMultiThreadMode.MaximumPerformance => _mtRecommendationService
                        .RecommendHighestPerformance(source, destination).ThreadCount,
                    _ => configuration.JobOptions.MultiThreadCount,
                },
                RetryCount = 0,
                RetryWaitSeconds = 0,
                Restartable = true,
                DryRun = false,
                LogFilePath = Path.Combine(logDirectory, $"{configuration.Id:N}.log"),
            };
            var quarantineRoot = OneWaySyncQuarantineService.GetQuarantineRoot(destination, configuration.Id);
            var normalizedDestination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination)) + Path.DirectorySeparatorChar;
            if (quarantineRoot.StartsWith(normalizedDestination, StringComparison.OrdinalIgnoreCase))
            {
                var quarantineTopLevel = Path.GetRelativePath(destination, quarantineRoot)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                options = options with
                {
                    ExcludedDirectories = options.ExcludedDirectories
                        .Append(quarantineTopLevel)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                };
            }
            var errorCollector = new RobocopyErrorCollector(source, destination);
            var summaryCollector = new RobocopySummaryCollector();
            var summaryGate = new object();
            var copiedPaths = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var result = await _runner.RunAsync(
                _commandBuilder.Build(options),
                outputReceived: line =>
                {
                    lock (summaryGate)
                    {
                        summaryCollector.ProcessLine(line);
                        errorCollector.ProcessLine(line);
                    }
                },
                cancellationToken: cancellationToken,
                fileProgressChanged: progress =>
                {
                    if (progress.Percent >= 100)
                    {
                        copiedPaths.TryAdd(Path.GetRelativePath(destination, progress.DestinationPath), 0);
                    }
                });
            var exitCode = RobocopyExitCodeInterpreter.Interpret(result.ExitCode);
            if (!exitCode.IsSuccess)
            {
                var errors = errorCollector.Items.Count > 0
                    ? errorCollector.Items
                    : [new RobocopyErrorItem(
                        result.ExitCode,
                        RobocopyErrorCategory.Unknown,
                        source,
                        destination,
                        $"Robocopy exit code {result.ExitCode}")];
                var retry = await ReviewErrorsAsync(errors);
                if (retry)
                {
                    RequestTask(configuration.Id);
                }

                var failureMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Text("AgentTaskFailedFormat"),
                    $"Robocopy exit code {result.ExitCode}");
                Log(failureMessage);
                SetTrayStatus(failureMessage);
                ShowBalloon(Text("AgentName"), failureMessage);
                return false;
            }

            _ = RobocopySummaryParser.TryParse(summaryCollector.GetSummary(), out var parsedSummary);

            var extras = await Task.Run(() => _planner.FindDestinationOnlyItems(options, cancellationToken), cancellationToken);
            if (configuration.QuarantineDeletedItems)
            {
                extras = _quarantineService.ExcludeEquivalentPreviouslyQuarantinedFiles(
                    destination,
                    configuration.Id,
                    extras);
            }
            IReadOnlyCollection<OneWaySyncExtraItem> selected;
            if (configuration.DestinationOnlyPolicy == DestinationOnlyPolicy.Ask)
            {
                var reviewSet = DestinationOnlyRetention.Split(extras, configuration.RetainedDestinationOnlyPaths);
                var review = await ReviewExtrasAsync(reviewSet.NewItems, reviewSet.RetainedItems);
                selected = review.SelectedForDeletion;
                if (review.Applied)
                {
                    var retainedPaths = DestinationOnlyRetention.Update(
                        configuration.RetainedDestinationOnlyPaths,
                        reviewSet.NewItems,
                        review.SelectedNewForDeletion,
                        review.SelectedRetainedForDeletion);
                    SaveRetainedPaths(configuration, retainedPaths);
                }
            }
            else
            {
                selected = configuration.DestinationOnlyPolicy == DestinationOnlyPolicy.Delete ? extras : [];
            }
            if (selected.Count > 0)
            {
                var deletion = configuration.QuarantineDeletedItems
                    ? await _quarantineService.QuarantineSelectedAsync(
                        source,
                        destination,
                        selected,
                        configuration.Id,
                        cancellationToken)
                    : await _deletionService.DeleteSelectedAsync(
                        source,
                        destination,
                        selected,
                        cancellationToken: cancellationToken);
                if (!deletion.IsSuccess)
                {
                    var errors = deletion.Failures.Select(failure => new RobocopyErrorItem(
                        0,
                        RobocopyErrorCategory.Unknown,
                        failure.Path,
                        failure.Path,
                        failure.Reason)).ToArray();
                    if (await ReviewErrorsAsync(errors))
                    {
                        RequestTask(configuration.Id);
                    }

                    return false;
                }
            }

            if (configuration.QuarantineDeletedItems)
            {
                _quarantineService.PurgeExpired(destination, configuration.Id, TimeSpan.FromDays(7), DateTimeOffset.UtcNow);
            }

            var deletedFiles = selected.Count(item => !item.IsDirectory);
            var copiedFiles = Math.Max(copiedPaths.Count, ParseSummaryCount(parsedSummary?.Files.ElementAtOrDefault(1)));
            if (copiedFiles + deletedFiles > 0)
            {
                var notification = new ContinuousSyncNotification
                {
                    SourceDirectory = source,
                    DestinationDirectory = destination,
                    CopiedFiles = copiedFiles,
                    DeletedFiles = deletedFiles,
                    Changes = copiedPaths.Keys.OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
                        .Select(path => new ContinuousSyncChangeItem(ContinuousSyncChangeKind.Copied, path))
                        .Concat(selected.Where(item => !item.IsDirectory).Select(item =>
                            new ContinuousSyncChangeItem(ContinuousSyncChangeKind.Deleted, item.RelativePath)))
                        .ToArray(),
                };
                _notificationRepository.Add(notification);
                _latestNotificationId = notification.Id;
                ShowSyncNotification(notification);
            }

            Log(string.Format(CultureInfo.CurrentCulture, Text("AgentTaskCompletedFormat"), source, destination));
            SetTrayStatus(Text("AgentRunning"));
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            var message = string.Format(CultureInfo.CurrentCulture, Text("AgentTaskFailedFormat"), exception.Message);
            Log(message);
            SetTrayStatus(message);
            ShowBalloon(Text("AgentName"), message);
            return false;
        }
    }

    private void ReconcileChangeMonitors(
        IReadOnlyList<ContinuousSyncConfiguration> configurations,
        IReadOnlySet<Guid> configuredIds)
    {
        foreach (var removedId in _changeMonitors.Keys.Where(id => !configuredIds.Contains(id)).ToArray())
        {
            if (_changeMonitors.TryRemove(removedId, out var removed)) removed.Dispose();
        }
        foreach (var configuration in configurations)
        {
            _changeMonitors.AddOrUpdate(
                configuration.Id,
                _ => new ContinuousSyncChangeMonitor(configuration.JobOptions.SourceDirectory, configuration.JobOptions.DestinationDirectory),
                (_, existing) =>
                {
                    if (existing.Matches(configuration.JobOptions.SourceDirectory, configuration.JobOptions.DestinationDirectory)
                        && !existing.IsFallback) return existing;
                    existing.Dispose();
                    return new ContinuousSyncChangeMonitor(configuration.JobOptions.SourceDirectory, configuration.JobOptions.DestinationDirectory);
                });
        }
    }

    private async Task<AgentExtrasReviewResult> ReviewExtrasAsync(
        IReadOnlyList<OneWaySyncExtraItem> newItems,
        IReadOnlyList<OneWaySyncExtraItem> retainedItems)
    {
        if (newItems.Count == 0)
        {
            return new AgentExtrasReviewResult(false, [], []);
        }

        await _reviewGate.WaitAsync(_shutdown.Token);
        try
        {
            return await Dispatcher.InvokeAsync(() =>
            {
                var window = new AgentReviewWindow(newItems, retainedItems);
                if (window.ShowDialog() != true)
                {
                    return new AgentExtrasReviewResult(false, [], []);
                }
                return new AgentExtrasReviewResult(
                    true,
                    window.SelectedForDeletion,
                    window.SelectedRetainedForDeletion);
            });
        }
        finally
        {
            _reviewGate.Release();
        }
    }

    private void SaveRetainedPaths(ContinuousSyncConfiguration configuration, IReadOnlyList<string> retainedPaths)
    {
        _repository.Save(configuration with { RetainedDestinationOnlyPaths = retainedPaths });
    }

    private async Task<bool> ReviewErrorsAsync(IReadOnlyList<RobocopyErrorItem> errors)
    {
        return await Dispatcher.InvokeAsync(() =>
        {
            var window = new AgentErrorWindow(errors);
            window.ShowDialog();
            return window.RetryRequested;
        });
    }

    private void RebuildTrayMenu(IReadOnlyList<ContinuousSyncConfiguration> configurations)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem(
            configurations.Count == 0 ? Text("AgentNoTasks") : Text("AgentRunning"))
        { Enabled = false });
        menu.Items.Add(Text("AgentSyncAllNow"), null, (_, _) => RequestAll());
        menu.Items.Add(Text("AgentNotificationList"), null, (_, _) => OpenNotificationList());
        menu.Items.Add(new Forms.ToolStripSeparator());

        foreach (var configuration in configurations)
        {
            var taskMenu = new Forms.ToolStripMenuItem(BuildTaskLabel(configuration));
            taskMenu.DropDownItems.Add(Text("AgentSyncNow"), null, (_, _) => RequestTask(configuration.Id));
            taskMenu.DropDownItems.Add(Text("AgentChangeSettings"), null, (_, _) => EditTask(configuration, AgentTaskEditMode.Edit));
            taskMenu.DropDownItems.Add(Text("AgentQuarantine"), null, (_, _) => OpenQuarantine(configuration));
            taskMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
            taskMenu.DropDownItems.Add(Text("AgentDeleteTask"), null, (_, _) => DeleteTask(configuration));
            menu.Items.Add(taskMenu);
        }

        menu.Items.Add(Text("AgentAddTask"), null, (_, _) => EditTask(null, AgentTaskEditMode.Add));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Text("AgentExit"), null, (_, _) => ExitAgent());
        var oldMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = menu;
        oldMenu?.Dispose();
    }

    private void EditTask(
        ContinuousSyncConfiguration? configuration,
        AgentTaskEditMode mode,
        bool replaceConflictsOnSave = false)
    {
        var window = new AgentTaskEditorWindow(configuration, mode);
        if (window.ShowDialog() != true || window.Configuration is not { } updated)
        {
            return;
        }

        try
        {
            if (replaceConflictsOnSave)
            {
                var replacement = _repository.ReplaceConflicting(updated);
                RequestTask(replacement.Id);
                return;
            }

            var conflicts = _repository.FindConflicts(updated);
            ContinuousSyncConfiguration saved;
            if (conflicts.Count == 0)
            {
                _repository.Save(updated);
                saved = updated;
            }
            else
            {
                var conflictWindow = new AgentTaskConflictWindow(conflicts, updated, _repository, () => _reloadEvent?.Set());
                if (conflictWindow.ShowDialog() != true)
                {
                    return;
                }

                if (conflictWindow.Action == AgentConflictAction.UseSelected)
                {
                    foreach (var selected in conflictWindow.SelectedConfigurations)
                    {
                        RequestTask(selected.Id == updated.Id
                            ? _repository.ReplaceConflicting(selected).Id
                            : selected.Id);
                    }
                    return;
                }

                if (conflictWindow.Action == AgentConflictAction.ChangeSettings
                    && conflictWindow.ConfigurationToEdit is { } configurationToEdit)
                {
                    EditTask(
                        configurationToEdit,
                        AgentTaskEditMode.Edit,
                        conflictWindow.ConfigurationToEditIsCandidate);
                    return;
                }

                if (conflictWindow.Action != AgentConflictAction.RegisteredListChanged
                    || _repository.FindConflicts(updated).Count != 0)
                {
                    return;
                }

                _repository.Save(updated);
                saved = updated;
            }

            RequestTask(saved.Id);
        }
        catch (ContinuousSyncTaskConflictException)
        {
            System.Windows.MessageBox.Show(
                Text("AgentPathConflict"),
                Text("AgentName"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            System.Windows.MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, Text("AgentTaskFailedFormat"), exception.Message),
                Text("AgentName"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void DeleteTask(ContinuousSyncConfiguration configuration)
    {
        var prompt = string.Format(
            CultureInfo.CurrentCulture,
            Text("AgentDeleteConfirmFormat"),
            Environment.NewLine,
            configuration.JobOptions.SourceDirectory,
            configuration.JobOptions.DestinationDirectory);
        if (System.Windows.MessageBox.Show(prompt, Text("AgentName"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _repository.Delete(configuration.Id);
        _nextRuns.TryRemove(configuration.Id, out _);
        RequestAll();
    }

    private void OpenQuarantine(ContinuousSyncConfiguration configuration)
    {
        var window = new AgentQuarantineWindow(configuration);
        window.FilesRestored += (_, eventArgs) => SaveRestoredPathsAsRetained(configuration.Id, eventArgs.RelativePaths);
        window.ShowDialog();
    }

    private void SaveRestoredPathsAsRetained(Guid configurationId, IReadOnlyList<string> restoredPaths)
    {
        try
        {
            var current = _repository.LoadAll().Single(item => item.Id == configurationId);
            var retained = current.RetainedDestinationOnlyPaths
                .Concat(restoredPaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _repository.Save(current with { RetainedDestinationOnlyPaths = retained });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            System.Windows.MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, Text("AgentTaskFailedFormat"), exception.Message),
                Text("AgentName"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RequestTask(Guid id)
    {
        _requestedTasks[id] = 0;
        _reloadEvent?.Set();
    }

    private void RequestAll()
    {
        lock (_requestGate)
        {
            _syncAllRequested = true;
        }

        _reloadEvent?.Set();
    }

    private void ExitAgent()
    {
        if (System.Windows.MessageBox.Show(
                Text("AgentExitConfirm"),
                Text("AgentName"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            AgentStartupRegistration.Unregister();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            Log($"Startup unregistration failed: {exception.Message}");
        }

        _shutdown.Cancel();
        Shutdown();
    }

    private async Task WaitForSignalAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (_reloadEvent is null)
        {
            await Task.Delay(delay, cancellationToken);
            return;
        }

        var timeout = delay > TimeSpan.FromMilliseconds(int.MaxValue)
            ? int.MaxValue
            : Math.Max(1, (int)delay.TotalMilliseconds);
        await Task.Run(
            () => WaitHandle.WaitAny([_reloadEvent, cancellationToken.WaitHandle], timeout),
            CancellationToken.None);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static bool TryEnsureDestination(string destination)
    {
        if (Directory.Exists(destination))
        {
            return true;
        }

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(destination));
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return false;
            }

            Directory.CreateDirectory(destination);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private void SetTrayStatus(string status) => Dispatcher.BeginInvoke(() =>
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Text = status.Length <= 63 ? status : status[..63];
        }
    });

    private void ShowBalloon(string title, string message, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Warning) => Dispatcher.BeginInvoke(() =>
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(5000);
    });

    private void OpenNotificationList() => Dispatcher.BeginInvoke(() =>
    {
        try
        {
            new AgentNotificationListWindow(_notificationRepository.LoadAll()).Show();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            System.Windows.MessageBox.Show(exception.Message, Text("AgentNotificationListTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    });

    private void OpenLatestNotification() => Dispatcher.BeginInvoke(() =>
    {
        if (_latestNotificationId is not Guid id) return;
        try
        {
            var notification = _notificationRepository.LoadAll().FirstOrDefault(item => item.Id == id);
            if (notification is not null) OpenNotification(notification);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log(exception.Message);
        }
    });

    private string FormatNotification(ContinuousSyncNotification notification) => string.Format(
        CultureInfo.CurrentCulture,
        Text("AgentNotificationFormat"),
        notification.Changes.FirstOrDefault() is { RelativePath: { Length: > 0 } path }
            ? Path.GetFileName(path)
            : Text("AgentNotificationUnknownFile"),
        notification.CopiedFiles + notification.DeletedFiles,
        notification.CopiedFiles,
        notification.DeletedFiles);

    private void ShowSyncNotification(ContinuousSyncNotification notification) => Dispatcher.BeginInvoke(() =>
    {
        var toast = new AgentSyncToastWindow(notification, FormatNotification(notification), OpenNotification);
        toast.Show();
    });

    private static void OpenNotification(ContinuousSyncNotification notification) =>
        AgentNotificationDetailWindow.Open(notification);

    private static string FolderName(string path) => Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path;

    private static int ParseSummaryCount(string? value) => int.TryParse(
        value?.Replace(",", string.Empty, StringComparison.Ordinal),
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var count) ? Math.Max(0, count) : 0;

    private static string BuildTaskLabel(ContinuousSyncConfiguration configuration)
    {
        var sourceName = Path.GetFileName(Path.TrimEndingDirectorySeparator(configuration.JobOptions.SourceDirectory));
        var destinationName = Path.GetFileName(Path.TrimEndingDirectorySeparator(configuration.JobOptions.DestinationDirectory));
        sourceName = string.IsNullOrWhiteSpace(sourceName) ? configuration.JobOptions.SourceDirectory : sourceName;
        destinationName = string.IsNullOrWhiteSpace(destinationName) ? configuration.JobOptions.DestinationDirectory : destinationName;
        return $"{sourceName} → {destinationName}";
    }

    private string Text(string key) => FindResource(key) as string ?? key;

    private static void Log(string message)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RoboCopy_GUI_Kr",
                "continuous-sync");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "agent.log"),
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine(exception);
        }
    }
}

internal sealed record AgentExtrasReviewResult(
    bool Applied,
    IReadOnlyCollection<OneWaySyncExtraItem> SelectedNewForDeletion,
    IReadOnlyCollection<OneWaySyncExtraItem> SelectedRetainedForDeletion)
{
    public IReadOnlyCollection<OneWaySyncExtraItem> SelectedForDeletion => SelectedNewForDeletion;
}
