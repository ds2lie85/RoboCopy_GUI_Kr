namespace RoboCopy_GUI_Kr.Core.Paths;

public sealed record ShellSelectionTarget(string ItemPath, string FolderPath, bool CanSelectItem);

public static class ShellSelectionTargetResolver
{
    public static ShellSelectionTarget Resolve(string requestedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedPath);

        var fullPath = Path.GetFullPath(requestedPath.Trim());
        if (File.Exists(fullPath))
        {
            return new ShellSelectionTarget(
                fullPath,
                Path.GetDirectoryName(fullPath) ?? throw new DirectoryNotFoundException(fullPath),
                CanSelectItem: true);
        }

        if (Directory.Exists(fullPath))
        {
            var parent = Directory.GetParent(fullPath)?.FullName;
            return string.IsNullOrWhiteSpace(parent)
                ? new ShellSelectionTarget(fullPath, fullPath, CanSelectItem: false)
                : new ShellSelectionTarget(fullPath, parent, CanSelectItem: true);
        }

        var existingFolder = FindNearestExistingFolder(Path.GetDirectoryName(fullPath));
        if (existingFolder is null)
        {
            throw new DirectoryNotFoundException(fullPath);
        }

        return new ShellSelectionTarget(fullPath, existingFolder, CanSelectItem: false);
    }

    private static string? FindNearestExistingFolder(string? candidate)
    {
        while (!string.IsNullOrWhiteSpace(candidate))
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.GetDirectoryName(candidate);
        }

        return null;
    }
}
