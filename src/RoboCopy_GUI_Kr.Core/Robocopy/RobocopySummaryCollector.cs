using System.Text;
using System.Text.RegularExpressions;

namespace RoboCopy_GUI_Kr.Core.Robocopy;

public sealed partial class RobocopySummaryCollector
{
    private readonly object _gate = new();
    private readonly List<string> _candidate = [];
    private string? _completedSummary;
    private bool _collecting;

    [GeneratedRegex(@"^\s*(?:Dirs|Files|Bytes|Times|Speed|Ended|디렉터리|파일|바이트|시간|속도|종료됨)\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SummaryRowRegex();

    public void ProcessLine(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_gate)
        {
            var trimmed = line.Trim();
            if (IsSeparator(trimmed))
            {
                _candidate.Clear();
                _candidate.Add(line);
                _collecting = false;
                return;
            }

            if (_candidate.Count == 1 && IsSummaryHeader(trimmed))
            {
                _candidate.Add(line);
                _collecting = true;
                return;
            }

            if (!_collecting)
            {
                return;
            }

            if (trimmed.Length == 0 || SummaryRowRegex().IsMatch(line))
            {
                _candidate.Add(line);
                if (trimmed.StartsWith("Ended", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("종료됨", StringComparison.Ordinal))
                {
                    _completedSummary = JoinLines(_candidate);
                    _candidate.Clear();
                    _collecting = false;
                }
            }
        }
    }

    public string? GetSummary()
    {
        lock (_gate)
        {
            return _completedSummary;
        }
    }

    private static bool IsSeparator(string value) =>
        value.Length >= 20 && value.All(character => character == '-');

    private static bool IsSummaryHeader(string value) =>
        (value.Contains("Total", StringComparison.OrdinalIgnoreCase)
            && value.Contains("Copied", StringComparison.OrdinalIgnoreCase))
        || (value.Contains("전체", StringComparison.Ordinal)
            && value.Contains("복사됨", StringComparison.Ordinal));

    private static string JoinLines(IEnumerable<string> lines)
    {
        var result = new StringBuilder();
        foreach (var line in lines)
        {
            result.AppendLine(line);
        }

        return result.ToString().TrimEnd();
    }
}
