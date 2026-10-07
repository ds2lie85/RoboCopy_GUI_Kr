namespace RoboCopy_GUI_Kr;

public sealed record CopySummaryRow(
    string Label,
    string Total,
    string Copied,
    string Skipped,
    string Mismatch,
    string Failed,
    string Extras)
{
    public static CopySummaryRow Create(string label, IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new CopySummaryRow(
            label,
            GetValue(values, 0),
            GetValue(values, 1),
            GetValue(values, 2),
            GetValue(values, 3),
            GetValue(values, 4),
            GetValue(values, 5));
    }

    private static string GetValue(IReadOnlyList<string> values, int index) =>
        index < values.Count ? values[index] : string.Empty;
}
