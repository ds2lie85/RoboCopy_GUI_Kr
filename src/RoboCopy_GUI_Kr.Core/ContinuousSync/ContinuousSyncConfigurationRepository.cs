using System.Text.Json;
using System.Text.Json.Serialization;
using RoboCopy_GUI_Kr.Core.Validation;

namespace RoboCopy_GUI_Kr.Core.ContinuousSync;

public sealed class ContinuousSyncConfigurationRepository
{
    private sealed record ConfigurationDocument
    {
        public int Version { get; init; } = 1;

        public IReadOnlyList<ContinuousSyncConfiguration> Tasks { get; init; } = [];
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _configurationPath;

    public ContinuousSyncConfigurationRepository(string? dataRoot = null)
    {
        var root = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboCopy_GUI_Kr");
        _configurationPath = Path.Combine(root, "continuous-sync", "configuration.json");
    }

    public string ConfigurationPath => _configurationPath;

    public IReadOnlyList<ContinuousSyncConfiguration> LoadAll()
    {
        if (!File.Exists(_configurationPath))
        {
            return [];
        }

        var document = JsonSerializer.Deserialize<ConfigurationDocument>(
            File.ReadAllText(_configurationPath),
            SerializerOptions) ?? new ConfigurationDocument();
        if (document.Version != 1)
        {
            throw new NotSupportedException("Unsupported continuous synchronization configuration version.");
        }

        foreach (var configuration in document.Tasks)
        {
            Validate(configuration, requireSourceExists: false);
        }

        return document.Tasks.ToArray();
    }

    public void Save(ContinuousSyncConfiguration configuration)
    {
        Validate(configuration, requireSourceExists: true);
        var configurations = LoadAll().ToList();
        if (FindConflicts(configuration, configurations).Count != 0)
        {
            throw new ContinuousSyncTaskConflictException();
        }

        var existingIndex = configurations.FindIndex(item => item.Id == configuration.Id);
        if (existingIndex >= 0)
        {
            configurations[existingIndex] = configuration;
        }
        else
        {
            configurations.Add(configuration);
        }

        WriteAll(configurations);
    }

    public IReadOnlyList<ContinuousSyncConfiguration> FindConflicts(ContinuousSyncConfiguration configuration)
    {
        Validate(configuration, requireSourceExists: true);
        return FindConflicts(configuration, LoadAll());
    }

    public ContinuousSyncConfiguration ReplaceConflicting(ContinuousSyncConfiguration configuration)
    {
        Validate(configuration, requireSourceExists: true);
        var configurations = LoadAll().ToList();
        var conflicts = FindConflicts(configuration, configurations);
        if (conflicts.Count == 0)
        {
            Save(configuration);
            return configuration;
        }

        var candidateAlreadyExists = configurations.Any(item => item.Id == configuration.Id);
        var replacement = candidateAlreadyExists
            ? configuration
            : configuration with { Id = conflicts[0].Id };
        var removedIds = conflicts.Select(item => item.Id).Append(configuration.Id).ToHashSet();
        var insertionIndex = configurations.FindIndex(item => removedIds.Contains(item.Id));
        configurations.RemoveAll(item => removedIds.Contains(item.Id));
        configurations.Insert(insertionIndex < 0 ? configurations.Count : insertionIndex, replacement);
        WriteAll(configurations);
        return replacement;
    }

    public void Delete(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Continuous synchronization task ID is required.", nameof(id));
        }

        WriteAll(LoadAll().Where(configuration => configuration.Id != id));
    }

    public void Delete(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var removedIds = ids.Where(id => id != Guid.Empty).ToHashSet();
        if (removedIds.Count == 0)
        {
            return;
        }

        WriteAll(LoadAll().Where(configuration => !removedIds.Contains(configuration.Id)));
    }

