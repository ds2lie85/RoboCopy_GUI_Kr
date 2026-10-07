using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class RobocopySummaryCollectorTests
{
    [Fact]
    public void GetSummary_CapturesEnglishSummaryThroughLocalizedEndDate()
    {
        var collector = new RobocopySummaryCollector();
        var lines = new[]
        {
            "unrelated output",
            "------------------------------",
            "               Total    Copied   Skipped  Mismatch    FAILED    Extras",
            "    Dirs :       28534     28534         1         0         0         0",
            "   Files :       87640     87640         0         0         0         0",
            "   Bytes : 103485701819 103485701819         0         0         0         0",
            "   Times :     0:25:18   0:03:59                       0:00:00   0:01:10",
            string.Empty,
            "   Speed :         432077984 Bytes/sec.",
            "   Ended : 2026년 9월 29일 화요일 오전 8:08:43",
        };

        foreach (var line in lines)
        {
            collector.ProcessLine(line);
        }

        var summary = collector.GetSummary();
        Assert.NotNull(summary);
        Assert.DoesNotContain("unrelated output", summary, StringComparison.Ordinal);
        Assert.Contains("Files :", summary, StringComparison.Ordinal);
        Assert.Contains("Ended : 2026년 9월 29일", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSummary_CapturesKoreanSummaryLabels()
    {
        var collector = new RobocopySummaryCollector();
        collector.ProcessLine("------------------------------");
        collector.ProcessLine("               전체      복사됨      건너 뜀       불일치        실패        추가");
        collector.ProcessLine("디렉터리 :        2         2         0         0         0         0");
        collector.ProcessLine("파일 :            4         4         0         0         0         0");
        collector.ProcessLine("종료됨: 2026년 9월 29일 화요일 오전 8:08:43");

        var summary = collector.GetSummary();
        Assert.NotNull(summary);
        Assert.Contains("디렉터리 :", summary, StringComparison.Ordinal);
        Assert.EndsWith("종료됨: 2026년 9월 29일 화요일 오전 8:08:43", summary, StringComparison.Ordinal);
    }
}
