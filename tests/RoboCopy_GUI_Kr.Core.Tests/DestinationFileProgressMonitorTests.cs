using RoboCopy_GUI_Kr.Core.Progress;
using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class DestinationFileProgressMonitorTests
{
    [Fact]
    public void TryCreateProgress_MapsTouchedDestinationBackToSourceAndCapsAtNinetyNine()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".destination-progress", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(testRoot, "원본");
        var destinationRoot = Path.Combine(testRoot, "대상");
        var sourcePath = Path.Combine(sourceRoot, "하위", "파일.bin");
        var destinationPath = Path.Combine(destinationRoot, "하위", "파일.bin");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.WriteAllBytes(sourcePath, new byte[100]);
            File.WriteAllBytes(destinationPath, new byte[100]);
            var workload = new FileWorkloadSnapshot(
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { [sourcePath] = 100 });

            using var monitor = new DestinationFileProgressMonitor(sourceRoot, destinationRoot, workload, _ => { });

            Assert.True(monitor.TryCreateProgress(destinationPath, out var progress));
            Assert.NotNull(progress);
            Assert.Equal(sourcePath, progress.SourcePath);
            Assert.Equal(destinationPath, progress.DestinationPath);
            Assert.Equal(99, progress.Percent);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FileSystemWatcher_PublishesProgressForActualDestinationWrite()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".destination-watch", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(testRoot, "source");
        var destinationRoot = Path.Combine(testRoot, "destination");
        var sourcePath = Path.Combine(sourceRoot, "watched.bin");
        var destinationPath = Path.Combine(destinationRoot, "watched.bin");

        try
        {
            Directory.CreateDirectory(sourceRoot);
            Directory.CreateDirectory(destinationRoot);
            File.WriteAllBytes(sourcePath, new byte[200]);
            var workload = new FileWorkloadSnapshot(
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { [sourcePath] = 200 });
            var observed = new TaskCompletionSource<RobocopyFileProgress>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            using var monitor = new DestinationFileProgressMonitor(
                sourceRoot,
                destinationRoot,
                workload,
                progress =>
                {
                    if (progress.Percent >= 50)
                    {
                        observed.TrySetResult(progress);
                    }
                });

            File.WriteAllBytes(destinationPath, new byte[100]);
            var progress = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(sourcePath, progress.SourcePath);
            Assert.Equal(50, progress.Percent);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CompletionMonitor_PublishesOneHundredAfterWriterReleasesCompletedFile()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".destination-completion", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(testRoot, "원본");
        var destinationRoot = Path.Combine(testRoot, "대상");
        var sourcePath = Path.Combine(sourceRoot, "완료.bin");
        var destinationPath = Path.Combine(destinationRoot, "완료.bin");

        try
        {
            Directory.CreateDirectory(sourceRoot);
            Directory.CreateDirectory(destinationRoot);
            File.WriteAllBytes(sourcePath, new byte[128]);
            var workload = new FileWorkloadSnapshot(
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { [sourcePath] = 128 });
            var completed = new TaskCompletionSource<RobocopyFileProgress>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            using var monitor = new DestinationFileProgressMonitor(
                sourceRoot,
                destinationRoot,
                workload,
                progress =>
                {
                    if (progress.Percent >= 100)
                    {
                        completed.TrySetResult(progress);
                    }
                });

            using (var writer = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                writer.Write(new byte[128]);
                writer.Flush(flushToDisk: true);
                await Task.Delay(250);
                Assert.False(completed.Task.IsCompleted);
            }

            var progress = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(100, progress.Percent);
            Assert.Equal(destinationPath, progress.DestinationPath);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
