using System.IO;

namespace RoboCopy_GUI_Kr.Core.Performance;

public sealed class StorageDetector
{
    public StorageKind Detect(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return StorageKind.Network;
        }

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
            {
                return StorageKind.Unknown;
            }

            return new DriveInfo(root).DriveType switch
            {
                DriveType.Fixed => StorageKind.Fixed,
                DriveType.Removable => StorageKind.Removable,
                DriveType.Network => StorageKind.Network,
                _ => StorageKind.Unknown,
            };
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return StorageKind.Unknown;
        }
    }

    public bool AreOnSameVolume(string sourcePath, string destinationPath)
    {
        try
        {
            var sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath));
            var destinationRoot = Path.GetPathRoot(Path.GetFullPath(destinationPath));
            return !string.IsNullOrWhiteSpace(sourceRoot)
                && !string.IsNullOrWhiteSpace(destinationRoot)
                && string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public string? GetVolumeKey(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return string.IsNullOrWhiteSpace(root)
                ? null
                : Path.TrimEndingDirectorySeparator(root).ToUpperInvariant();
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
