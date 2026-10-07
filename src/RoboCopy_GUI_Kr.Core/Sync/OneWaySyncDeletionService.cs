namespace RoboCopy_GUI_Kr.Core.Sync;

public sealed record OneWaySyncDeletionProgress(int CompletedItems, int TotalItems);

public sealed record OneWaySyncDeletionFailure(string Path, string Reason);

public sealed record OneWaySyncDeletionResult(
    int DeletedFiles,
    int DeletedDirectories,
    IReadOnlyList<OneWaySyncDeletionFailure> Failures)
{
    public bool IsSuccess => Failures.Count == 0;
}

public sealed class OneWaySyncDeletionService
{
    public Task<OneWaySyncDeletionResult> DeleteSelectedAsync(
        string sourceRoot,
        string destinationRoot,
        IReadOnlyCollection<OneWaySyncExtraItem> selectedItems,
        IProgress<OneWaySyncDeletionProgress>? progress = null,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var fullSourceRoot = Path.GetFullPath(sourceRoot);
        var fullDestinationRoot = Path.GetFullPath(destinationRoot);
        var failures = new List<OneWaySyncDeletionFailure>();
        var deletedFiles = 0;
        var deletedDirectories = 0;
        var completed = 0;
        var lastReportedPercent = -1;
        var selectedDirectories = selectedItems.Where(item => item.IsDirectory).ToArray();

        foreach (var item in selectedItems.Where(item => !item.IsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var destinationPath = EnsureWithinRoot(item.DestinationPath, fullDestinationRoot);
                var sourcePath = Path.Combine(fullSourceRoot, Path.GetRelativePath(fullDestinationRoot, destinationPath));
                if (!File.Exists(sourcePath) && File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                    deletedFiles++;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                failures.Add(new OneWaySyncDeletionFailure(item.DestinationPath, exception.Message));
            }

            ReportProgress(progress, ++completed, selectedItems.Count, ref lastReportedPercent);
        }

        foreach (var item in selectedDirectories.OrderByDescending(item => item.DestinationPath.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var destinationPath = EnsureWithinRoot(item.DestinationPath, fullDestinationRoot);
                var sourcePath = Path.Combine(fullSourceRoot, Path.GetRelativePath(fullDestinationRoot, destinationPath));
                if (!Directory.Exists(sourcePath)
                    && Directory.Exists(destinationPath)
                    && !Directory.EnumerateFileSystemEntries(destinationPath).Any())
                {
                    Directory.Delete(destinationPath);
                    deletedDirectories++;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                failures.Add(new OneWaySyncDeletionFailure(item.DestinationPath, exception.Message));
            }

            ReportProgress(progress, ++completed, selectedItems.Count, ref lastReportedPercent);
        }

        return new OneWaySyncDeletionResult(deletedFiles, deletedDirectories, failures);
    }, cancellationToken);

    private static void ReportProgress(
        IProgress<OneWaySyncDeletionProgress>? progress,
        int completed,
        int total,
        ref int lastReportedPercent)
    {
        if (progress is null)
        {
            return;
        }

        var percent = total == 0 ? 100 : completed * 100 / total;
        if (percent == lastReportedPercent && completed != total)
        {
            return;
        }

        lastReportedPercent = percent;
        progress.Report(new OneWaySyncDeletionProgress(completed, total));
    }

    private static string EnsureWithinRoot(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The deletion target is outside the destination root.", nameof(path));
        }

        return fullPath;
    }
}
