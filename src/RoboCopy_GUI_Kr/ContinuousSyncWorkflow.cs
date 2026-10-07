using System.Globalization;
using System.IO;
using System.Windows;
using RoboCopy_GUI_Kr.Core.ContinuousSync;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr;

public sealed class ContinuousSyncWorkflow
{
    private readonly ContinuousSyncConfigurationRepository _repository;
    private readonly ContinuousSyncAgentLauncher _launcher;

    public ContinuousSyncWorkflow(
        ContinuousSyncConfigurationRepository? repository = null,
        ContinuousSyncAgentLauncher? launcher = null)
    {
        _repository = repository ?? new ContinuousSyncConfigurationRepository();
        _launcher = launcher ?? new ContinuousSyncAgentLauncher();
    }

    public bool StartWithDefaults(Window? owner, IEnumerable<RobocopyJobOptions> jobs)
    {
        var candidates = jobs.Select(CreateDefaultConfiguration).ToArray();
        return ProcessCandidates(owner, candidates, []);
    }

    public bool ConfigureAndStart(
        Window? owner,
        RobocopyJobOptions options,
        ContinuousSyncConfiguration? existing = null)
    {
        ContinuousSyncConfiguration configuration;
        while (true)
        {
            var settings = new ContinuousSyncSettingsWindow(options, existing);
            SetOwner(settings, owner);
            if (settings.ShowDialog() != true || settings.Configuration is not { } edited)
            {
                return false;
            }

            if (!ConfirmDeletionPolicy(owner, edited))
            {
                existing = edited;
                continue;
            }

            configuration = edited;
            break;
        }

        return SaveOrUseExisting(owner, configuration);
    }

    public bool SaveOrUseExisting(Window? owner, ContinuousSyncConfiguration configuration)
    {
        var conflicts = _repository.FindConflicts(configuration);
        if (conflicts.Count == 0)
        {
            _repository.Save(configuration);
            _launcher.StartOrReload([configuration.Id]);
            return true;
        }

        return ResolveConflicts(owner, conflicts, [configuration], []);
    }

    private bool ProcessCandidates(
        Window? owner,
        IReadOnlyList<ContinuousSyncConfiguration> candidates,
        IReadOnlyCollection<Guid> alreadySelectedIds)
    {
        var selectedIds = alreadySelectedIds.ToHashSet();
        var pending = new List<ContinuousSyncConfiguration>();
        var conflicts = new Dictionary<Guid, ContinuousSyncConfiguration>();
        foreach (var candidate in candidates)
        {
            var candidateConflicts = _repository.FindConflicts(candidate);
            if (candidateConflicts.Count == 0)
            {
                _repository.Save(candidate);
                selectedIds.Add(candidate.Id);
                continue;
            }

            pending.Add(candidate);
            foreach (var conflict in candidateConflicts)
            {
                conflicts[conflict.Id] = conflict;
            }
        }

        if (pending.Count == 0)
        {
            _launcher.StartOrReload(selectedIds);
            return selectedIds.Count > 0;
        }

        return ResolveConflicts(owner, conflicts.Values.ToArray(), pending, selectedIds);
    }

    private bool ResolveConflicts(
        Window? owner,
        IReadOnlyList<ContinuousSyncConfiguration> conflicts,
        IReadOnlyList<ContinuousSyncConfiguration> pendingCandidates,
        IReadOnlyCollection<Guid> selectedIds)
    {
        var conflictWindow = new ContinuousSyncConflictWindow(conflicts, pendingCandidates, _repository);
        conflictWindow.ConfigureRequested += (_, request) =>
        {
            request.Applied = request.IsCandidate
                ? ConfigureCandidateAndStart(conflictWindow, request.Configuration)
                : ConfigureAndStart(conflictWindow, request.Configuration.JobOptions, request.Configuration);
        };
        SetOwner(conflictWindow, owner);
        if (conflictWindow.ShowDialog() != true)
        {
            if (selectedIds.Count > 0)
            {
                _launcher.StartOrReload(selectedIds);
            }

            return selectedIds.Count > 0;
        }

        switch (conflictWindow.Action)
        {
            case ContinuousSyncConflictAction.UseSelected:
            {
                var pendingIds = pendingCandidates.Select(item => item.Id).ToHashSet();
                var ids = selectedIds.ToHashSet();
                foreach (var selected in conflictWindow.SelectedConfigurations)
                {
                    if (pendingIds.Contains(selected.Id))
                    {
                        ids.Add(_repository.ReplaceConflicting(selected).Id);
                    }
                    else
                    {
                        ids.Add(selected.Id);
                    }
                }

                _launcher.StartOrReload(ids);
                return ids.Count > 0;
            }
            case ContinuousSyncConflictAction.ConfigurationApplied:
            {
                if (selectedIds.Count > 0)
                {
                    _launcher.StartOrReload(selectedIds);
                }

                return true;
            }
            case ContinuousSyncConflictAction.RegisteredListChanged:
                return ProcessCandidates(owner, pendingCandidates, selectedIds);
            default:
                return false;
        }
    }

    private bool ConfigureCandidateAndStart(Window? owner, ContinuousSyncConfiguration candidate)
    {
        ContinuousSyncConfiguration configuration;
        var current = candidate;
        while (true)
        {
            var settings = new ContinuousSyncSettingsWindow(current.JobOptions, current);
            SetOwner(settings, owner);
            if (settings.ShowDialog() != true || settings.Configuration is not { } edited)
            {
                return false;
            }

            if (!ConfirmDeletionPolicy(owner, edited))
            {
                current = edited;
                continue;
            }

            configuration = edited;
            break;
        }

        var saved = _repository.ReplaceConflicting(configuration);
        _launcher.StartOrReload([saved.Id]);
        return true;
    }

    private static ContinuousSyncConfiguration CreateDefaultConfiguration(RobocopyJobOptions options) => new()
    {
        JobOptions = options with
        {
            OperationMode = JobOperationMode.UpdateSync,
            RetryCount = 0,
            RetryWaitSeconds = 0,
            Restartable = true,
            DryRun = false,
            LogFilePath = null,
        },
        MultiThreadMode = ContinuousSyncMultiThreadMode.Recommended,
        DestinationOnlyPolicy = DestinationOnlyPolicy.Delete,
        QuarantineDeletedItems = true,
    };

    private static bool ConfirmDeletionPolicy(Window? owner, ContinuousSyncConfiguration configuration)
    {
        if (configuration.DestinationOnlyPolicy != DestinationOnlyPolicy.Delete)
        {
            return true;
        }

        var message = Application.Current.FindResource("ContinuousSyncDeleteConfirm") as string ?? string.Empty;
        var title = Application.Current.FindResource("ContinuousSyncSettingsTitle") as string ?? string.Empty;
        return owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private static void SetOwner(Window child, Window? owner)
    {
        if (owner is { IsVisible: true })
        {
            child.Owner = owner;
        }
    }
}
