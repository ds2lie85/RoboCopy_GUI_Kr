using System.Collections.Concurrent;

namespace RoboCopy_GUI_Kr.Core.Shell;

public enum ShellConflictScanStage
{
    CollectingFiles,
    ScanningDestinations,
    ComparingDestinations,
}

public sealed record ShellConflictScanProgress(ShellConflictScanStage Stage, int Completed, int Total);

public sealed record ShellCopyCandidate(
    string SourcePath,
    string DestinationPath,
    long Size,
    DateTime ModifiedAt,
    long Sequence = 0,
    DateTime CreatedAt = default);

public sealed record ShellExistingDestination(
    string Path,
    long Size,
    DateTime ModifiedAt,
    DateTime CreatedAt = default);

public sealed record ShellCopyConflictGroup(
    string DestinationPath,
    IReadOnlyList<ShellCopyCandidate> Sources,
    ShellExistingDestination? ExistingDestination)
{
    public string FileName => Path.GetFileName(DestinationPath);
    public bool RequiresOverwriteSelection => Sources.Count > 1;
}

public sealed record ShellCopyConflictAnalysis(
    IReadOnlyList<ShellCopyConflictGroup> Groups,
    IReadOnlySet<string> PlannedDestinationPaths);

public sealed class ShellCopyConflictPlanner
{
    private const int DestinationIndexThreshold = 512;

    public Task<ShellCopyConflictAnalysis> AnalyzeAsync(
        IEnumerable<ShellCopyRequest> requests,
        IProgress<ShellConflictScanProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => FindConflicts(requests, progress, cancellationToken), cancellationToken);

    public IReadOnlyList<ShellCopyConflictGroup> FindConflicts(IEnumerable<ShellCopyRequest> requests) =>
        FindConflicts(requests, null, CancellationToken.None).Groups;

