using RoboCopy_GUI_Kr.Core.Progress;
using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class JobProgressAggregatorTests
{
    [Fact]
    public void Report_AggregatesFileProgressByFileSizeWithoutDoubleCounting()
    {
        var snapshot = new FileWorkloadSnapshot(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            [@"D:\원본\small.bin"] = 100,
            [@"D:\원본\large.bin"] = 300,
        });
        var aggregator = new JobProgressAggregator(snapshot);

        Assert.Equal(12.5, aggregator.Report(CreateProgress(@"D:\원본\small.bin", 50)), precision: 1);
        Assert.Equal(12.5, aggregator.Report(CreateProgress(@"D:\원본\small.bin", 40)), precision: 1);
        Assert.Equal(25, aggregator.Report(CreateProgress(@"D:\원본\small.bin", 100)), precision: 1);
        Assert.Equal(100, aggregator.Report(CreateProgress(@"D:\원본\large.bin", 100)), precision: 1);
    }

    private static RobocopyFileProgress CreateProgress(string sourcePath, double percent)
    {
        return new RobocopyFileProgress(
            Path.GetFileName(sourcePath),
            percent,
            sourcePath,
            sourcePath.Replace(@"D:\원본", @"E:\대상", StringComparison.OrdinalIgnoreCase),
            string.Empty);
    }
}
