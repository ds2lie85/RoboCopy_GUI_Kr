namespace RoboCopy_GUI_Kr.Core.Progress;

public static class FileProgressDisplayName
{
    public static string Format(string? fileName, string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var resolvedName = string.IsNullOrWhiteSpace(fileName)
            ? Path.GetFileName(sourcePath)
            : fileName.Trim();

        var parentName = Path.GetFileName(Path.GetDirectoryName(sourcePath));
        return string.IsNullOrWhiteSpace(parentName)
            ? resolvedName
            : Path.Combine(parentName, resolvedName);
    }
}
