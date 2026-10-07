namespace RoboCopy_GUI_Kr.Core.Errors;

public enum ErrorFileRetryState
{
    Failed,
    Queued,
    Running,
    Paused,
    Resolved,
    RetryFailed,
}
