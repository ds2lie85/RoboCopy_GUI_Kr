namespace RoboCopy_GUI_Kr.Core.Validation;

public static class PathSafetyValidator
{
    public static IReadOnlyList<string> ValidateCopyDirectories(string sourceDirectory, string destinationDirectory)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            errors.Add("SourceRequired");
        }
        else if (!Directory.Exists(sourceDirectory))
        {
            errors.Add("SourceNotFound");
        }

        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            errors.Add("DestinationRequired");
        }

        if (errors.Count != 0)
        {
            return errors;
        }

        try
        {
            var source = Normalize(sourceDirectory);
            var destination = Normalize(destinationDirectory);

            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("SameDirectory");
            }
            else if (IsSubdirectory(destination, source) || IsSubdirectory(source, destination))
            {
                errors.Add("OverlappingDirectories");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errors.Add("InvalidPath");
        }

        return errors;
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsSubdirectory(string candidate, string parent)
    {
        return candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
