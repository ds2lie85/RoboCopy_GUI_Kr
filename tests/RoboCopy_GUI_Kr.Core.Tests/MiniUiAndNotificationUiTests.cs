using System.Xml.Linq;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class MiniUiAndNotificationUiTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void MainWindowProvidesMiniUiButtonBesideExplorerIntegration()
    {
        var document = Load("MainWindow.xaml");
        var buttons = document.Descendants(Presentation + "Button").ToArray();
        Assert.Contains(buttons, item => item.Attribute(Xaml + "Name")?.Value == "MiniUiButton");
        Assert.Contains(buttons, item => item.Attribute(Xaml + "Name")?.Value == "ExplorerIntegrationButton");
    }

    [Fact]
    public void MiniUiProvidesProgressPauseStopAndAdvancedSettings()
    {
        var document = Load("MiniCopyWindow.xaml");
        Assert.Contains(document.Descendants(Presentation + "ProgressBar"), item => item.Attribute(Xaml + "Name")?.Value == "MiniProgressBar");
        Assert.Contains(document.Descendants(Presentation + "Button"), item => item.Attribute("Click")?.Value == "PauseResume_Click");
        Assert.Contains(document.Descendants(Presentation + "Button"), item => item.Attribute("Click")?.Value == "Stop_Click");
        Assert.Contains(document.Descendants(Presentation + "Button"), item => item.Attribute("Content")?.Value == "{StaticResource MiniUiAdvancedSettings}");
        Assert.Contains(document.Descendants(Presentation + "Path"), item => item.Attribute("Fill")?.Value == "White");
        Assert.True(double.Parse(document.Root!.Attribute("Height")!.Value) >= 270);
        Assert.Contains(document.Descendants(Presentation + "Viewbox"), item => item.Attribute("HorizontalAlignment")?.Value == "Right"
            && item.Attribute("VerticalAlignment")?.Value == "Bottom"
            && item.Attribute("Stretch")?.Value == "Fill");

        var source = File.ReadAllText(FixturePath("MiniCopyWindow.xaml.cs"));
        Assert.Contains("state.CompletedFiles", source, StringComparison.Ordinal);
        Assert.Contains("MiniUiRunningStateFormat", source, StringComparison.Ordinal);
        Assert.Contains("if (!state.IsConfigured)", source, StringComparison.Ordinal);
        Assert.Contains("MiniUiWaitingForSettings", source, StringComparison.Ordinal);
        Assert.Contains("RouteTextBlock.Text = string.Empty", source, StringComparison.Ordinal);
        Assert.Contains("ProgressStatusTextBlock.Text = string.Empty", source, StringComparison.Ordinal);
        Assert.Contains("_mainWindow.CloseFromMiniUi()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellDefaultsSelectMiniUiAndConfigurationKeepsMainUi()
    {
        var source = File.ReadAllText(FixturePath("App.xaml.cs"));
        Assert.Contains("preferMiniUi: operationWindow.Decision == ShellActionDecision.StartWithDefaults", source, StringComparison.Ordinal);
        Assert.Contains("await operationMainWindow.StartWithMiniUiAsync()", source, StringComparison.Ordinal);
        var mainSource = File.ReadAllText(FixturePath("MainWindow.xaml.cs"));
        Assert.Contains("internal async Task StartWithMiniUiAsync()", mainSource, StringComparison.Ordinal);
        Assert.Contains("FindCommonDirectory", mainSource, StringComparison.Ordinal);
        Assert.Contains("CountMiniBatchWorkloadAsync", mainSource, StringComparison.Ordinal);
        Assert.Contains("_miniProcessedWeightBeforeCurrent", mainSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentTrayAndNotificationDetailsAreConnected()
    {
        var source = File.ReadAllText(FixturePath("AgentApp.xaml.cs"));
        Assert.Contains("Text(\"AgentNotificationList\")", source, StringComparison.Ordinal);
        Assert.Contains("BalloonTipClicked", source, StringComparison.Ordinal);
        Assert.Contains("ShowSyncNotification(notification)", source, StringComparison.Ordinal);

        var list = Load("AgentNotificationListWindow.xaml");
        Assert.Contains(list.Descendants(Presentation + "DataGrid"), item => item.Attribute("MouseDoubleClick")?.Value == "NotificationDataGrid_MouseDoubleClick");
        Assert.Contains(list.Descendants(Presentation + "DataGridTextColumn"), item => item.Attribute("Binding")?.Value == "{Binding Target}");
        Assert.Contains(list.Descendants(Presentation + "Button"), item => item.Attribute("Click")?.Value == "Close_Click");
        var listSource = File.ReadAllText(FixturePath("AgentNotificationListWindow.xaml.cs"));
        Assert.Contains("AgentNotificationTargetFormat", listSource, StringComparison.Ordinal);
        Assert.Contains("AgentNotificationDetailWindow.Open(row.Notification, this)", listSource, StringComparison.Ordinal);
        var detail = Load("AgentNotificationDetailWindow.xaml");
        Assert.Contains(detail.Descendants(), item => item.Name.LocalName == "AgentPathTextBlock"
            && item.Attribute("FullPath")?.Value == "{Binding RelativePath}");
        Assert.Contains(detail.Descendants(Presentation + "MenuItem"), item => item.Attribute("Click")?.Value == "Open_Click");
        Assert.Contains(detail.Descendants(Presentation + "MenuItem"), item => item.Attribute("Click")?.Value == "OpenLocation_Click");
        Assert.Contains(detail.Descendants(Presentation + "Button"), item => item.Attribute("Click")?.Value == "Close_Click");
        var detailSource = File.ReadAllText(FixturePath("AgentNotificationDetailWindow.xaml.cs"));
        Assert.Contains("private void Close_Click", detailSource, StringComparison.Ordinal);
        Assert.Contains("public static AgentNotificationDetailWindow Open", detailSource, StringComparison.Ordinal);
        Assert.Contains("AgentNotificationDetailWindow.Open(notification)", source, StringComparison.Ordinal);

        var toastSource = File.ReadAllText(FixturePath("AgentSyncToastWindow.xaml.cs"));
        Assert.Contains("TimeSpan.FromSeconds(5)", toastSource, StringComparison.Ordinal);
        var toast = Load("AgentSyncToastWindow.xaml");
        Assert.Equal("True", toast.Root?.Attribute("Topmost")?.Value);
    }

    [Fact]
    public void ContinuousSyncDeletionDeclineReturnsToSettings()
    {
        var workflow = File.ReadAllText(FixturePath("ContinuousSyncWorkflow.cs"));
        Assert.Contains("existing = edited;", workflow, StringComparison.Ordinal);
        Assert.Contains("current = edited;", workflow, StringComparison.Ordinal);

        var settings = Load("ContinuousSyncSettingsWindow.xaml");
        var policies = settings.Descendants(Presentation + "RadioButton")
            .Where(item => item.Attribute("GroupName")?.Value == "DestinationOnlyPolicy")
            .ToArray();
        Assert.Equal(3, policies.Length);
        Assert.All(policies, item => Assert.Contains("Tooltip", item.Attribute("ToolTip")?.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new[] { null, "1", "2" }, policies.Select(item => item.Attribute("Grid.Column")?.Value).ToArray());
        var runConditions = settings.Descendants(Presentation + "CheckBox")
            .Where(item => new[] { "PauseOnBatteryCheckBox", "PauseOnMeteredCheckBox", "QuarantineCheckBox" }
                .Contains(item.Attribute(Xaml + "Name")?.Value))
            .ToArray();
        Assert.Equal(3, runConditions.Length);
        Assert.All(runConditions, item => Assert.Equal("12", item.Parent?.Attribute("Grid.Row")?.Value));

        var resources = Load("AgentStrings.ko-KR.xaml");
        var strings = resources.Root!.Elements().ToDictionary(
            item => item.Attribute(Xaml + "Key")?.Value ?? string.Empty,
            item => item.Value);
        Assert.Equal("비허용(기본값)", strings["DestinationOnlyDelete"]);
        Assert.Equal("허용", strings["DestinationOnlyAllow"]);
        Assert.Equal("사용자 확인", strings["DestinationOnlyAsk"]);
    }

    private static XDocument Load(string fileName) => XDocument.Load(FixturePath(fileName));
    private static string FixturePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
}
