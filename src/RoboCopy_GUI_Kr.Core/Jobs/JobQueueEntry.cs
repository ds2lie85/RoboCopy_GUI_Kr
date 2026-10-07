using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Jobs;

public sealed record JobQueueEntry
{
    public required Guid Id { get; init; }

    public required RobocopyJobOptions Options { get; init; }

    public JobQueueEntryState State { get; init; } = JobQueueEntryState.Waiting;

    public DateTimeOffset QueuedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}
