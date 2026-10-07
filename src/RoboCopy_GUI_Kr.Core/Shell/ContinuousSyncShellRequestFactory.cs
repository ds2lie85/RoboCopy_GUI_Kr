using System.IO;
using RoboCopy_GUI_Kr.Core.Exclusions;

namespace RoboCopy_GUI_Kr.Core.Shell;

public static class ContinuousSyncShellRequestFactory
{
    public static IReadOnlyList<ShellCopyRequest> Create(
        string destinationDirectory,
        IEnumerable<string> selectedPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        var entries = selectedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (entries.Length == 0)
        {
            return [];
        }

        var commonParent = entries
            .Select(GetParentDirectory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (commonParent.Length == 1)
        {
            return [CreateForParent(commonParent[0], Path.GetFullPath(destinationDirectory), entries)];
        }

        return entries
            .Where(Directory.Exists)
            .Select(path => new ShellCopyRequest(
                path,
                Path.GetFullPath(destinationDirectory),
                [],
                RecurseSubdirectories: true))
            .ToArray();
    }

    private static ShellCopyRequest CreateForParent(
        string sourceDirectory,
        string destinationDirectory,
        IReadOnlyCollection<string> selectedPaths)
    {
        var selectionState = ExclusionSelectionState.FromIncludedChildren(sourceDirectory, selectedPaths);

        return new ShellCopyRequest(
            sourceDirectory,
            destinationDirectory,
            [],
            RecurseSubdirectories: true)
        {
            ExcludedDirectories = selectionState.Directories,
            ExcludedFiles = selectionState.Files,
        };
    }

    private static string GetParentDirectory(string path)
    {
        var normalized = Directory.Exists(path) ? Path.TrimEndingDirectorySeparator(path) : path;
        return Path.GetDirectoryName(normalized)
            ?? throw new ArgumentException($"A selected item has no parent directory: {path}", nameof(path));
    }
}
