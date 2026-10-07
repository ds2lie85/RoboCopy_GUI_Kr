using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ShellSelectionBatchMergerTests
{
    [Fact]
    public void Merge_ConcurrentExplorerInvocationsAccumulateWithoutDuplicates()
    {
        var now = DateTimeOffset.UtcNow;

        var result = ShellSelectionBatchMerger.Merge(
            [@"C:\자료\하나.txt"],
            now.AddMilliseconds(-200),
            [@"C:\자료\둘.txt", @"c:\자료\하나.txt"],
            now,
            TimeSpan.FromSeconds(2));

        Assert.Equal(2, result.Count);
        Assert.Contains(@"C:\자료\둘.txt", result);
    }

    [Fact]
    public void Merge_LaterCopySelectionReplacesPreviousManifest()
    {
        var now = DateTimeOffset.UtcNow;

        var result = ShellSelectionBatchMerger.Merge(
            [@"C:\이전\파일.txt"],
            now.AddSeconds(-3),
            [@"D:\새 선택\파일.txt"],
            now,
            TimeSpan.FromSeconds(2));

        Assert.Equal([@"D:\새 선택\파일.txt"], result);
    }
}
