using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Jobs;

public sealed record PersistedJob
{
    public required Guid Id { get; init; }

    public required RobocopyJobOptions Options { get; init; }

    public JobExecutionState State { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}
