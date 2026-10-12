namespace RoboCopy_GUI_Kr.Core.Shell;

public enum ShellConflictDecision
{
    Skip,
    Overwrite,
    KeepBoth,
}

public sealed record ShellConflictResolution(
    ShellCopyConflictGroup Group,
    ShellConflictDecision Decision,
    string? WinnerPath = null);

public sealed class ShellConflictResolutionPlanner
{
    public IReadOnlyList<ShellCopyRequest> Apply(
        IReadOnlyList<ShellCopyRequest> requests,
        IEnumerable<ShellConflictResolution> resolutions,
        IEnumerable<string> plannedDestinationPaths)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(resolutions);

        var directCopies = new List<ShellCopyRequest>();
        var conflictSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reservedDestinations = plannedDestinationPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var resolution in resolutions)
        {
            foreach (var source in resolution.Group.Sources)
            {
                conflictSources.Add(source.SourcePath);
            }

            switch (resolution.Decision)
            {
                case ShellConflictDecision.Skip:
                    if (resolution.Group.ExistingDestination is null
                        && resolution.Group.Sources.FirstOrDefault() is { } firstSource)
                    {
                        directCopies.Add(CreateDirectCopy(
                            requests,
                            firstSource.SourcePath,
                            resolution.Group.DestinationPath,
                            overwrite: false));
                    }

                    break;

                case ShellConflictDecision.Overwrite:
                    var winner = resolution.WinnerPath is null && resolution.Group.Sources.Count == 1
                        ? resolution.Group.Sources[0]
                        : resolution.Group.Sources.FirstOrDefault(source => string.Equals(
                        source.SourcePath,
                        resolution.WinnerPath,
                        StringComparison.OrdinalIgnoreCase));
                    if (winner is not null)
                    {
                        directCopies.Add(CreateDirectCopy(
                            requests,
                            winner.SourcePath,
                            resolution.Group.DestinationPath,
                            overwrite: true));
                    }

                    break;

                case ShellConflictDecision.KeepBoth:
                    var firstOriginal = resolution.Group.ExistingDestination is null
                        ? resolution.Group.Sources.FirstOrDefault()
                        : null;
                    if (firstOriginal is not null)
                    {
                        directCopies.Add(CreateDirectCopy(
                            requests,
                            firstOriginal.SourcePath,
                            resolution.Group.DestinationPath,
                            overwrite: false));
                    }

                    foreach (var source in resolution.Group.Sources)
                    {
                        if (ReferenceEquals(source, firstOriginal))
                        {
                            continue;
                        }

                        var renamedDestination = FindAvailableName(resolution.Group.DestinationPath, reservedDestinations);
                        reservedDestinations.Add(renamedDestination);
                        directCopies.Add(CreateDirectCopy(
                            requests,
                            source.SourcePath,
                            renamedDestination,
                            overwrite: false));
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(resolutions));
            }
        }

        // Exact conflict choices run first. The original bulk Robocopy jobs then skip every
        // destination file that already exists, avoiding an arbitrarily long /XF argument list.
        var bulkRequests = requests.Select(request => request with
            {
                SkipExistingFiles = conflictSources.Any(path => IsOwnedByRequest(path, request)),
            })
            .ToArray();
        return directCopies.Concat(bulkRequests).ToArray();
    }

    private static ShellCopyRequest CreateDirectCopy(
        IReadOnlyList<ShellCopyRequest> requests,
        string sourcePath,
        string destinationPath,
        bool overwrite)
    {
        var template = FindOwningRequest(requests, sourcePath);
        return new ShellCopyRequest(
            Path.GetDirectoryName(sourcePath)!,
            Path.GetDirectoryName(destinationPath)!,
            [Path.GetFileName(sourcePath)],
            RecurseSubdirectories: false)
        {
            OperationMode = template.OperationMode,
            StartImmediately = template.StartImmediately,
            DestinationFileName = Path.GetFileName(destinationPath),
            OverwriteDestination = overwrite,
        };
    }

    private static ShellCopyRequest FindOwningRequest(IEnumerable<ShellCopyRequest> requests, string sourcePath) =>
        requests.First(request => IsOwnedByRequest(sourcePath, request));

    private static bool IsOwnedByRequest(string path, ShellCopyRequest request)
    {
        if (!IsPathWithin(path, request.SourceDirectory))
        {
            return false;
        }

        if (request.FileSpecifications.Count == 0)
        {
            return true;
        }

        var relativePath = Path.GetRelativePath(request.SourceDirectory, path);
        return request.FileSpecifications.Contains(relativePath, StringComparer.OrdinalIgnoreCase)
               || request.FileSpecifications.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsPathWithin(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        return fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindAvailableName(string destinationPath, IReadOnlySet<string> reserved)
    {
        var directory = Path.GetDirectoryName(destinationPath)!;
        var extension = Path.GetExtension(destinationPath);
        var baseName = Path.GetFileNameWithoutExtension(destinationPath);
        for (var suffix = 1; suffix < int.MaxValue; suffix++)
        {
            var candidate = Path.Combine(directory, $"{baseName} ({suffix}){extension}");
            if (!reserved.Contains(candidate) && !File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"No available destination file name: {destinationPath}");
    }
}
