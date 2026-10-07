using System.IO.Enumeration;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Moves;

public sealed record MoveVerificationFailure(
    string SourcePath,
    string DestinationPath,
    int ErrorCode,
    string Reason);

public sealed record VerifiedMoveProgress(int CompletedFiles, int TotalFiles);

public sealed record VerifiedMoveResult(
    int DeletedFiles,
    int DeletedDirectories,
    IReadOnlyList<MoveVerificationFailure> Failures)
{
    public bool IsSuccess => Failures.Count == 0;
}

public sealed class VerifiedMoveService
{
    private const int BufferSize = 1024 * 1024;

    public async Task<VerifiedMoveResult> ExecuteAsync(
        RobocopyJobOptions options,
        IProgress<VerifiedMoveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sourceRoot = Path.GetFullPath(options.SourceDirectory);
        var destinationRoot = Path.GetFullPath(options.DestinationDirectory);
        var excludedDirectories = options.ExcludedDirectories
            .Where(Path.IsPathFullyQualified)
            .Select(Path.GetFullPath)
            .ToArray();
        var sourceFiles = EnumerateSourceFiles(sourceRoot, options, excludedDirectories, cancellationToken).ToArray();
        var failures = new List<MoveVerificationFailure>();
        var deletedFiles = 0;
        var lastReportedPercent = -1;

        for (var index = 0; index < sourceFiles.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = sourceFiles[index];
            var destinationPath = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, sourcePath));
            try
            {
                if (!File.Exists(destinationPath))
                {
                    throw new IOException("The destination file does not exist.");
                }

                if (!await ContentsEqualAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false))
                {
                    throw new IOException("The copied file content does not match the source.");
                }

