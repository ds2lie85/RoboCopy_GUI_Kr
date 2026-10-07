using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Moves;

public sealed record FailedMoveCleanupProgress(int CompletedItems, int TotalItems);

public sealed record FailedMoveCleanupResult(
    int DeletedFiles,
    int DeletedDirectories,
    int PreservedExistingFiles,
    int SkippedFiles);

public sealed class FailedMoveCleanupService
{
    public async Task<FailedMoveCleanupResult> DeleteNewCopiedFilesAsync(
        RobocopyJobOptions options,
        IReadOnlyCollection<string> sourceCandidates,
        IReadOnlyCollection<string> sourceDirectoryCandidates,
        IReadOnlySet<string> destinationsExistingBeforeRun,
        IReadOnlySet<string> destinationDirectoriesExistingBeforeRun,
        IProgress<FailedMoveCleanupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sourceRoot = Path.GetFullPath(options.SourceDirectory);
        var destinationRoot = Path.GetFullPath(options.DestinationDirectory);
        var deleted = 0;
        var deletedDirectories = 0;
        var preserved = 0;
        var skipped = 0;
        var totalItems = sourceCandidates.Count + sourceDirectoryCandidates.Count;
        var completedItems = 0;
        var lastReportedPercent = -1;

        foreach (var sourcePath in sourceCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.GetFullPath(Path.Combine(
                destinationRoot,
                Path.GetRelativePath(sourceRoot, sourcePath)));
            if (!File.Exists(destinationPath))
            {
                ReportProgress(progress, ++completedItems, totalItems, ref lastReportedPercent);
                continue;
            }

            if (destinationsExistingBeforeRun.Contains(destinationPath))
            {
                preserved++;
                ReportProgress(progress, ++completedItems, totalItems, ref lastReportedPercent);
                continue;
            }

            try
            {
                if (await ContentsEqualAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false))
                {
                    File.Delete(destinationPath);
                    deleted++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                skipped++;
            }

            ReportProgress(progress, ++completedItems, totalItems, ref lastReportedPercent);
        }

        foreach (var sourceDirectory in sourceDirectoryCandidates.OrderByDescending(path => path.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationDirectory = Path.GetFullPath(Path.Combine(
                destinationRoot,
                Path.GetRelativePath(sourceRoot, sourceDirectory)));
            if (!destinationDirectoriesExistingBeforeRun.Contains(destinationDirectory)
                && Directory.Exists(destinationDirectory)
                && !Directory.EnumerateFileSystemEntries(destinationDirectory).Any())
            {
                Directory.Delete(destinationDirectory);
                deletedDirectories++;
            }

            ReportProgress(progress, ++completedItems, totalItems, ref lastReportedPercent);
        }

        return new FailedMoveCleanupResult(deleted, deletedDirectories, preserved, skipped);
    }

    private static void ReportProgress(
        IProgress<FailedMoveCleanupProgress>? progress,
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
        progress.Report(new FailedMoveCleanupProgress(completed, total));
    }

    private static async Task<bool> ContentsEqualAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var sourceInfo = new FileInfo(sourcePath);
        var destinationInfo = new FileInfo(destinationPath);
        if (sourceInfo.Length != destinationInfo.Length)
        {
            return false;
        }

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        await using var destination = new FileStream(destinationPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        var sourceBuffer = new byte[1024 * 1024];
        var destinationBuffer = new byte[1024 * 1024];
        while (true)
        {
            var sourceRead = await source.ReadAsync(sourceBuffer, cancellationToken).ConfigureAwait(false);
            var destinationRead = await destination.ReadAsync(destinationBuffer, cancellationToken).ConfigureAwait(false);
            if (sourceRead != destinationRead)
            {
                return false;
            }

            if (sourceRead == 0)
            {
                return true;
            }

            if (!sourceBuffer.AsSpan(0, sourceRead).SequenceEqual(destinationBuffer.AsSpan(0, destinationRead)))
            {
                return false;
            }
        }
    }
}
