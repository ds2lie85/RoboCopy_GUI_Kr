using System.Text.RegularExpressions;

namespace RoboCopy_GUI_Kr.Core.Robocopy;

public sealed record RobocopyFileProgress(
    string FileName,
    double Percent,
    string SourcePath,
    string DestinationPath,
    string RawLine);

/// <summary>
/// /FP가 포함된 Robocopy 출력의 전체 원본 경로와 뒤따르는 진행률 줄을 결합합니다.
/// </summary>
public sealed partial class RobocopyFileProgressTracker
{
    private readonly object _gate = new();
    private readonly string _sourceDirectory;
    private readonly string _destinationDirectory;
    private readonly bool _listedFilesAreComplete;
    private string? _currentSourcePath;

    public RobocopyFileProgressTracker(
        string sourceDirectory,
        string destinationDirectory,
        bool listedFilesAreComplete = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        _sourceDirectory = NormalizeDirectory(sourceDirectory);
        _destinationDirectory = Path.GetFullPath(destinationDirectory.Trim());
        _listedFilesAreComplete = listedFilesAreComplete;
    }

    [GeneratedRegex(@"(?<path>(?:[A-Za-z]:\\|\\\\)[^\r\n]+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex FullPathAtEndRegex();

    public bool TryProcessLine(string? line, out RobocopyFileProgress? progress)
    {
        progress = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        lock (_gate)
        {
            var sourceFileDiscovered = false;
            var pathMatch = FullPathAtEndRegex().Match(line);
            if (pathMatch.Success)
            {
                var candidate = pathMatch.Groups["path"].Value.Trim();
                if (IsUnderSource(candidate))
                {
                    _currentSourcePath = Path.GetFullPath(candidate);
                    sourceFileDiscovered = !Directory.Exists(_currentSourcePath);
                }
            }

            var hasParsedProgress = RobocopyOutputParser.TryParseProgress(line, out var parsed)
                && parsed is not null;
            if (_currentSourcePath is null
                || (!hasParsedProgress && !(_listedFilesAreComplete && sourceFileDiscovered)))
            {
                return false;
            }

            var relativePath = Path.GetRelativePath(_sourceDirectory, _currentSourcePath);
            if (relativePath == "." || relativePath.StartsWith("..", StringComparison.Ordinal))
            {
                return false;
            }

            progress = new RobocopyFileProgress(
                Path.GetFileName(_currentSourcePath),
                hasParsedProgress ? parsed!.Percent : 100,
                _currentSourcePath,
                Path.Combine(_destinationDirectory, relativePath),
                line);
            return true;
        }
    }

    private bool IsUnderSource(string candidate)
    {
        try
        {
            var fullPath = Path.GetFullPath(candidate);
            return fullPath.StartsWith(_sourceDirectory, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fullPath, _sourceDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim())) + Path.DirectorySeparatorChar;
    }
}
