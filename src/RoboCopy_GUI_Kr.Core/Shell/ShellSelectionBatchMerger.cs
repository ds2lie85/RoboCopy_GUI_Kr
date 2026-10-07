namespace RoboCopy_GUI_Kr.Core.Shell;

public static class ShellSelectionBatchMerger
{
    public static IReadOnlyList<string> Merge(
        IEnumerable<string> existingPaths,
        DateTimeOffset existingUpdatedAt,
        IEnumerable<string> incomingPaths,
        DateTimeOffset now,
        TimeSpan batchWindow)
    {
        if (batchWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(batchWindow));
        }

        var incoming = incomingPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (now - existingUpdatedAt > batchWindow)
        {
            return incoming;
        }

        return existingPaths.Concat(incoming)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
