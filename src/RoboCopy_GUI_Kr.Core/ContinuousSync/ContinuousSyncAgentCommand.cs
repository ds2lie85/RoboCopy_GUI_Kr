namespace RoboCopy_GUI_Kr.Core.ContinuousSync;

public static class ContinuousSyncAgentCommand
{
    public static string BuildStartupCommand(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{Path.GetFullPath(executablePath)}\"";
    }
}
