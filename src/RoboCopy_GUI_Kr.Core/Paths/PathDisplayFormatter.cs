namespace RoboCopy_GUI_Kr.Core.Paths;

public static class PathDisplayFormatter
{
    public static string Compact(string? path, int maximumCharacters)
    {
        if (string.IsNullOrEmpty(path) || maximumCharacters <= 0)
        {
            return string.Empty;
        }

        if (path.Length <= maximumCharacters)
        {
            return path;
        }

        var root = Path.GetPathRoot(path) ?? string.Empty;
        var remainder = path[root.Length..].Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parts = remainder.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return TrimFromLeft(path, maximumCharacters);
        }

        var separator = Path.DirectorySeparatorChar.ToString();
        var firstDirectory = parts.Length > 1 ? parts[0] : string.Empty;
        var prefix = root + (firstDirectory.Length > 0 ? firstDirectory + separator : string.Empty);
        var suffixStart = parts.Length - 1;
        var best = Build(prefix, parts, suffixStart, separator);
        if (best.Length > maximumCharacters)
        {
            prefix = root;
            best = Build(prefix, parts, suffixStart, separator);
        }

        while (suffixStart > (firstDirectory.Length > 0 ? 1 : 0))
        {
            var candidate = Build(prefix, parts, suffixStart - 1, separator);
            if (candidate.Length > maximumCharacters)
            {
                break;
            }

            suffixStart--;
            best = candidate;
        }

        return best.Length <= maximumCharacters
            ? best
            : TrimFromLeft(best, maximumCharacters);
    }

    private static string Build(string prefix, IReadOnlyList<string> parts, int suffixStart, string separator) =>
        prefix + "..." + separator + string.Join(separator, parts.Skip(suffixStart));

    private static string TrimFromLeft(string value, int maximumCharacters)
    {
        if (value.Length <= maximumCharacters)
        {
            return value;
        }

        if (maximumCharacters <= 3)
        {
            return value[^maximumCharacters..];
        }

        return "..." + value[^(maximumCharacters - 3)..];
    }
}
