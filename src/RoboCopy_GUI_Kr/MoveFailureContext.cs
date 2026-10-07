using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr;

public enum MoveFailurePolicy
{
    MoveVerifiedItemsFirst,
    DeferUntilAllResolved,
}

public enum MoveFailureDecision
{
    MoveVerifiedItemsFirst,
    DeferUntilAllResolved,
    Stop,
    DeleteCopiedFiles,
}

public sealed class MoveFailureContext(RobocopyJobOptions originalOptions, MoveFailurePolicy policy)
{
    public RobocopyJobOptions OriginalOptions { get; } = originalOptions;

    public MoveFailurePolicy Policy { get; } = policy;

    public HashSet<ErrorFileRow> Rows { get; } = [];
}
