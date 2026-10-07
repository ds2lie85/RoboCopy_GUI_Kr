using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class RobocopyFileProgressTrackerTests
{
    [Fact]
    public void TryProcessLine_CombinesFullSourcePathAndFollowingProgress()
    {
        var tracker = new RobocopyFileProgressTracker(@"D:\원본 폴더", @"E:\대상 폴더");

        Assert.False(tracker.TryProcessLine("    새 파일       1024    D:\\원본 폴더\\하위 폴더\\파일 #1.txt", out _));
        Assert.True(tracker.TryProcessLine("        54.3%", out var progress));

        Assert.NotNull(progress);
        Assert.Equal("파일 #1.txt", progress.FileName);
        Assert.Equal(54.3, progress.Percent, precision: 1);
        Assert.Equal(@"D:\원본 폴더\하위 폴더\파일 #1.txt", progress.SourcePath);
        Assert.Equal(@"E:\대상 폴더\하위 폴더\파일 #1.txt", progress.DestinationPath);
    }

    [Fact]
    public void TryProcessLine_ParsesPercentAndPathOnTheSameLine()
    {
        var tracker = new RobocopyFileProgressTracker(@"C:\원본", @"F:\백업");

        Assert.True(tracker.TryProcessLine("  12,5%    C:\\원본\\한글 특수 # 파일.bin", out var progress));

        Assert.NotNull(progress);
        Assert.Equal(12.5, progress.Percent, precision: 1);
        Assert.Equal(@"F:\백업\한글 특수 # 파일.bin", progress.DestinationPath);
    }

    [Fact]
    public void TryProcessLine_TreatsListedPreviewFileAsCompleted()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".preview", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "원본");
        var destination = Path.Combine(testRoot, "대상");

        try
        {
            Directory.CreateDirectory(source);
            var filePath = Path.Combine(source, "미리보기.txt");
            File.WriteAllText(filePath, "preview");
            var tracker = new RobocopyFileProgressTracker(source, destination, listedFilesAreComplete: true);

            Assert.True(tracker.TryProcessLine($"    새 파일       7    {filePath}", out var progress));
            Assert.NotNull(progress);
            Assert.Equal(100, progress.Percent);
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
