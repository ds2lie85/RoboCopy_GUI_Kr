using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Shell;

public sealed record ShellCopyRequest(
    string SourceDirectory,
    string DestinationDirectory,
    IReadOnlyList<string> FileSpecifications,
    bool RecurseSubdirectories)
{
    public JobOperationMode OperationMode { get; init; } = JobOperationMode.Copy;

    public bool StartImmediately { get; init; }

    public IReadOnlyList<string> ExcludedDirectories { get; init; } = [];

    public IReadOnlyList<string> ExcludedFiles { get; init; } = [];

    public string? DestinationFileName { get; init; }

    public bool OverwriteDestination { get; init; }

    public bool SkipExistingFiles { get; init; }
}

public static class ShellCopyRequestFactory
{
    private const int MaximumFileSpecificationCharacters = 12_000;
    private const int MaximumFileSpecificationsPerRequest = 128;

    public static IReadOnlyList<ShellCopyRequest> Create(string destinationDirectory, IEnumerable<string> selectedPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var distinctPaths = selectedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var entries = distinctPaths
            .Select(path => File.Exists(path)
                ? new SelectionEntry(path, IsDirectory: false)
                : Directory.Exists(path) ? new SelectionEntry(path, IsDirectory: true) : null)
            .Where(entry => entry is not null)
            .Cast<SelectionEntry>()
            .ToArray();
        var selectedDirectories = entries
            .Where(entry => entry.IsDirectory)
            .Select(entry => entry.Path)
            .ToArray();
        entries = entries.Where(entry => !selectedDirectories.Any(directory =>
                !string.Equals(entry.Path, directory, StringComparison.OrdinalIgnoreCase)
                && IsDescendantOf(entry.Path, directory)))
            .ToArray();
        var requests = new List<ShellCopyRequest>();

        foreach (var group in entries.GroupBy(
                     entry => GetParentDirectory(entry.Path, entry.IsDirectory),
                     StringComparer.OrdinalIgnoreCase))
        {
            var selectedFiles = group
                .Where(entry => !entry.IsDirectory)
                .Select(entry => Path.GetFileName(entry.Path)!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var fileBatch in BatchFileSpecifications(selectedFiles))
            {
                requests.Add(new ShellCopyRequest(
                    group.Key,
                    destinationDirectory,
                    fileBatch,
                    RecurseSubdirectories: false));
            }

            // Robocopy has no positive "include these top-level directories only" switch.
            // Listing every unselected sibling through /XD and /XF can exceed Windows'
            // process command-line limit in folders containing many entries. Copy each
            // selected directory from its own root instead, preserving Explorer's
            // destination\selected-folder layout without a potentially huge exclusion list.
            foreach (var selectedDirectory in group.Where(entry => entry.IsDirectory).Select(entry => entry.Path))
            {
                requests.Add(new ShellCopyRequest(
                    selectedDirectory,
                    Path.Combine(destinationDirectory, Path.GetFileName(Path.TrimEndingDirectorySeparator(selectedDirectory))),
                    Array.Empty<string>(),
                    RecurseSubdirectories: true));
            }
        }

        return requests;
    }

    private static IEnumerable<IReadOnlyList<string>> BatchFileSpecifications(IEnumerable<string> specifications)
    {
        var batch = new List<string>();
        var characterCount = 0;
        foreach (var specification in specifications)
        {
            var estimatedCharacters = specification.Length + 3;
            if (batch.Count > 0
                && (batch.Count >= MaximumFileSpecificationsPerRequest
                    || characterCount + estimatedCharacters > MaximumFileSpecificationCharacters))
            {
                yield return batch.ToArray();
                batch.Clear();
                characterCount = 0;
            }

            batch.Add(specification);
            characterCount += estimatedCharacters;
        }

        if (batch.Count > 0)
        {
            yield return batch.ToArray();
        }
    }

    private static string GetParentDirectory(string path, bool isDirectory)
    {
        var normalized = isDirectory ? Path.TrimEndingDirectorySeparator(path) : path;
        return Path.GetDirectoryName(normalized)
            ?? throw new ArgumentException($"A selected item has no parent directory: {path}", nameof(path));
    }

    private static bool IsDescendantOf(string path, string possibleAncestor)
    {
        var ancestor = Path.TrimEndingDirectorySeparator(Path.GetFullPath(possibleAncestor)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(ancestor, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SelectionEntry(string Path, bool IsDirectory);
}
