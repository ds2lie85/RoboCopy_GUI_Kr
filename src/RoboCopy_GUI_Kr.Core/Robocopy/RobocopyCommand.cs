namespace RoboCopy_GUI_Kr.Core.Robocopy;

/// <summary>
/// 실행 파일과 토큰화된 인자를 보관합니다. 실제 실행 시 문자열을 다시 분해하지 않습니다.
/// </summary>
public sealed record RobocopyCommand(string FileName, IReadOnlyList<string> Arguments)
{
    public string ToDisplayString()
    {
        return string.Join(' ', new[] { FileName }.Concat(Arguments).Select(QuoteForDisplay));
    }

    private static string QuoteForDisplay(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        return value.Any(char.IsWhiteSpace) || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
    }
}