    private static ShellCopyConflictAnalysis FindConflicts(
        IEnumerable<ShellCopyRequest> requests,
        IProgress<ShellConflictScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var requestArray = requests.ToArray();
        var candidates = new ConcurrentBag<ShellCopyCandidate>();
        var collected = 0;
        progress?.Report(new ShellConflictScanProgress(ShellConflictScanStage.CollectingFiles, 0, 0));

        Parallel.ForEach(
            requestArray.Select((request, index) => (Request: request, Index: index)),
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 4),
            },
            item =>
            {
                var localIndex = 0L;
                foreach (var sourcePath in EnumerateSourceFiles(item.Request, cancellationToken))
                {
                    var relativePath = Path.GetRelativePath(item.Request.SourceDirectory, sourcePath);
                    candidates.Add(new ShellCopyCandidate(
                        sourcePath,
                        Path.GetFullPath(Path.Combine(item.Request.DestinationDirectory, relativePath)),
                        0,
                        default,
                        ((long)item.Index << 32) | (uint)localIndex++));
                    var current = Interlocked.Increment(ref collected);
                    if (current == 1 || current % 100 == 0)
                    {
                        progress?.Report(new ShellConflictScanProgress(ShellConflictScanStage.CollectingFiles, current, 0));
                    }
                }
            });

        var destinationGroups = candidates
            .GroupBy(candidate => candidate.DestinationPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var destinationIndex = destinationGroups.Length >= DestinationIndexThreshold
            ? BuildDestinationIndex(requestArray, progress, cancellationToken)
            : null;
        var conflicts = new ConcurrentBag<ShellCopyConflictGroup>();
        var compared = 0;
        progress?.Report(new ShellConflictScanProgress(
            ShellConflictScanStage.ComparingDestinations,
            0,
            destinationGroups.Length));

        Parallel.ForEach(
            destinationGroups,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount * 2, 2, 8),
            },
            group =>
            {
                var sourceCandidates = group.OrderBy(candidate => candidate.Sequence).ToArray();
                ShellExistingDestination? existing = null;
                var existingType = destinationIndex is null
                    ? File.Exists(group.Key) ? DestinationEntryType.File
                        : Directory.Exists(group.Key) ? DestinationEntryType.Directory
                        : DestinationEntryType.None
                    : destinationIndex.GetValueOrDefault(group.Key);
                if (existingType == DestinationEntryType.File)
                {
                    var info = new FileInfo(group.Key);
                    existing = new ShellExistingDestination(
                        info.FullName,
                        info.Length,
                        info.LastWriteTime,
                        info.CreationTime);
                }

                if (sourceCandidates.Length > 1 || existing is not null || existingType == DestinationEntryType.Directory)
                {
                    conflicts.Add(new ShellCopyConflictGroup(
                        group.Key,
                        sourceCandidates.Select(ReadSourceMetadata).ToArray(),
                        existing));
                }

                var current = Interlocked.Increment(ref compared);
                if (current == destinationGroups.Length || current % 100 == 0)
                {
                    progress?.Report(new ShellConflictScanProgress(
                        ShellConflictScanStage.ComparingDestinations,
                        current,
                        destinationGroups.Length));
                }
            });

        var orderedConflicts = conflicts
            .OrderBy(group => group.FileName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(group => group.DestinationPath, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return new ShellCopyConflictAnalysis(
            orderedConflicts,
            candidates.Select(candidate => candidate.DestinationPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateSourceFiles(
        ShellCopyRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FileSpecifications.Count > 0)
        {
            foreach (var specification in request.FileSpecifications)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.Combine(request.SourceDirectory, specification);
                if (File.Exists(path))
                {
                    yield return Path.GetFullPath(path);
                }
            }

            yield break;
        }

        var pending = new Stack<string>();
        pending.Push(request.SourceDirectory);
        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<string> files;
            IEnumerable<string> directories;
            try
            {
                files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
                directories = request.RecurseSubdirectories
                    ? Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly).ToArray()
                    : [];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return Path.GetFullPath(file);
            }

            foreach (var child in directories)
            {
                try
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(child);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Robocopy reports inaccessible paths during the actual operation.
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, DestinationEntryType> BuildDestinationIndex(
        IReadOnlyList<ShellCopyRequest> requests,
        IProgress<ShellConflictScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var roots = requests
            .Select(request => Path.GetFullPath(request.DestinationDirectory))
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        roots = roots.Where(root => !roots.Any(candidate =>
                !string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase)
                && IsDescendantPath(root, candidate)))
            .ToArray();

        var entries = new ConcurrentDictionary<string, DestinationEntryType>(StringComparer.OrdinalIgnoreCase);
        var scanned = 0;
        progress?.Report(new ShellConflictScanProgress(ShellConflictScanStage.ScanningDestinations, 0, 0));
        Parallel.ForEach(
            roots,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 4),
            },
            root =>
            {
                foreach (var entry in EnumerateDestinationEntries(root, cancellationToken))
                {
                    entries[entry.Path] = entry.Type;
                    var current = Interlocked.Increment(ref scanned);
                    if (current == 1 || current % 250 == 0)
                    {
                        progress?.Report(new ShellConflictScanProgress(
                            ShellConflictScanStage.ScanningDestinations,
                            current,
                            0));
                    }
                }
            });
        return entries;
    }

    private static IEnumerable<(string Path, DestinationEntryType Type)> EnumerateDestinationEntries(
        string root,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<string> files;
            IEnumerable<string> directories;
            try
            {
                files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
                directories = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return (Path.GetFullPath(file), DestinationEntryType.File);
            }

            foreach (var child in directories)
            {
                yield return (Path.GetFullPath(child), DestinationEntryType.Directory);
                try
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(child);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // The actual copy reports paths that become inaccessible during review.
                }
            }
        }
    }

    private static bool IsDescendantPath(string path, string possibleAncestor)
    {
        var ancestor = Path.TrimEndingDirectorySeparator(possibleAncestor) + Path.DirectorySeparatorChar;
        return path.StartsWith(ancestor, StringComparison.OrdinalIgnoreCase);
    }

    private static ShellCopyCandidate ReadSourceMetadata(ShellCopyCandidate candidate)
    {
        try
        {
            var info = new FileInfo(candidate.SourcePath);
            return candidate with
            {
                Size = info.Length,
                ModifiedAt = info.LastWriteTime,
                CreatedAt = info.CreationTime,
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return candidate;
        }
    }

    private enum DestinationEntryType
    {
        None,
        File,
        Directory,
    }
}
