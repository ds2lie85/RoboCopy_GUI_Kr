namespace RoboCopy_GUI_Kr.Core.Errors;

public static class ErrorFileLockPaths
{
    public static string[] Collect(IEnumerable<RobocopyErrorItem> items) => items
        .SelectMany(item => new[] { item.SourcePath, item.DestinationPath })
        .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
