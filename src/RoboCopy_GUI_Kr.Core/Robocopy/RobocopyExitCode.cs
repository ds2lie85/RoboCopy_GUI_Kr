namespace RoboCopy_GUI_Kr.Core.Robocopy;

[Flags]
public enum RobocopyExitFlag
{
    None = 0,
    FilesCopied = 1,
    ExtraFilesDetected = 2,
    MismatchedFilesDetected = 4,
    CopyFailures = 8,
    FatalError = 16,
}

public sealed record RobocopyExitCodeResult(
    int ExitCode,
    bool IsSuccess,
    IReadOnlyList<RobocopyExitFlag> Flags);

public static class RobocopyExitCodeInterpreter
{
    private static readonly RobocopyExitFlag[] KnownFlags =
    [
        RobocopyExitFlag.FilesCopied,
        RobocopyExitFlag.ExtraFilesDetected,
        RobocopyExitFlag.MismatchedFilesDetected,
        RobocopyExitFlag.CopyFailures,
        RobocopyExitFlag.FatalError,
    ];

    public static RobocopyExitCodeResult Interpret(int exitCode)
    {
        if (exitCode < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exitCode));
        }

        var flags = KnownFlags.Where(flag => (exitCode & (int)flag) != 0).ToArray();
        return new RobocopyExitCodeResult(exitCode, exitCode < 8, flags);
    }
}
