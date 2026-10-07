using RoboCopy_GUI_Kr.Core.Jobs;

namespace RoboCopy_GUI_Kr.Core.Errors;

public static class ErrorFileRetryStateResolver
{
    public static ErrorFileRetryState FromQueueOutcome(JobQueueEntryState outcome) => outcome switch
    {
        JobQueueEntryState.Completed => ErrorFileRetryState.Resolved,
        JobQueueEntryState.Paused => ErrorFileRetryState.Paused,
        _ => ErrorFileRetryState.RetryFailed,
    };
}
