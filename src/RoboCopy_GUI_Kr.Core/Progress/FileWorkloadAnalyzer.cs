using System.IO.Enumeration;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Progress;

public sealed class FileWorkloadAnalyzer
{
    public Task<FileWorkloadSnapshot> AnalyzeAsync(RobocopyJobOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => Analyze(options, cancellationToken), cancellationToken);
    }

    private static FileWorkloadSnapshot Analyze(RobocopyJobOptions options, CancellationToken cancellationToken)
    {
        var sourceRoot = Path.GetFullPath(options.SourceDirectory);
        var excludedDirectories = options.ExcludedDirectories
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeExclusion(value, sourceRoot))
            .ToArray();
        var excludedFiles = options.ExcludedFiles
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        var fileSpecifications = options.FileSpecifications
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        var fileSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingDirectories = new Stack<DirectoryInfo>();
        pendingDirectories.Push(new DirectoryInfo(sourceRoot));

        var enumerationOptions = new EnumerationOptions
        {
            AttributesToSkip = 0,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
        };

        while (pendingDirectories.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsExcludedDirectory(directory.FullName, directory.Name, excludedDirectories))
            {
                continue;
            }

            var directoryIdentity = ResolveDirectoryIdentity(directory, options.SymbolicLinkPolicy);
            if (directoryIdentity is null || !visitedDirectories.Add(directoryIdentity))
            {
                continue;
            }

            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = directory.EnumerateFileSystemInfos("*", enumerationOptions);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            try
            {
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry is DirectoryInfo childDirectory)
                    {
                        if (!options.RecurseSubdirectories)
                        {
                            continue;
                        }

                        if (IsReparsePoint(childDirectory)
                            && options.SymbolicLinkPolicy != SymbolicLinkPolicy.FollowTarget)
                        {
                            continue;
                        }

                        pendingDirectories.Push(childDirectory);
                    }
                    else if (entry is FileInfo file
                             && !IsExcludedFile(file, excludedFiles)
                             && IsIncludedFile(file, fileSpecifications)
                             && (!IsReparsePoint(file) || options.SymbolicLinkPolicy != SymbolicLinkPolicy.Exclude))
                    {
                        var fileLength = GetFileLength(file, options.SymbolicLinkPolicy);
                        if (!options.ExcludeZeroByteFiles || fileLength > 0)
                        {
                            fileSizes[file.FullName] = fileLength;
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Robocopy will report inaccessible entries during the actual run.
            }
        }

        return new FileWorkloadSnapshot(fileSizes);
    }

    private static string NormalizeExclusion(string exclusion, string sourceRoot)
    {
        var trimmed = exclusion.Trim();
        return Path.IsPathFullyQualified(trimmed)
            ? Path.GetFullPath(trimmed)
            : trimmed.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsExcludedDirectory(string fullPath, string name, IReadOnlyList<string> exclusions)
    {
        foreach (var exclusion in exclusions)
        {
            if (Path.IsPathFullyQualified(exclusion))
            {
                if (string.Equals(fullPath, exclusion, StringComparison.OrdinalIgnoreCase)
                    || fullPath.StartsWith(
                        Path.TrimEndingDirectorySeparator(exclusion) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (FileSystemName.MatchesSimpleExpression(exclusion, name, ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExcludedFile(FileInfo file, IReadOnlyList<string> exclusions)
    {
        foreach (var exclusion in exclusions)
        {
            if (Path.IsPathFullyQualified(exclusion))
            {
                if (string.Equals(file.FullName, Path.GetFullPath(exclusion), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (FileSystemName.MatchesSimpleExpression(exclusion, file.Name, ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIncludedFile(FileInfo file, IReadOnlyList<string> specifications)
    {
        return specifications.Count == 0
            || specifications.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, file.Name, ignoreCase: true));
    }

    private static string? ResolveDirectoryIdentity(DirectoryInfo directory, SymbolicLinkPolicy linkPolicy)
    {
        if (!IsReparsePoint(directory))
        {
            return Path.GetFullPath(directory.FullName);
        }

        if (linkPolicy != SymbolicLinkPolicy.FollowTarget)
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static long GetFileLength(FileInfo file, SymbolicLinkPolicy linkPolicy)
    {
        if (IsReparsePoint(file) && linkPolicy == SymbolicLinkPolicy.CopyLinkItself)
        {
            return 0;
        }

        try
        {
            return file.Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static bool IsReparsePoint(FileSystemInfo info)
    {
        try
        {
            return info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
