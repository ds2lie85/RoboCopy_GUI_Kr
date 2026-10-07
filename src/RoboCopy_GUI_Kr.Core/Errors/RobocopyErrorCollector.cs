using System.Text.RegularExpressions;

namespace RoboCopy_GUI_Kr.Core.Errors;

public sealed partial class RobocopyErrorCollector
{
    private readonly string _sourceRoot;
    private readonly string _destinationRoot;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, RobocopyErrorItem> _items = new(StringComparer.OrdinalIgnoreCase);

    public RobocopyErrorCollector(string sourceRoot, string destinationRoot)
    {
        _sourceRoot = NormalizeRoot(sourceRoot);
        _destinationRoot = NormalizeRoot(destinationRoot);
    }

    public IReadOnlyList<RobocopyErrorItem> Items
    {
        get
        {
            lock (_syncRoot)
            {
                return _items.Values.ToArray();
            }
        }
    }

    public bool ProcessLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = ErrorLineRegex().Match(line);
        if (!match.Success || !int.TryParse(match.Groups["code"].Value, out var code))
        {
            return false;
        }

        var path = TrimPath(match.Groups["path"].Value);
        var (sourcePath, destinationPath) = ResolvePaths(path);
        var key = $"{code}|{sourcePath}|{destinationPath}";
        lock (_syncRoot)
        {
            if (_items.TryGetValue(key, out var existing))
            {
                _items[key] = existing with { Occurrences = existing.Occurrences + 1, Message = line.Trim() };
            }
            else
            {
                _items[key] = new RobocopyErrorItem(
                    code,
                    RobocopyErrorClassifier.Classify(code),
                    sourcePath,
                    destinationPath,
                    line.Trim());
            }
        }

        return true;
    }

    private (string SourcePath, string DestinationPath) ResolvePaths(string path)
    {
        if (IsUnderRoot(path, _destinationRoot))
        {
            var relative = Path.GetRelativePath(_destinationRoot, path);
            return (Path.Combine(_sourceRoot, relative), path);
        }

        if (IsUnderRoot(path, _sourceRoot))
        {
            var relative = Path.GetRelativePath(_sourceRoot, path);
            return (path, Path.Combine(_destinationRoot, relative));
        }

        return (path, string.Empty);
    }

    private static bool IsUnderRoot(string path, string root)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            return false;
        }

        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoot(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;

    private static string TrimPath(string value) => value.Trim().TrimEnd('.', ' ', '\t');

    [GeneratedRegex(@"(?:ERROR|\uC624\uB958)\s+(?<code>\d+)\s+\([^)]*\)\s+[^\r\n]*?\s(?<path>(?:[A-Za-z]:\\|\\\\).+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ErrorLineRegex();
}
