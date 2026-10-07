using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.ContinuousSync;

public sealed record ContinuousSyncConfiguration
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required RobocopyJobOptions JobOptions { get; init; }

    public int MonitorIntervalMinutes { get; init; } = 5;

    public ContinuousSyncMultiThreadMode MultiThreadMode { get; init; } = ContinuousSyncMultiThreadMode.Recommended;

    public DestinationOnlyPolicy DestinationOnlyPolicy { get; init; } = DestinationOnlyPolicy.Delete;

    public bool LimitToSchedule { get; init; }

    public int ScheduleStartMinute { get; init; }

    public int ScheduleEndMinute { get; init; } = 1440;

    public bool PauseOnBattery { get; init; }

    public bool PauseOnMeteredNetwork { get; init; }

    public bool QuarantineDeletedItems { get; init; } = true;

    public IReadOnlyList<string> RetainedDestinationOnlyPaths { get; init; } = [];
}
