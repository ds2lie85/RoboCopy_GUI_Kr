namespace RoboCopy_GUI_Kr.Core.Sync;

public sealed record DestinationOnlyReviewSet(
    IReadOnlyList<OneWaySyncExtraItem> NewItems,
    IReadOnlyList<OneWaySyncExtraItem> RetainedItems);

public static class DestinationOnlyRetention
{
    public static DestinationOnlyReviewSet Split(
        IReadOnlyList<OneWaySyncExtraItem> items,
        IReadOnlyCollection<string> retainedPaths)
    {
        var retained = retainedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new DestinationOnlyReviewSet(
            items.Where(item => !retained.Contains(item.RelativePath)).ToArray(),
            items.Where(item => retained.Contains(item.RelativePath)).ToArray());
    }

    public static IReadOnlyList<string> Update(
        IReadOnlyCollection<string> existingRetainedPaths,
        IReadOnlyCollection<OneWaySyncExtraItem> newItems,
        IReadOnlyCollection<OneWaySyncExtraItem> selectedForDeletion,
        IReadOnlyCollection<OneWaySyncExtraItem> selectedRetainedForDeletion)
    {
        var selected = selectedForDeletion.Select(item => item.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return existingRetainedPaths
            .Except(selectedRetainedForDeletion.Select(item => item.RelativePath), StringComparer.OrdinalIgnoreCase)
            .Concat(newItems.Where(item => !selected.Contains(item.RelativePath)).Select(item => item.RelativePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
