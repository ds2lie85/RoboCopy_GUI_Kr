using RoboCopy_GUI_Kr.Core.Jobs;
using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Sync;

namespace RoboCopy_GUI_Kr;

public abstract record PostCopyOperation(JobQueueEntryState CompletionState);

public sealed record MovePostCopyOperation(
    RobocopyJobOptions Options,
    JobQueueEntryState CompletionState)
    : PostCopyOperation(CompletionState);

public sealed record DeletePostCopyOperation(
    RobocopyJobOptions Options,
    IReadOnlyCollection<string> SourceFiles,
    IReadOnlyCollection<string> SourceDirectories,
    IReadOnlySet<string> ExistingDestinationFiles,
    IReadOnlySet<string> ExistingDestinationDirectories,
    JobQueueEntryState CompletionState)
    : PostCopyOperation(CompletionState);

public sealed record OneWaySyncDeletePostCopyOperation(
    RobocopyJobOptions Options,
    IReadOnlyCollection<OneWaySyncExtraItem> SelectedItems,
    JobQueueEntryState CompletionState)
    : PostCopyOperation(CompletionState);
