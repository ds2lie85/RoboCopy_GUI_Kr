namespace RoboCopy_GUI_Kr.Core.Errors;

public static class MiniUiCompletionPolicy
{
    public static bool RequiresErrorManagement(bool workStarted, bool isRunning, bool isPaused,
        int waitingCount, bool hasFailedJobs, bool hasUnresolvedFiles) =>
        workStarted && !isRunning && !isPaused && waitingCount == 0
        && (hasFailedJobs || hasUnresolvedFiles);
}
