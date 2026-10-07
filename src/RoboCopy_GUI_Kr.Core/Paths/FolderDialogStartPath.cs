using System.IO;

namespace RoboCopy_GUI_Kr.Core.Paths;

public static class FolderDialogStartPath
{
    public static string? ResolveDestinationStartPath(string? sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return null;
        }

        try
        {
            var sourcePath = Path.GetFullPath(sourceDirectory);
            return Directory.GetParent(sourcePath)?.FullName ?? Path.GetPathRoot(sourcePath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
