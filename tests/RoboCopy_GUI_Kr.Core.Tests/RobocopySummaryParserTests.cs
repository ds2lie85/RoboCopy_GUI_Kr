using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class RobocopySummaryParserTests
{
    [Fact]
    public void TryParse_MapsRobocopyColumnsAndTimeGapsToStructuredValues()
    {
        const string output = """
               Total    Copied   Skipped  Mismatch    FAILED    Extras
    Dirs :     28534     28534         1         0         0         0
   Files :     87640     87640         0         0         0         0
   Bytes : 103485701819 103485701819         0         0         0         0
   Times :   0:25:18   0:04:12                       0:00:00   0:00:40
   Speed :         410470229 Bytes/sec.
   Speed :         23487.294 MegaBytes/min.
   Ended : 2026년 9월 29일 화요일 오전 8:47:02
""";

        Assert.True(RobocopySummaryParser.TryParse(output, out var summary));
        Assert.NotNull(summary);
        Assert.Equal("28534", summary.Directories[0]);
        Assert.Equal("1", summary.Directories[2]);
        Assert.Equal(string.Empty, summary.Times[2]);
        Assert.Equal("0:00:00", summary.Times[4]);
        Assert.Equal("410470229 Bytes/sec.", summary.Speeds[0]);
        Assert.Equal("2026년 9월 29일 화요일 오전 8:47:02", summary.Ended);
    }

    [Fact]
    public void TryParse_AcceptsKoreanLabels()
    {
        const string output = """
디렉터리 : 2 2 0 0 0 0
파일 : 4 4 0 0 0 0
바이트 : 8 8 0 0 0 0
시간 : 0:00:02 0:00:01 0:00:00 0:00:00
속도 : 8 바이트/초
종료됨: 오늘
""";

        Assert.True(RobocopySummaryParser.TryParse(output, out var summary));
        Assert.NotNull(summary);
        Assert.Equal("4", summary.Files[0]);
        Assert.Equal("8 바이트/초", Assert.Single(summary.Speeds));
        Assert.Equal("오늘", summary.Ended);
    }
}
