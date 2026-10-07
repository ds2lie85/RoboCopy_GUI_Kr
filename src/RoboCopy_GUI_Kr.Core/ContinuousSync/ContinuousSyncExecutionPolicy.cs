namespace RoboCopy_GUI_Kr.Core.ContinuousSync;

public enum ContinuousSyncPauseReason
{
    None,
    OutsideSchedule,
    Battery,
    MeteredNetwork,
}

public static class ContinuousSyncExecutionPolicy
{
    public static ContinuousSyncPauseReason Evaluate(
        ContinuousSyncConfiguration configuration,
        DateTimeOffset now,
        bool isOnBattery,
        bool isMeteredNetwork)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.PauseOnBattery && isOnBattery)
        {
            return ContinuousSyncPauseReason.Battery;
        }

        if (configuration.PauseOnMeteredNetwork && isMeteredNetwork)
        {
            return ContinuousSyncPauseReason.MeteredNetwork;
        }

        if (configuration.LimitToSchedule
            && !IsWithinSchedule(now.TimeOfDay, configuration.ScheduleStartMinute, configuration.ScheduleEndMinute))
        {
            return ContinuousSyncPauseReason.OutsideSchedule;
        }

        return ContinuousSyncPauseReason.None;
    }

    public static bool IsWithinSchedule(TimeSpan time, int startMinute, int endMinute)
    {
        if (startMinute is < 0 or > 1439 || endMinute is < 0 or > 1440)
        {
            throw new ArgumentOutOfRangeException(nameof(startMinute));
        }

        var current = (int)time.TotalMinutes;
        if (endMinute == 1440)
        {
            return current >= startMinute;
        }

        if (startMinute == endMinute)
        {
            return true;
        }

        return startMinute < endMinute
            ? current >= startMinute && current < endMinute
            : current >= startMinute || current < endMinute;
    }
}
