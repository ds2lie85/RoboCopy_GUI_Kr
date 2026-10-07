using System.Text.RegularExpressions;

namespace RoboCopy_GUI_Kr.Core.Robocopy;

public sealed record RobocopySummaryData(
    IReadOnlyList<string> Directories,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Bytes,
    IReadOnlyList<string> Times,
    IReadOnlyList<string> Speeds,
    string Ended);

public static partial class RobocopySummaryParser
{
    [GeneratedRegex(@"^\s*(?<label>Dirs|Files|Bytes|Times|Speed|Ended|디렉터리|파일|바이트|시간|속도|종료됨)\s*:\s*(?<value>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SummaryLineRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhiteSpaceRegex();

    public static bool TryParse(string? text, out RobocopySummaryData? summary)
    {
        summary = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[]? directories = null;
        string[]? files = null;
        string[]? bytes = null;
        string[]? times = null;
        var speeds = new List<string>();
        var ended = string.Empty;

        foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var match = SummaryLineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var label = match.Groups["label"].Value;
            var value = match.Groups["value"].Value.Trim();
            if (label.Equals("Dirs", StringComparison.OrdinalIgnoreCase) || label == "디렉터리")
            {
                directories = ParseSixColumns(value, isTimeRow: false);
            }
            else if (label.Equals("Files", StringComparison.OrdinalIgnoreCase) || label == "파일")
            {
                files = ParseSixColumns(value, isTimeRow: false);
            }
            else if (label.Equals("Bytes", StringComparison.OrdinalIgnoreCase) || label == "바이트")
            {
                bytes = ParseSixColumns(value, isTimeRow: false);
            }
            else if (label.Equals("Times", StringComparison.OrdinalIgnoreCase) || label == "시간")
            {
                times = ParseSixColumns(value, isTimeRow: true);
            }
            else if (label.Equals("Speed", StringComparison.OrdinalIgnoreCase) || label == "속도")
            {
                speeds.Add(value);
            }
            else if (label.Equals("Ended", StringComparison.OrdinalIgnoreCase) || label == "종료됨")
            {
                ended = value;
            }
        }

        if (directories is null || files is null || bytes is null || times is null || string.IsNullOrWhiteSpace(ended))
        {
            return false;
        }

        summary = new RobocopySummaryData(directories, files, bytes, times, speeds, ended);
        return true;
    }

    private static string[] ParseSixColumns(string value, bool isTimeRow)
    {
        var tokens = WhiteSpaceRegex().Split(value.Trim())
            .Where(token => token.Length > 0)
            .ToArray();

        if (isTimeRow && tokens.Length == 4)
        {
            return [tokens[0], tokens[1], string.Empty, string.Empty, tokens[2], tokens[3]];
        }

        var result = new string[6];
        Array.Copy(tokens, result, Math.Min(tokens.Length, result.Length));
        return result;
    }
}
