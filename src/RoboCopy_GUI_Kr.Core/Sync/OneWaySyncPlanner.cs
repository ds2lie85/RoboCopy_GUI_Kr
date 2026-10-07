using System.IO.Enumeration;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Sync;

public sealed record OneWaySyncExtraItem(
    string DestinationPath,
    string RelativePath,
    bool IsDirectory,
    long Size);

public sealed class OneWaySyncPlanner
{
    public IReadOnlyList<OneWaySyncExtraItem> FindDestinationOnlyItems(
        RobocopyJobOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sourceRoot = Path.GetFullPath(options.SourceDirectory);
        var destinationRoot = Path.GetFullPath(options.DestinationDirectory);
        if (!Directory.Exists(destinationRoot))
        {
            return [];
        }

        var extras = new List<OneWaySyncExtraItem>();
        var destinationFiles = new List<string>();
        var destinationDirectories = new List<string>();
        var pending = new Stack<string>();
        pending.Push(destinationRoot);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            destinationFiles.AddRange(Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly));
            foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                destinationDirectories.Add(child);
                pending.Push(child);
            }
        }

        foreach (var destinationPath in destinationFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(destinationRoot, destinationPath);
            var sourcePath = Path.Combine(sourceRoot, relativePath);
            var info = new FileInfo(destinationPath);
            if (File.Exists(sourcePath)
                || IsExcludedDirectory(Path.GetDirectoryName(sourcePath)!, sourceRoot, options.ExcludedDirectories)
                || IsExcludedFile(sourcePath, options.ExcludedFiles)
                || !MatchesFileSpecifications(info.Name, options.FileSpecifications)
                || (options.ExcludeZeroByteFiles && info.Length == 0)
                || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            extras.Add(new OneWaySyncExtraItem(destinationPath, relativePath, false, info.Length));
        }

        foreach (var destinationDirectory in destinationDirectories.OrderByDescending(path => path.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(destinationRoot, destinationDirectory);
            var sourcePath = Path.Combine(sourceRoot, relativePath);
            if (!Directory.Exists(sourcePath)
                && !IsExcludedDirectory(sourcePath, sourceRoot, options.ExcludedDirectories)
                && (File.GetAttributes(destinationDirectory) & FileAttributes.ReparsePoint) == 0)
            {
                extras.Add(new OneWaySyncExtraItem(destinationDirectory, relativePath, true, 0));
            }
        }

        return extras.OrderBy(item => item.RelativePath, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static bool IsExcludedDirectory(string path, string sourceRoot, IEnumerable<string> exclusions)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        foreach (var exclusion in exclusions.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (Path.IsPathFullyQualified(exclusion))
            {
                var fullExcluded = Path.TrimEndingDirectorySeparator(Path.GetFullPath(exclusion));
                if (fullPath.Equals(fullExcluded, StringComparison.OrdinalIgnoreCase)
                    || fullPath.StartsWith(fullExcluded + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                var relative = Path.GetRelativePath(sourceRoot, fullPath);
                if (relative.Split(Path.DirectorySeparatorChar).Any(segment =>
                        FileSystemName.MatchesSimpleExpression(exclusion, segment, ignoreCase: true)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsExcludedFile(string path, IEnumerable<string> exclusions) => exclusions
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Any(exclusion => Path.IsPathFullyQualified(exclusion)
            ? string.Equals(Path.GetFullPath(path), Path.GetFullPath(exclusion), StringComparison.OrdinalIgnoreCase)
            : FileSystemName.MatchesSimpleExpression(exclusion, Path.GetFileName(path), ignoreCase: true));

    private static bool MatchesFileSpecifications(string fileName, IReadOnlyList<string> specifications) =>
        specifications.Count == 0
        || specifications.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true));
}
