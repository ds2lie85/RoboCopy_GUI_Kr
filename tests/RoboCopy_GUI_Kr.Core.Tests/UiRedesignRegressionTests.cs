using System.Xml.Linq;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class UiRedesignRegressionTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("MainApp.xaml")]
    [InlineData("AgentApp.xaml")]
    public void ThemeSeparatesWindowCardsAndInnerPanels(string fileName)
    {
        var document = Load(fileName);
        var resources = document.Descendants()
            .Where(element => element.Attribute(Xaml + "Key") is not null)
            .ToDictionary(element => element.Attribute(Xaml + "Key")!.Value, element => element);

        static string Color(IReadOnlyDictionary<string, XElement> values, string key) =>
            values[key].Attribute("Color")?.Value ?? string.Empty;

        Assert.Equal("#E9EFF7", Color(resources, "WindowBrush"));
        Assert.Equal("#FFFFFF", Color(resources, "PanelBrush"));
        Assert.Equal("#F7F9FC", Color(resources, "SurfaceAltBrush"));
        Assert.Equal("#C8D5E6", Color(resources, "CardBorderBrush"));
        Assert.NotEqual(Color(resources, "WindowBrush"), Color(resources, "SurfaceAltBrush"));
        Assert.NotEqual(Color(resources, "PanelBrush"), Color(resources, "SurfaceAltBrush"));
    }

    [Fact]
    public void MainHeaderUsesDarkBrandGradientAndReadableText()
    {
        var app = Load("MainApp.xaml");
        var headerBrush = app.Descendants(Presentation + "LinearGradientBrush")
            .Single(element => element.Attribute(Xaml + "Key")?.Value == "HeaderBrush");
        var colors = headerBrush.Elements(Presentation + "GradientStop")
            .Select(element => element.Attribute("Color")?.Value)
            .ToArray();

        Assert.Equal(["#FFFFFF", "#EFF6FF", "#2563EB", "#1D4ED8"], colors);

        var mainWindow = Load("MainWindow.xaml");
        Assert.Contains(mainWindow.Descendants(Presentation + "TextBlock"), element =>
            element.Attribute("Text")?.Value == "{StaticResource AppTitle}" &&
            element.Attribute("Foreground")?.Value == "#102A56");
        var rhythm = mainWindow.Descendants(Presentation + "Viewbox").Single(element =>
            element.Attribute(Xaml + "Name")?.Value == "DigitalRhythmVisual");
        Assert.Equal("335,0,-20,-16", rhythm.Attribute("Margin")?.Value);
        Assert.Equal("0.64", rhythm.Attribute("Opacity")?.Value);
        Assert.Contains(rhythm.Descendants(Presentation + "Path"), path =>
            path.Attribute("Fill")?.Value == "White");
        Assert.DoesNotContain(rhythm.Descendants(), element =>
            element.Attribute("Fill")?.Value == "#2563EB");
        Assert.Contains(rhythm.Descendants(Presentation + "Path"), path =>
            path.Attribute(Xaml + "Name")?.Value == "DigitalRhythmEndingRise" &&
            path.Attribute("Data")?.Value.Contains("M666,2", StringComparison.Ordinal) == true);

        var explorerButton = mainWindow.Descendants(Presentation + "Button").Single(element =>
            element.Attribute(Xaml + "Name")?.Value == "ExplorerIntegrationButton");
        Assert.Equal("#B31D4ED8", explorerButton.Attribute("Background")?.Value);
        Assert.Equal("#D9FFFFFF", explorerButton.Attribute("BorderBrush")?.Value);
    }

    [Theory]
    [InlineData("ExclusionWindow.xaml")]
    [InlineData("AgentExclusionWindow.xaml")]
    public void FilePatternAreaIsTallerAndSupportsExtendedSelection(string fileName)
    {
        var document = Load(fileName);
        var list = document.Descendants(Presentation + "ListBox")
            .Single(element => element.Attribute(Xaml + "Name")?.Value == "PatternListBox");

        Assert.Equal("700", document.Root?.Attribute("Height")?.Value);
        Assert.Equal("Extended", list.Attribute("SelectionMode")?.Value);
        Assert.Equal("100", list.Attribute("MinHeight")?.Value);
        Assert.Contains(document.Descendants(Presentation + "RowDefinition"), row =>
            row.Attribute("Height")?.Value == "190");
    }

    [Theory]
    [InlineData("ExclusionWindow.xaml.cs")]
    [InlineData("AgentExclusionWindow.xaml.cs")]
    public void FilePatternDragSelectionCapturesAcrossItemBoundaries(string fileName)
    {
        var source = File.ReadAllText(FixturePath(fileName));
        Assert.Contains("CaptureMode.SubTree", source, StringComparison.Ordinal);
        Assert.Contains("FindPatternItemAt", source, StringComparison.Ordinal);
        Assert.Contains("ReleaseMouseCapture", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ContinuousSyncSettingsWindow.xaml")]
    [InlineData("AgentTaskEditorWindow.xaml")]
    public void ContinuousSyncEditorFitsAndShowsCompleteScheduleTimes(string fileName)
    {
        var document = Load(fileName);
        Assert.Equal("780", document.Root?.Attribute("Height")?.Value);
        Assert.Equal("700", document.Root?.Attribute("MinHeight")?.Value);

        foreach (var name in new[] { "ScheduleStartTextBox", "ScheduleEndTextBox" })
        {
            var timeBox = document.Descendants(Presentation + "TextBox")
                .Single(element => element.Attribute(Xaml + "Name")?.Value == name);
            Assert.Equal("88", timeBox.Attribute("Width")?.Value);
            Assert.Equal("Center", timeBox.Attribute("HorizontalContentAlignment")?.Value);
        }
    }

    private static XDocument Load(string fileName) => XDocument.Load(FixturePath(fileName));

    private static string FixturePath(string fileName) => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        fileName);
}
