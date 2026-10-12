namespace RoboCopy_GUI_Kr.Core.Shell;

public sealed class NamedFileCopyService
{
    public async Task CopyAsync(
        string sourcePath,
        string destinationPath,
        bool moveSource,
        bool overwriteDestination = false,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var sourceInfo = new FileInfo(sourcePath);
        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException("Source file was not found.", sourcePath);
        }

        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationPath))!;
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        Exception? operationFailure = null;
        try
        {
            await using (var source = new FileStream(
                             sourceInfo.FullName,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[1024 * 1024];
                long copied = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    copied += read;
                    progress?.Report(sourceInfo.Length == 0 ? 100 : copied * 100d / sourceInfo.Length);
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.SetCreationTime(temporaryPath, sourceInfo.CreationTime);
            File.SetLastWriteTime(temporaryPath, sourceInfo.LastWriteTime);
            var sourceAttributes = sourceInfo.Attributes & ~FileAttributes.ReparsePoint;
            File.SetAttributes(temporaryPath, sourceAttributes & ~FileAttributes.ReadOnly);
            cancellationToken.ThrowIfCancellationRequested();
            ReplaceTemporaryFile(temporaryPath, destinationPath, overwriteDestination);
            File.SetAttributes(destinationPath, sourceAttributes);
            progress?.Report(100);

            if (moveSource)
            {
                try
                {
                    File.Delete(sourceInfo.FullName);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    throw new MoveSourceDeletionException(
                        sourceInfo.FullName,
                        destinationPath,
                        exception,
                        exception is UnauthorizedAccessException ? 5 : exception.HResult & 0xFFFF);
                }
            }
        }
        catch (Exception exception)
        {
            operationFailure = exception;
            throw;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.SetAttributes(temporaryPath, File.GetAttributes(temporaryPath) & ~FileAttributes.ReadOnly);
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanupFailure) when (operationFailure is not null
                && cleanupFailure is IOException or UnauthorizedAccessException)
            {
                // Preserve the operation's original error if cleanup also fails.
                operationFailure.Data["TemporaryFileCleanupError"] = cleanupFailure;
            }
        }
    }

    private static void ReplaceTemporaryFile(string temporaryPath, string destinationPath, bool overwrite)
    {
        FileAttributes? originalAttributes = null;
        if (overwrite && File.Exists(destinationPath))
        {
            var attributes = File.GetAttributes(destinationPath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(destinationPath, attributes & ~FileAttributes.ReadOnly);
                originalAttributes = attributes;
            }
        }

        try
        {
            File.Move(temporaryPath, destinationPath, overwrite);
        }
        catch (Exception exception)
        {
            if (originalAttributes is { } attributes)
            {
                try { File.SetAttributes(destinationPath, attributes); }
                catch (Exception restoreFailure) when (restoreFailure is IOException or UnauthorizedAccessException)
                {
                    exception.Data["DestinationAttributeRestoreError"] = restoreFailure;
                }
            }
            throw;
        }
    }
}

public sealed class MoveSourceDeletionException : IOException
{
    public MoveSourceDeletionException(
        string sourcePath,
        string destinationPath,
        Exception innerException,
        int errorCode)
        : base(innerException.Message, innerException)
    {
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        ErrorCode = errorCode == 0 ? 1 : errorCode;
    }

    public string SourcePath { get; }

    public string DestinationPath { get; }

    public int ErrorCode { get; }
}
