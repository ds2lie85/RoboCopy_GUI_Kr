using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class RobocopyUpdateBufferTests
{
    [Fact]
    public void EnqueueOutput_DropsOldestLinesAtConfiguredLimit()
    {
        var buffer = new RobocopyUpdateBuffer(maximumPendingOutputLines: 3, maximumPendingFileUpdates: 3);

        for (var index = 1; index <= 5; index++)
        {
            buffer.EnqueueOutput($"line-{index}");
        }

        var batch = buffer.Drain(int.MaxValue, int.MaxValue);

        Assert.Equal(["line-3", "line-4", "line-5"], batch.OutputLines);
        Assert.True(batch.UpdatesWereDropped);
        Assert.False(buffer.HasPendingUpdates);
    }

    [Fact]
    public void ProgressUpdates_AreCoalescedToLatestValues()
    {
        var buffer = new RobocopyUpdateBuffer(maximumPendingOutputLines: 10, maximumPendingFileUpdates: 10);
        var initial = CreateFileProgress(@"D:\원본\파일.txt", 10);
        var latest = CreateFileProgress(@"D:\원본\파일.txt", 75);

        buffer.EnqueueOverallProgress(10);
        buffer.EnqueueOverallProgress(75);
        buffer.EnqueueFileProgress(initial);
        buffer.EnqueueFileProgress(latest);

        var batch = buffer.Drain(int.MaxValue, int.MaxValue);

        Assert.Equal(75, batch.OverallProgress);
        Assert.Equal(latest, Assert.Single(batch.FileProgressUpdates));
        Assert.False(batch.UpdatesWereDropped);
    }

    [Fact]
    public void EnqueueFileProgress_DropsAdditionalUniqueFilesAtConfiguredLimit()
    {
        var buffer = new RobocopyUpdateBuffer(maximumPendingOutputLines: 10, maximumPendingFileUpdates: 2);

        buffer.EnqueueFileProgress(CreateFileProgress(@"D:\원본\1.txt", 10));
        buffer.EnqueueFileProgress(CreateFileProgress(@"D:\원본\2.txt", 20));
        buffer.EnqueueFileProgress(CreateFileProgress(@"D:\원본\3.txt", 30));

        var batch = buffer.Drain(int.MaxValue, int.MaxValue);

        Assert.Equal(2, batch.FileProgressUpdates.Count);
        Assert.True(batch.UpdatesWereDropped);
    }

    [Fact]
    public void EnqueueFileProgress_DoesNotReplaceCompletionWithLateWatcherUpdate()
    {
        var buffer = new RobocopyUpdateBuffer(maximumPendingOutputLines: 10, maximumPendingFileUpdates: 10);
        var completed = CreateFileProgress(@"D:\원본\파일.txt", 100);

        buffer.EnqueueFileProgress(completed);
        buffer.EnqueueFileProgress(CreateFileProgress(@"D:\원본\파일.txt", 99));

        var batch = buffer.Drain(int.MaxValue, int.MaxValue);

        Assert.Equal(completed, Assert.Single(batch.FileProgressUpdates));
    }

    [Fact]
    public void RepeatedLargeJobs_AreBoundedAndLeaveNoPendingUpdatesAfterDrain()
    {
        const int outputLimit = 256;
        const int fileLimit = 64;
        var buffer = new RobocopyUpdateBuffer(outputLimit, fileLimit);

        for (var job = 0; job < 3; job++)
        {
            for (var update = 0; update < 10_000; update++)
            {
                buffer.EnqueueOutput($"job-{job}-line-{update}");
                buffer.EnqueueOverallProgress(update % 101);
                buffer.EnqueueFileProgress(CreateFileProgress($@"D:\원본\job-{job}\file-{update % 100}.bin", update % 101));
            }

            var batch = buffer.Drain(int.MaxValue, int.MaxValue);

            Assert.InRange(batch.OutputLines.Count, 1, outputLimit);
            Assert.InRange(batch.FileProgressUpdates.Count, 1, fileLimit);
            Assert.True(batch.UpdatesWereDropped);
            Assert.False(buffer.HasPendingUpdates);
        }
    }

    private static RobocopyFileProgress CreateFileProgress(string sourcePath, double percent)
    {
        return new RobocopyFileProgress(
            Path.GetFileName(sourcePath),
            percent,
            sourcePath,
            Path.Combine(@"E:\대상", Path.GetFileName(sourcePath)),
            $"{percent}% {sourcePath}");
    }
}