    private void WriteAll(IEnumerable<ContinuousSyncConfiguration> configurations)
    {
        var directory = Path.GetDirectoryName(_configurationPath)
            ?? throw new InvalidOperationException("Continuous synchronization configuration directory could not be resolved.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _configurationPath + ".tmp";
        var document = new ConfigurationDocument { Tasks = configurations.ToArray() };
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, SerializerOptions));
        File.Move(temporaryPath, _configurationPath, overwrite: true);
    }

    public static void Validate(ContinuousSyncConfiguration configuration, bool requireSourceExists = true)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Id == Guid.Empty)
        {
            throw new ArgumentException("Continuous synchronization task ID is required.", nameof(configuration));
        }

        if (configuration.MonitorIntervalMinutes is < 1 or > 1440)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Monitor interval must be between 1 and 1440 minutes.");
        }

        if (!Enum.IsDefined(configuration.MultiThreadMode))
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Continuous synchronization multi-thread mode is invalid.");
        }

        if (configuration.ScheduleStartMinute is < 0 or > 1439
            || configuration.ScheduleEndMinute is < 0 or > 1440)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Continuous synchronization schedule is invalid.");
        }

        var job = configuration.JobOptions
            ?? throw new ArgumentException("Job options are required.", nameof(configuration));
        var pathErrors = PathSafetyValidator.ValidateCopyDirectories(job.SourceDirectory, job.DestinationDirectory)
            .Where(error => requireSourceExists || error != "SourceNotFound")
            .ToArray();
        if (pathErrors.Length != 0)
        {
            throw new ArgumentException(
                $"Continuous synchronization paths are invalid: {string.Join(", ", pathErrors)}",
                nameof(configuration));
        }

        if (job.MultiThreadCount is < 1 or > 128
            || job.RetryCount is < 0 or > 999
            || job.RetryWaitSeconds is < 0 or > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Continuous synchronization numeric options are invalid.");
        }

        var destinationRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(job.DestinationDirectory))
            + Path.DirectorySeparatorChar;
        foreach (var retainedPath in configuration.RetainedDestinationOnlyPaths)
        {
            if (string.IsNullOrWhiteSpace(retainedPath) || Path.IsPathFullyQualified(retainedPath))
            {
                throw new ArgumentException("Retained destination-only paths must be relative paths.", nameof(configuration));
            }

            var fullRetainedPath = Path.GetFullPath(Path.Combine(job.DestinationDirectory, retainedPath));
            if (!fullRetainedPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("A retained destination-only path is outside the destination root.", nameof(configuration));
            }
        }
    }

    private static bool HasUnsafePathOverlap(
        ContinuousSyncConfiguration candidate,
        ContinuousSyncConfiguration existing)
    {
        var candidateSource = Normalize(candidate.JobOptions.SourceDirectory);
        var candidateDestination = Normalize(candidate.JobOptions.DestinationDirectory);
        var existingSource = Normalize(existing.JobOptions.SourceDirectory);
        var existingDestination = Normalize(existing.JobOptions.DestinationDirectory);
        return Overlaps(candidateDestination, existingDestination)
               || Overlaps(candidateDestination, existingSource)
               || Overlaps(candidateSource, existingDestination);
    }

    public static bool HasPathConflict(
        ContinuousSyncConfiguration candidate,
        ContinuousSyncConfiguration existing)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existing);
        return HasUnsafePathOverlap(candidate, existing) || HasUnsafePathOverlap(existing, candidate);
    }

    private static IReadOnlyList<ContinuousSyncConfiguration> FindConflicts(
        ContinuousSyncConfiguration candidate,
        IEnumerable<ContinuousSyncConfiguration> configurations) =>
        configurations
            .Where(existing => existing.Id != candidate.Id && HasUnsafePathOverlap(candidate, existing))
            .ToArray();

    private static string Normalize(string path) => Path.GetFullPath(path)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool Overlaps(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase)
        || left.StartsWith(right + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || right.StartsWith(left + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
