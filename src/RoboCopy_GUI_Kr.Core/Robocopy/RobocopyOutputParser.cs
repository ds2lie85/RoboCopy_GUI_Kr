using System.Globalization;
using System.Text.RegularExpressions;

namespace RoboCopy_GUI_Kr.Core.Robocopy;

public sealed record RobocopyProgress(double Percent, string RawLine);

public static partial class RobocopyOutputParser
{
    [GeneratedRegex(@"(?<!\d)(?<percent>\d{1,3}(?:[\.,]\d+)?)%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentRegex();

    public static bool TryParseProgress(string? line, out RobocopyProgress? progress)
    {
        progress = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = PercentRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        var normalized = match.Groups["percent"].Value.Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        progress = new RobocopyProgress(Math.Clamp(value, 0, 100), line);
        return true;
    }
}
