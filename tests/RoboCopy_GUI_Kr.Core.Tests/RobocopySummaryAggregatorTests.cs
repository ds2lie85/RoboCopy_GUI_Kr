using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class RobocopySummaryAggregatorTests
{
    [Fact]
    public void Build_SumsMultipleRobocopyJobsAndDirectFileCopies()
    {
        var aggregator = new RobocopySummaryAggregator();
        aggregator.Add(new RobocopySummaryData(
            ["2", "2", "0", "0", "0", "0"],
            ["3", "2", "1", "0", "0", "0"],
            ["300", "200", "100", "0", "0", "0"],
            ["0:00:03", "0:00:02", "", "", "0:00:00", "0:00:01"],
            [],
            "ended"), TimeSpan.FromSeconds(4));
        aggregator.AddCopiedFile(50, TimeSpan.FromSeconds(1));

        var result = aggregator.Build();

        Assert.Equal(2, result.OperationCount);
        Assert.Equal(4, result.Files[0]);
        Assert.Equal(3, result.Files[1]);
        Assert.Equal(350, result.Bytes[0]);
        Assert.Equal(250, result.Bytes[1]);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Elapsed);
        Assert.Equal(50, result.BytesPerSecond);
    }

    [Fact]
    public void Add_FileOnlyShellJobDoesNotCountItsSourceFolderAsCopiedDirectory()
    {
        var aggregator = new RobocopySummaryAggregator();
        aggregator.Add(new RobocopySummaryData(
            ["1", "1", "1", "0", "0", "0"],
            ["10", "7", "3", "0", "0", "0"],
            ["100", "70", "30", "0", "0", "0"],
            ["0:00:01", "0:00:01", "", "", "0:00:00", "0:00:00"],
            [],
            "ended"), TimeSpan.FromSeconds(1), includeDirectories: false);

        var result = aggregator.Build();

        Assert.All(result.Directories, value => Assert.Equal(0, value));
        Assert.Equal(10, result.Files[0]);
        Assert.Equal(7, result.Files[1]);
        Assert.Equal(3, result.Files[2]);
    }
}