                File.Delete(sourcePath);
                deletedFiles++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(new MoveVerificationFailure(
                    sourcePath,
                    destinationPath,
                    GetWin32ErrorCode(exception),
                    exception.Message));
            }

            ReportProgress(progress, index + 1, sourceFiles.Length, ref lastReportedPercent);
        }

        var deletedDirectories = DeleteVerifiedEmptyDirectories(
            sourceRoot,
            destinationRoot,
            excludedDirectories,
            options.IncludeEmptyDirectories,
            cancellationToken);
        if (Directory.Exists(sourceRoot)
            && Directory.Exists(destinationRoot)
            && !Directory.EnumerateFileSystemEntries(sourceRoot).Any())
        {
            Directory.Delete(sourceRoot);
            deletedDirectories++;
        }

        return new VerifiedMoveResult(deletedFiles, deletedDirectories, failures);
    }

    public int DeleteVerifiedEmptyAncestors(
        string sourceDirectory,
        string sourceRoot,
        string destinationRoot,
        CancellationToken cancellationToken = default)
    {
        var fullSourceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot));
        var fullDestinationRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationRoot));
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory));
        if (!IsSamePathOrDescendant(current, fullSourceRoot))
        {
            throw new ArgumentException("The cleanup directory must be inside the move source root.", nameof(sourceDirectory));
        }

        var deleted = 0;
        while (!string.Equals(current, fullSourceRoot, StringComparison.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(current))
            {
                if (Directory.EnumerateFileSystemEntries(current).Any())
                {
                    break;
                }

                var relativePath = Path.GetRelativePath(fullSourceRoot, current);
                var destinationDirectory = Path.Combine(fullDestinationRoot, relativePath);
                if (!Directory.Exists(destinationDirectory))
                {
                    break;
                }

                Directory.Delete(current);
                deleted++;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent)
                || !IsSamePathOrDescendant(parent, fullSourceRoot))
            {
                break;
            }

            current = Path.TrimEndingDirectorySeparator(parent);
        }

        return deleted;
    }

    private static bool IsSamePathOrDescendant(string path, string root) =>
        string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static int GetWin32ErrorCode(Exception exception)
    {
        if (exception is UnauthorizedAccessException)
        {
            return 5;
        }

        var code = exception.HResult & 0xFFFF;
        return code == 0 ? 1 : code;
    }

    private static void ReportProgress(
        IProgress<VerifiedMoveProgress>? progress,
        int completed,
        int total,
        ref int lastReportedPercent)
    {
        if (progress is null)
        {
            return;
        }

        var percent = total == 0 ? 100 : completed * 100 / total;
        if (percent == lastReportedPercent && completed != total)
        {
            return;
        }

        lastReportedPercent = percent;
        progress.Report(new VerifiedMoveProgress(completed, total));
    }

    private static IEnumerable<string> EnumerateSourceFiles(
        string sourceRoot,
        RobocopyJobOptions options,
        IReadOnlyList<string> excludedDirectories,
        CancellationToken cancellationToken)
    {
        var directories = new Stack<string>();
        directories.Push(sourceRoot);
        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = directories.Pop();
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0
                    || (options.ExcludeZeroByteFiles && info.Length == 0)
                    || IsExcludedFile(file, options.ExcludedFiles)
                    || !MatchesFileSpecifications(info.Name, options.FileSpecifications))
                {
                    continue;
                }

                yield return file;
            }

            if (!options.RecurseSubdirectories)
            {
                continue;
            }

            foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                var attributes = File.GetAttributes(child);
                if ((attributes & FileAttributes.ReparsePoint) == 0
                    && !IsPathExcluded(child, excludedDirectories))
                {
                    directories.Push(child);
                }
            }
        }
    }

    private static bool IsExcludedFile(string path, IEnumerable<string> exclusions)
    {
        foreach (var exclusion in exclusions.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (Path.IsPathFullyQualified(exclusion))
            {
                if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(exclusion), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (FileSystemName.MatchesSimpleExpression(exclusion, Path.GetFileName(path), ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesFileSpecifications(string fileName, IReadOnlyList<string> specifications) =>
        specifications.Count == 0
        || specifications.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true));

    private static bool IsPathExcluded(string path, IReadOnlyList<string> excludedDirectories)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return excludedDirectories.Any(excluded =>
        {
            var fullExcluded = Path.TrimEndingDirectorySeparator(excluded);
            return fullPath.Equals(fullExcluded, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullExcluded + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static async Task<bool> ContentsEqualAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var sourceInfo = new FileInfo(sourcePath);
        var destinationInfo = new FileInfo(destinationPath);
        if (sourceInfo.Length != destinationInfo.Length)
        {
            return false;
        }

        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var sourceBuffer = new byte[BufferSize];
        var destinationBuffer = new byte[BufferSize];
        while (true)
        {
            var sourceRead = await source.ReadAsync(sourceBuffer, cancellationToken).ConfigureAwait(false);
            var destinationRead = await destination.ReadAsync(destinationBuffer, cancellationToken).ConfigureAwait(false);
            if (sourceRead != destinationRead)
            {
                return false;
            }

            if (sourceRead == 0)
            {
                return true;
            }

            if (!sourceBuffer.AsSpan(0, sourceRead).SequenceEqual(destinationBuffer.AsSpan(0, destinationRead)))
            {
                return false;
            }
        }
    }

    private static int DeleteVerifiedEmptyDirectories(
        string sourceRoot,
        string destinationRoot,
        IReadOnlyList<string> excludedDirectories,
        bool includeEmptyDirectories,
        CancellationToken cancellationToken)
    {
        var deleted = 0;
        var pending = new Stack<string>();
        var discovered = new List<string>();
        pending.Push(sourceRoot);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parent = pending.Pop();
            foreach (var directory in Directory.EnumerateDirectories(parent, "*", SearchOption.TopDirectoryOnly))
            {
                if (IsPathExcluded(directory, excludedDirectories)
                    || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                discovered.Add(directory);
                pending.Push(directory);
            }
        }

        var directories = discovered
            .OrderByDescending(path => path.Length)
            .ToArray();
        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsPathExcluded(directory, excludedDirectories)
                || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0
                || Directory.EnumerateFileSystemEntries(directory).Any())
            {
                continue;
            }

            var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory));
            if (!Directory.Exists(destination) && !includeEmptyDirectories)
            {
                continue;
            }

            if (Directory.Exists(destination))
            {
                Directory.Delete(directory);
                deleted++;
            }
        }

        return deleted;
    }
}
