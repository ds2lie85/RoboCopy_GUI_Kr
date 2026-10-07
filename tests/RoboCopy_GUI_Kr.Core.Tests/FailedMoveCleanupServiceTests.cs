using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Moves;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class FailedMoveCleanupServiceTests
{
    [Fact]
    public async Task DeleteNewCopiedFilesAsync_CoalescesLargeProgressStreamsButReportsCompletion()
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".cleanup", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            var candidates = Enumerable.Range(0, 500)
                .Select(index => Path.Combine(source, $"{index}.txt"))
                .ToArray();
            var reports = new List<FailedMoveCleanupProgress>();

            _ = await new FailedMoveCleanupService().DeleteNewCopiedFilesAsync(
                new RobocopyJobOptions { SourceDirectory = source, DestinationDirectory = destination },
                candidates,
                [],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new CallbackProgress<FailedMoveCleanupProgress>(reports.Add));

            Assert.InRange(reports.Count, 90, 101);
            Assert.Equal(500, reports[^1].CompletedItems);
            Assert.Equal(500, reports[^1].TotalItems);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeleteNewCopiedFilesAsync_AfterCancellation_ContinuesWithoutRecopying()
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".cleanup", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            var sourceFiles = new List<string>();
            for (var index = 0; index < 3; index++)
            {
                var sourceFile = Path.Combine(source, $"{index}.txt");
                var destinationFile = Path.Combine(destination, $"{index}.txt");
                await File.WriteAllTextAsync(sourceFile, $"data-{index}");
                await File.WriteAllTextAsync(destinationFile, $"data-{index}");
                sourceFiles.Add(sourceFile);
            }

            var options = new RobocopyJobOptions { SourceDirectory = source, DestinationDirectory = destination };
            using var cancellation = new CancellationTokenSource();
            var progress = new CallbackProgress<FailedMoveCleanupProgress>(update =>
            {
                if (update.CompletedItems == 1)
                {
                    cancellation.Cancel();
                }
            });
            var service = new FailedMoveCleanupService();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DeleteNewCopiedFilesAsync(
                options,
                sourceFiles,
                [],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                progress,
                cancellation.Token));
            Assert.Equal(2, Directory.EnumerateFiles(destination).Count());

            _ = await service.DeleteNewCopiedFilesAsync(
                options,
                sourceFiles,
                [],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Empty(Directory.EnumerateFiles(destination));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeleteNewCopiedFilesAsync_DeletesOnlyNewMatchingDestination()
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".cleanup", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            var newSource = Path.Combine(source, "new.txt");
            var oldSource = Path.Combine(source, "old.txt");
            var mismatchSource = Path.Combine(source, "mismatch.txt");
            await File.WriteAllTextAsync(newSource, "new");
            await File.WriteAllTextAsync(oldSource, "old");
            await File.WriteAllTextAsync(mismatchSource, "source");
            var newDestination = Path.Combine(destination, "new.txt");
            var oldDestination = Path.Combine(destination, "old.txt");
            var mismatchDestination = Path.Combine(destination, "mismatch.txt");
            var copiedFolder = Path.Combine(source, "copied-folder");
            var copiedDestinationFolder = Path.Combine(destination, "copied-folder");
            Directory.CreateDirectory(copiedFolder);
            Directory.CreateDirectory(copiedDestinationFolder);
            await File.WriteAllTextAsync(newDestination, "new");
            await File.WriteAllTextAsync(oldDestination, "old");
            await File.WriteAllTextAsync(mismatchDestination, "different");

            var result = await new FailedMoveCleanupService().DeleteNewCopiedFilesAsync(
                new RobocopyJobOptions { SourceDirectory = source, DestinationDirectory = destination },
                [newSource, oldSource, mismatchSource],
                [copiedFolder],
                new HashSet<string>([oldDestination], StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Equal(1, result.DeletedFiles);
            Assert.Equal(1, result.DeletedDirectories);
            Assert.Equal(1, result.PreservedExistingFiles);
            Assert.Equal(1, result.SkippedFiles);
            Assert.False(File.Exists(newDestination));
            Assert.True(File.Exists(oldDestination));
            Assert.True(File.Exists(mismatchDestination));
            Assert.False(Directory.Exists(copiedDestinationFolder));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
