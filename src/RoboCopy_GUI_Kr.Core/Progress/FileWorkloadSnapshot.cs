namespace RoboCopy_GUI_Kr.Core.Progress;

public sealed record FileWorkloadSnapshot(IReadOnlyDictionary<string, long> FileSizes)
{
    public int TotalFiles => FileSizes.Count;

    public long TotalBytes => FileSizes.Values.Sum();
}
