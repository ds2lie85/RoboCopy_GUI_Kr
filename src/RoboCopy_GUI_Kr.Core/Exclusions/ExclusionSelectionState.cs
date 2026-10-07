namespace RoboCopy_GUI_Kr.Core.Exclusions;

public sealed class ExclusionSelectionState
{
    private readonly HashSet<string> _directories;
    private readonly HashSet<string> _files;

    public ExclusionSelectionState(IEnumerable<string> directories, IEnumerable<string> files)
    {
        _directories = new HashSet<string>(directories.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        _files = new HashSet<string>(files.Select(Normalize), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> Directories => _directories.OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public IReadOnlyList<string> Files => _files.OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public static ExclusionSelectionState FromIncludedChildren(
        string sourceDirectory,
        IEnumerable<string> includedPaths)
    {
        var included = includedPaths.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var state = new ExclusionSelectionState([], []);
        foreach (var child in Directory.EnumerateFileSystemEntries(sourceDirectory))
        {
            var normalized = Normalize(child);
            if (!included.Contains(normalized))
            {
                state.SetIncluded(normalized, Directory.Exists(normalized), included: false);
            }
        }

        return state;
    }

    public bool HasExcludedDirectoryAncestor(string path)
    {
        var normalized = Normalize(path);
        return _directories.Any(excluded => IsDescendant(normalized, excluded));
    }

    public bool? GetState(string path, bool isDirectory)
    {
        var normalized = Normalize(path);
        if (_directories.Any(excluded => IsSameOrAncestor(excluded, normalized)))
        {
            return false;
        }

        if (!isDirectory)
        {
            return !_files.Contains(normalized);
        }

        return _directories.Any(excluded => IsDescendant(excluded, normalized))
            || _files.Any(excluded => IsDescendant(excluded, normalized))
            ? null
            : true;
    }

    public void SetIncluded(string path, bool isDirectory, bool included)
    {
        var normalized = Normalize(path);
        RemoveSameOrDescendants(_directories, normalized);
        RemoveSameOrDescendants(_files, normalized);

        if (included)
        {
            return;
        }

        if (isDirectory)
        {
            _directories.Add(normalized);
        }
        else
        {
            _files.Add(normalized);
        }
    }

    private static void RemoveSameOrDescendants(HashSet<string> values, string parent)
    {
        values.RemoveWhere(value => string.Equals(value, parent, StringComparison.OrdinalIgnoreCase) || IsDescendant(value, parent));
    }

    private static bool IsSameOrAncestor(string candidate, string path)
    {
        return string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase) || IsDescendant(path, candidate);
    }

    private static bool IsDescendant(string candidate, string parent)
    {
        return candidate.StartsWith(EnsureTrailingSeparator(parent), StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
    }

    private static string Normalize(string path)
    {
        var fullPath = Path.GetFullPath(path.Trim());
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
