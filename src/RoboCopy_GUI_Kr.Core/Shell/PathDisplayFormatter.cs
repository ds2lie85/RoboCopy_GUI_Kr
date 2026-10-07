namespace RoboCopy_GUI_Kr.Core.Shell;

public static class PathDisplayFormatter
{
    public static string Compact(string path, int maximumLength = 78) =>
        RoboCopy_GUI_Kr.Core.Paths.PathDisplayFormatter.Compact(path, maximumLength);
}
