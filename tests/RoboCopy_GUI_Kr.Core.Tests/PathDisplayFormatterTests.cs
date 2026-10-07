using RoboCopy_GUI_Kr.Core.Paths;
using System.Xml.Linq;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class PathDisplayFormatterTests
{
    [Fact]
    public void Compact_PreservesRootFirstDirectoryAndNearestParent()
    {
        const string path = @"C:\Users\USER\Desktop\source\잠금시험파일.txt";
        const string expected = @"C:\Users\...\source\잠금시험파일.txt";

        Assert.Equal(expected, PathDisplayFormatter.Compact(path, expected.Length));
    }

    [Fact]
    public void Compact_RevealsParentsStartingFromTheFileSideAsSpaceGrows()
    {
        const string path = @"C:\Users\USER\Desktop\source\잠금시험파일.txt";
        const string compact = @"C:\Users\...\source\잠금시험파일.txt";
        const string expanded = @"C:\Users\...\Desktop\source\잠금시험파일.txt";

        Assert.Equal(compact, PathDisplayFormatter.Compact(path, compact.Length));
        Assert.Equal(expanded, PathDisplayFormatter.Compact(path, expanded.Length));
        Assert.Equal(path, PathDisplayFormatter.Compact(path, path.Length));
    }

    [Theory]
    [InlineData("ErrorFilesWindow.xaml", "PathTextBlock")]
    [InlineData("MainWindow.xaml", "PathTextBlock")]
    [InlineData("JobQueueWindow.xaml", "PathTextBlock")]
    [InlineData("ContinuousSyncRegisteredTasksWindow.xaml", "PathTextBlock")]
    [InlineData("ContinuousSyncConflictWindow.xaml", "PathTextBlock")]
    [InlineData("OneWaySyncReviewWindow.xaml", "PathTextBlock")]
    [InlineData("ShellCopyConflictWindow.xaml", "PathTextBlock")]
    [InlineData("ShellOverwriteSelectionWindow.xaml", "PathTextBlock")]
    [InlineData("AgentErrorWindow.xaml", "AgentPathTextBlock")]
    [InlineData("AgentNotificationDetailWindow.xaml", "AgentPathTextBlock")]
    [InlineData("AgentQuarantineWindow.xaml", "AgentPathTextBlock")]
    [InlineData("AgentReviewWindow.xaml", "AgentPathTextBlock")]
    [InlineData("AgentRegisteredTasksWindow.xaml", "AgentPathTextBlock")]
    [InlineData("AgentTaskConflictWindow.xaml", "AgentPathTextBlock")]
    public void ReadOnlyPathListsUseAdaptivePathDisplay(string fileName, string controlName)
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == controlName
            && element.Attribute("FullPath") is not null);
    }

    [Fact]
    public void AdaptivePathDisplaysMeasureTheContainingDataGridCell()
    {
        foreach (var fileName in new[] { "PathTextBlock.cs", "AgentPathTextBlock.cs" })
        {
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
            Assert.Contains("FindParent<DataGridCell>", source, StringComparison.Ordinal);
            Assert.Contains("_hostCell.SizeChanged", source, StringComparison.Ordinal);
            Assert.Contains("hostWidth", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MainCommandPreviewShowsTheFullCommandAsATooltip()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        var command = document.Descendants(presentation + "TextBlock")
            .Single(element => element.Attribute(xaml + "Name")?.Value == "CommandPreviewTextBlock");
        Assert.Contains(command.Descendants(presentation + "Setter"), setter =>
            setter.Attribute("Property")?.Value == "ToolTip"
            && setter.Attribute("Value")?.Value == "{Binding Text, RelativeSource={RelativeSource Self}}");
        Assert.Contains(command.Descendants(presentation + "Trigger"), trigger =>
            trigger.Attribute("Property")?.Value == "Text"
            && trigger.Attribute("Value")?.Value == string.Empty
            && trigger.Descendants(presentation + "Setter").Any(setter =>
                setter.Attribute("Property")?.Value == "ToolTip"
                && setter.Attribute("Value")?.Value == "{x:Null}"));
    }
}
