namespace RoboCopy_GUI_Kr.Core.Sync;

public static class OneWaySyncSelection
{
    public static bool IsDescendant(string candidateRelativePath, string directoryRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateRelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryRelativePath);

        var candidate = Normalize(candidateRelativePath);
        var directory = Normalize(directoryRelativePath);
        return candidate.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static bool AreAllDescendantsSelected(
        IEnumerable<(string RelativePath, bool Selected)> items,
        string directoryRelativePath)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryRelativePath);

        var descendants = items
            .Where(item => IsDescendant(item.RelativePath, directoryRelativePath))
            .ToArray();
        return descendants.Length > 0 && descendants.All(item => item.Selected);
    }

    private static string Normalize(string path) => path
        .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
        .Trim(Path.DirectorySeparatorChar);
}
