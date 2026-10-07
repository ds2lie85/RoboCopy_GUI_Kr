namespace RoboCopy_GUI_Kr.Core.ContinuousSync;

public enum ContinuousSyncChangeKind
{
    Copied,
    Deleted,
}

public sealed record ContinuousSyncChangeItem(
    ContinuousSyncChangeKind Kind,
    string RelativePath);

public sealed record ContinuousSyncNotification
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public string SourceDirectory { get; init; } = string.Empty;

    public string DestinationDirectory { get; init; } = string.Empty;

    public int CopiedFiles { get; init; }

    public int DeletedFiles { get; init; }

    public IReadOnlyList<ContinuousSyncChangeItem> Changes { get; init; } = [];
}
