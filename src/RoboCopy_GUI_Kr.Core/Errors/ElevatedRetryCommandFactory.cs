using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Errors;

public static class ElevatedRetryCommandFactory
{
    public static RobocopyCommand Create(
        ElevatedRetryFile item,
        bool sourceIsDirectory,
        int retryCount,
        int retryWaitSeconds,
        bool restartable)
    {
        ArgumentNullException.ThrowIfNull(item);

        var sourceDirectory = sourceIsDirectory
            ? Path.TrimEndingDirectorySeparator(item.SourcePath)
            : Path.GetDirectoryName(item.SourcePath)
                ?? throw new ArgumentException("The source file must have a parent directory.", nameof(item));
        var destinationDirectory = sourceIsDirectory
            ? Path.TrimEndingDirectorySeparator(item.DestinationPath)
            : Path.GetDirectoryName(item.DestinationPath)
                ?? throw new ArgumentException("The destination file must have a parent directory.", nameof(item));

        return new RobocopyCommandBuilder().Build(new RobocopyJobOptions
        {
            SourceDirectory = sourceDirectory,
            DestinationDirectory = destinationDirectory,
            FileSpecifications = sourceIsDirectory ? ["*"] : [Path.GetFileName(item.SourcePath)],
            RecurseSubdirectories = sourceIsDirectory,
            IncludeEmptyDirectories = sourceIsDirectory,
            RetryCount = retryCount,
            RetryWaitSeconds = retryWaitSeconds,
            Restartable = restartable,
            MultiThreadCount = 1,
        });
    }
}

