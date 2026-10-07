using System.Xml.Linq;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ForceTerminationUiTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("LockingProcessesWindow.xaml")]
    [InlineData("AgentErrorWindow.xaml")]
    public void ForceTerminateControlsAreHiddenByDefault(string fileName)
    {
        var document = Load(fileName);
        var controls = document.Descendants()
            .Where(element => element.Attribute(Xaml + "Name")?.Value is "ForceTerminateButton" or "ForceTerminateMenuItem")
            .ToArray();

        Assert.Equal(2, controls.Length);
        Assert.All(controls, control => Assert.Equal("Collapsed", control.Attribute("Visibility")?.Value));
    }

    [Theory]
    [InlineData("ForceTerminateWarningWindow.xaml")]
    [InlineData("AgentForceTerminateWarningWindow.xaml")]
    public void ForceTerminateWarningUsesRedVisualTreatment(string fileName)
    {
        var document = Load(fileName);
        Assert.Contains(document.Descendants(Presentation + "Border"), border =>
            string.Equals(border.Attribute("BorderBrush")?.Value, "#B71C1C", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(document.Descendants(Presentation + "TextBlock"), text =>
            string.Equals(text.Attribute("Text")?.Value, "{StaticResource ForceTerminateWarning}", StringComparison.Ordinal));
    }

    private static XDocument Load(string fileName) => XDocument.Load(Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        fileName));
}
