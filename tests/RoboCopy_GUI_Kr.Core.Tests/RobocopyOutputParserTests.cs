using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class RobocopyOutputParserTests
{
    [Theory]
    [InlineData("        54.3%        New File", 54.3)]
    [InlineData("        12,5%        새 파일", 12.5)]
    [InlineData("100%", 100)]
    public void TryParseProgress_ParsesLocalizedDecimalSeparator(string line, double expected)
    {
        var parsed = RobocopyOutputParser.TryParseProgress(line, out var progress);

        Assert.True(parsed);
        Assert.NotNull(progress);
        Assert.Equal(expected, progress.Percent, precision: 1);
    }

    [Fact]
    public void TryParseProgress_IgnoresSummaryLinesWithoutPercent()
    {
        Assert.False(RobocopyOutputParser.TryParseProgress("Files : 10 9 1", out var progress));
        Assert.Null(progress);
    }
}
