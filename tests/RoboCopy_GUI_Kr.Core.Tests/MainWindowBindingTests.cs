using System.Xml.Linq;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class MainWindowBindingTests
{
    [Fact]
    public void FileProgressBar_UsesOneWayBindingForReadOnlyPercent()
    {
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml");
        var document = XDocument.Load(xamlPath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var valueBindings = document
            .Descendants(presentation + "ProgressBar")
            .Select(element => element.Attribute("Value")?.Value)
            .Where(value => value?.Contains("Binding Percent", StringComparison.Ordinal) == true)
            .ToArray();

        var binding = Assert.Single(valueBindings);
        Assert.Contains("Mode=OneWay", binding, StringComparison.Ordinal);
    }

    [Fact]
    public void OverallProgress_PercentTextIsOverlaidInsideProgressBarGrid()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var progressBar = document.Descendants(presentation + "ProgressBar")
            .Single(element => (string?)element.Attribute(x + "Name") == "ProgressBar");
        var progressText = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "ProgressTextBlock");

        Assert.Same(progressBar.Parent, progressText.Parent);
    }

    [Fact]
    public void OverallProgress_ProvidesPhaseLabelAndLargeNotice()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var label = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "ProgressLabelTextBlock");
        var notice = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "PhaseNoticeTextBlock");

        Assert.Equal("{StaticResource ProgressCopy}", (string?)label.Attribute("Text"));
        Assert.Equal("18", (string?)notice.Attribute("FontSize"));
        Assert.Equal("Collapsed", (string?)notice.Attribute("Visibility"));
    }

    [Fact]
    public void ExclusionPatterns_EnableExtendedDragSelectionHandlers()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ExclusionWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var list = document.Descendants(presentation + "ListBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "PatternListBox");

        Assert.Equal("Extended", (string?)list.Attribute("SelectionMode"));
        Assert.Equal("PatternListBox_PreviewMouseLeftButtonDown", (string?)list.Attribute("PreviewMouseLeftButtonDown"));
        Assert.Equal("PatternListBox_PreviewMouseMove", (string?)list.Attribute("PreviewMouseMove"));
        Assert.Equal("PatternListBox_PreviewMouseLeftButtonUp", (string?)list.Attribute("PreviewMouseLeftButtonUp"));
    }

    [Fact]
    public void MoveFailureDecision_ProvidesFourExplainedActions()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MoveFailureDecisionWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var buttons = document.Descendants(presentation + "Button").ToArray();

        Assert.Equal(4, buttons.Length);
        Assert.All(buttons, button => Assert.StartsWith("{DynamicResource MoveError", (string?)button.Attribute("ToolTip")));
        Assert.Contains(buttons, button => (string?)button.Attribute("Content") == "{DynamicResource MoveErrorContinueButton}");
        Assert.Contains(buttons, button => (string?)button.Attribute("Content") == "{DynamicResource MoveErrorResolveButton}");
        Assert.Contains(buttons, button => (string?)button.Attribute("Content") == "{DynamicResource MoveErrorStopButton}");
        Assert.Contains(buttons, button => (string?)button.Attribute("Content") == "{DynamicResource Delete}");
    }

    [Fact]
    public void OneWaySyncReview_ProvidesWholeAndPerItemDeletionChoices()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OneWaySyncReviewWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var selectAll = document.Descendants(presentation + "CheckBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "SelectAllCheckBox");
        Assert.Equal("False", (string?)selectAll.Attribute("IsThreeState"));
        var dataGrid = document.Descendants(presentation + "DataGrid").Single();
        Assert.Equal("ExtraItemsDataGrid_PreviewMouseLeftButtonDown", (string?)dataGrid.Attribute("PreviewMouseLeftButtonDown"));
        var deleteCheckBox = dataGrid.Descendants(presentation + "CheckBox").Single();
        Assert.Equal("{Binding Delete, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}", (string?)deleteCheckBox.Attribute("IsChecked"));
        Assert.Contains(document.Descendants(presentation + "Button"), button =>
            (string?)button.Attribute("Content") == "{StaticResource ApplyAndSync}");
    }

    [Fact]
    public void ContinuousSync_ProvidesAgentSettingsAndTrayTaskEditorFields()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var main = LoadMainWindow();
        Assert.Contains(main.Descendants(presentation + "ComboBoxItem"), item =>
            (string?)item.Attribute("Tag") == "ContinuousSync");

        var settings = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ContinuousSyncSettingsWindow.xaml"));
        Assert.NotNull(settings.Descendants(presentation + "TextBox")
            .SingleOrDefault(element => (string?)element.Attribute(x + "Name") == "IntervalTextBox"));
        Assert.Equal(5, settings.Descendants(presentation + "RadioButton").Count());
        var agentThreadCombo = settings.Descendants(presentation + "ComboBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "MultiThreadComboBox");
        Assert.Equal(new[] { "Recommended", "MaximumPerformance", "1", "2", "4", "8", "16", "32", "64", "128" },
            agentThreadCombo.Elements(presentation + "ComboBoxItem")
                .Select(item => (string?)item.Attribute("Tag")));
        Assert.Contains(settings.Descendants(presentation + "Button"), button =>
            (string?)button.Attribute("Content") == "{StaticResource StartRobocopyAgent}");

        var editor = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AgentTaskEditorWindow.xaml"));
        Assert.NotNull(editor.Descendants(presentation + "TextBox")
            .SingleOrDefault(element => (string?)element.Attribute(x + "Name") == "SourceTextBox"));
        Assert.NotNull(editor.Descendants(presentation + "TextBox")
            .SingleOrDefault(element => (string?)element.Attribute(x + "Name") == "DestinationTextBox"));
        Assert.Equal(5, editor.Descendants(presentation + "RadioButton").Count());
        Assert.Contains(editor.Descendants(presentation + "Button"), button =>
            (string?)button.Attribute("Content") == "{StaticResource ReevaluateAutoMt}");
        Assert.NotNull(editor.Descendants(presentation + "ComboBox")
            .SingleOrDefault(element => (string?)element.Attribute(x + "Name") == "MultiThreadComboBox"));
    }

    [Fact]
    public void PrimaryJobControls_ArePlacedInOneConfigurationRow()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var operation = document.Descendants(presentation + "ComboBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "OperationComboBox");
        var row = Assert.IsType<XElement>(operation.Parent);
        var buttonContents = row.Elements(presentation + "Button")
            .Select(element => (string?)element.Attribute("Content"))
            .ToArray();

        Assert.Contains("{StaticResource JobList}", buttonContents);
        Assert.Contains("{StaticResource Exclusions}", buttonContents);
        Assert.Contains(row.Elements(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{StaticResource Profile}");
    }

    [Fact]
    public void WorkProfileControls_EnableSaveLoadAndManagementActions()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var comboBox = document.Descendants(presentation + "ComboBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "ProfileComboBox");
        Assert.Equal("ProfileComboBox_SelectionChanged", (string?)comboBox.Attribute("SelectionChanged"));
        Assert.Contains(comboBox.Descendants(presentation + "EventSetter"), element =>
            (string?)element.Attribute("Event") == "PreviewMouseRightButtonDown"
            && (string?)element.Attribute("Handler") == "ProfileItem_PreviewMouseRightButtonDown");
        var actions = comboBox.Descendants(presentation + "MenuItem")
            .Select(element => (string?)element.Attribute("Click"))
            .ToArray();
        Assert.Contains("RenameProfile_Click", actions);
        Assert.Contains("DuplicateProfile_Click", actions);
        Assert.Contains("DeleteProfile_Click", actions);

        var saveButton = document.Descendants(presentation + "Button")
            .Single(element => (string?)element.Attribute(x + "Name") == "SaveProfileButton");
        Assert.Equal("SaveProfile_Click", (string?)saveButton.Attribute("Click"));

        var operationModes = document.Descendants(presentation + "ComboBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "OperationComboBox")
            .Elements(presentation + "ComboBoxItem")
            .Select(element => (string?)element.Attribute("Tag"))
            .ToArray();
        Assert.Contains("Copy", operationModes);
        Assert.Contains("Move", operationModes);
        Assert.Contains("UpdateSync", operationModes);
        Assert.Contains("ContinuousSync", operationModes);
    }

    [Fact]
    public void FileColumns_ExposeColumnSpecificExplorerContextMenus()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var menuHeaders = document.Descendants(presentation + "MenuItem")
            .Select(element => (string?)element.Attribute("Header"))
            .ToArray();

        Assert.Contains("{StaticResource OpenSourceLocation}", menuHeaders);
        Assert.Contains("{StaticResource OpenDestinationLocation}", menuHeaders);
    }

    [Fact]
    public void CopySummary_OverlaysTheActiveFileGrid()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var activeFiles = document.Descendants(presentation + "DataGrid")
            .Single(element => (string?)element.Attribute(x + "Name") == "FileProgressDataGrid");
        var summary = document.Descendants(presentation + "Grid")
            .Single(element => (string?)element.Attribute(x + "Name") == "CopySummaryPanel");

        Assert.Same(activeFiles.Parent, summary.Parent);
        Assert.Equal("Collapsed", (string?)summary.Attribute("Visibility"));
    }

    [Fact]
    public void CopySummary_ShowsTimeDetailsOnOneNormalSizedRow()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var names = document.Descendants(presentation + "TextBlock")
            .Select(element => (string?)element.Attribute(x + "Name"))
            .Where(name => name is not null)
            .ToArray();

        Assert.Contains("SummaryTimeTextBlock", names);
        Assert.DoesNotContain("SummaryElapsedTimeTextBlock", names);
        var summaryTime = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "SummaryTimeTextBlock");
        Assert.Null(summaryTime.Attribute("FontSize"));
        Assert.DoesNotContain(document.Descendants(presentation + "DataGridTextColumn"), column =>
            (string?)column.Attribute("Header") == "{StaticResource SummaryTimes}");
    }

    [Fact]
    public void PerformanceOptions_ExposeAutoMtReevaluationButton()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var button = document.Descendants(presentation + "Button")
            .Single(element => (string?)element.Attribute(x + "Name") == "ReevaluateAutoMtButton");

        Assert.Equal("{StaticResource ReevaluateAutoMt}", (string?)button.Attribute("Content"));
        Assert.Equal("ReevaluateAutoMt_Click", (string?)button.Attribute("Click"));
        Assert.Equal("0", (string?)button.Attribute("Margin"));

        var retryCount = document.Descendants(presentation + "TextBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "RetryCountTextBox");
        var retryWait = document.Descendants(presentation + "TextBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "RetryWaitTextBox");
        Assert.Equal("5", (string?)retryCount.Attribute("Grid.Column"));
        Assert.Equal("8", (string?)retryWait.Attribute("Grid.Column"));

        var outerGrid = Assert.IsType<XElement>(button.Parent);
        var widths = outerGrid.Element(presentation + "Grid.ColumnDefinitions")!
            .Elements(presentation + "ColumnDefinition")
            .Select(column => (string?)column.Attribute("Width"))
            .ToArray();
        Assert.Equal("*", widths[3]);
        Assert.Equal("*", widths[6]);
        Assert.Equal(widths[3], widths[6]);

        var operation = document.Descendants(presentation + "ComboBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "OperationComboBox");
        var multiThread = document.Descendants(presentation + "ComboBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "MultiThreadComboBox");
        Assert.Equal("1", (string?)operation.Attribute("Grid.Column"));
        Assert.Equal("1", (string?)multiThread.Attribute("Grid.Column"));

        var historyMenu = button.Descendants(presentation + "MenuItem").Single();
        Assert.Equal("{StaticResource MtBenchmarkHistory}", (string?)historyMenu.Attribute("Header"));
        Assert.Equal("OpenMtBenchmarkHistory_Click", (string?)historyMenu.Attribute("Click"));
    }

    [Fact]
    public void MtBenchmarkHistoryWindow_ProvidesMultiSelectionAndConfirmedDeletionAction()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MtBenchmarkHistoryWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var dataGrid = document.Descendants(presentation + "DataGrid")
            .Single(element => (string?)element.Attribute(x + "Name") == "HistoryDataGrid");
        Assert.Equal("Extended", (string?)dataGrid.Attribute("SelectionMode"));
        Assert.Contains(dataGrid.Descendants(presentation + "MenuItem"), item =>
            (string?)item.Attribute("Click") == "RemoveSelected_Click");
    }

    [Fact]
    public void ErrorFilesWindow_ProvidesMultiSelectionRetryAndLockLookup()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ErrorFilesWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var dataGrid = document.Descendants(presentation + "DataGrid")
            .Single(element => (string?)element.Attribute(x + "Name") == "ErrorDataGrid");
        Assert.Equal("Extended", (string?)dataGrid.Attribute("SelectionMode"));
        Assert.Contains(dataGrid.Descendants(presentation + "DataGridTextColumn"), column =>
            (string?)column.Attribute("Binding") == "{Binding RetryStateText}");
        Assert.Contains(dataGrid.Descendants(presentation + "DataGridTextColumn"), column =>
            (string?)column.Attribute("Binding") == "{Binding StatusText}");

        var actions = document.Descendants(presentation + "MenuItem")
            .Select(element => (string?)element.Attribute("Click"))
            .ToArray();
        Assert.Contains("RetrySelected_Click", actions);
        Assert.Contains("RetrySelectedAsAdministrator_Click", actions);
        Assert.Contains("CheckLockingProcesses_Click", actions);

        var errorCode = dataGrid.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "{Binding ErrorCode}");
        Assert.Equal("{Binding ErrorCodeToolTip}", (string?)errorCode.Attribute("ToolTip"));
        var paths = dataGrid.Descendants().Where(element => element.Name.LocalName == "PathTextBlock").ToArray();
        Assert.Contains(paths, element => (string?)element.Attribute("FullPath") == "{Binding SourcePath}");
        Assert.Contains(paths, element => (string?)element.Attribute("FullPath") == "{Binding DestinationPath}");
    }

    [Fact]
    public void LockingProcessesWindow_RequiresExplicitGracefulShutdownAction()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LockingProcessesWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var button = document.Descendants(presentation + "Button")
            .Single(element => (string?)element.Attribute(x + "Name") == "RequestShutdownButton");
        Assert.Equal("{StaticResource RequestNormalShutdown}", (string?)button.Attribute("Content"));
        Assert.Equal("RequestShutdown_Click", (string?)button.Attribute("Click"));
        Assert.Contains(document.Descendants(presentation + "MenuItem"), element =>
            (string?)element.Attribute("Click") == "RequestShutdown_Click");
        var restartableHeader = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "{StaticResource RestartableProcess}");
        Assert.Equal("{StaticResource RestartableProcessTooltip}", (string?)restartableHeader.Attribute("ToolTip"));
        Assert.DoesNotContain(document.Descendants(presentation + "Button"), element =>
            (string?)element.Attribute("Content") == "Process Killer");
    }

    [Fact]
    public void FileProgressBar_UsesFixedIndicatorTemplate()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var indicator = document.Descendants(presentation + "Border")
            .Single(element => (string?)element.Attribute(x + "Name") == "PART_Indicator");

        Assert.Equal("Left", (string?)indicator.Attribute("HorizontalAlignment"));
    }

    [Fact]
    public void ProgressColumn_UsesConsistentStretchingCellLayout()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var setters = document.Descendants(presentation + "DataGridTemplateColumn.CellStyle")
            .Descendants(presentation + "Setter")
            .ToArray();

        Assert.Contains(setters, setter =>
            (string?)setter.Attribute("Property") == "HorizontalContentAlignment"
            && (string?)setter.Attribute("Value") == "Stretch");
        Assert.Contains(setters, setter =>
            (string?)setter.Attribute("Padding") is null
            && (string?)setter.Attribute("Property") == "Padding"
            && (string?)setter.Attribute("Value") == "0");
        var progressCell = document.Descendants(presentation + "Grid")
            .Single(element => (string?)element.Attribute("Width") == "132"
                && (string?)element.Attribute("Height") == "18");
        Assert.Equal("Center", (string?)progressCell.Attribute("HorizontalAlignment"));
    }

    private static XDocument LoadMainWindow()
    {
        return XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
    }

    [Fact]
    public void ExplorerIntegrationButton_OpensConfigurationWindow()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var button = document.Descendants(presentation + "Button").Single(button =>
            (string?)button.Attribute(x + "Name") == "ExplorerIntegrationButton");

        Assert.Equal("{StaticResource ExplorerIntegration}", (string?)button.Attribute("Content"));
        Assert.Equal("ExplorerIntegration_Click", (string?)button.Attribute("Click"));
        Assert.Equal("Right", (string?)button.Attribute("HorizontalAlignment"));
        Assert.Equal("Bottom", (string?)button.Attribute("VerticalAlignment"));
        Assert.Contains(button.Ancestors(presentation + "Border"), border =>
            (string?)border.Attribute("Grid.Row") == "0");
    }

    [Fact]
    public void ShellActionPrompt_ProvidesThreeExplicitChoices()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ShellActionPromptWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var clickHandlers = document.Descendants(presentation + "Button")
            .Select(button => (string?)button.Attribute("Click"))
            .ToArray();

        Assert.Contains("StartWithDefaults_Click", clickHandlers);
        Assert.Contains("Configure_Click", clickHandlers);
        Assert.Contains("Cancel_Click", clickHandlers);
    }

    [Fact]
    public void ShellCopyConflictWindow_GroupsFilesAndProvidesThreeDecisions()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ShellCopyConflictWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        Assert.Single(document.Descendants(presentation + "TreeView"));
        var clickHandlers = document.Descendants(presentation + "Button")
            .Select(button => (string?)button.Attribute("Click"))
            .ToArray();
        Assert.Contains("SkipAll_Click", clickHandlers);
        Assert.Contains("OverwriteAll_Click", clickHandlers);
        Assert.Contains("KeepBothAll_Click", clickHandlers);
        Assert.Contains(document.Descendants(presentation + "ComboBox"), element =>
            (string?)element.Attribute("SelectedValue") == "{Binding Decision, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}");
    }

    [Fact]
    public void ShellConflictScanWindow_ShowsAutomaticProgressFeedback()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ShellConflictScanProgressWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        Assert.Single(document.Descendants(presentation + "ProgressBar"));
        Assert.Contains(document.Descendants(presentation + "TextBlock"), element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "ScanStatusTextBlock"));
    }

    [Fact]
    public void ShellOverwriteSelectionWindow_RequiresOneFinalCandidatePerGroup()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ShellOverwriteSelectionWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        Assert.Contains(document.Descendants(presentation + "DataGrid"), element =>
            (string?)element.Attribute("SelectedItem") == "{Binding SelectedCandidate, Mode=TwoWay}");
        Assert.Empty(document.Descendants(presentation + "ComboBox"));
        var headers = document.Descendants()
            .Where(element => element.Name == presentation + "DataGridTextColumn"
                              || element.Name == presentation + "DataGridTemplateColumn")
            .Select(element => (string?)element.Attribute("Header"))
            .ToArray();
        Assert.Contains("{StaticResource FileName}", headers);
        Assert.Contains("{StaticResource Size}", headers);
        Assert.Contains("{StaticResource ModifiedDate}", headers);
        Assert.Contains("{StaticResource CreatedDate}", headers);
        Assert.Contains("{StaticResource Address}", headers);
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "PathTextBlock"
            && (string?)element.Attribute("FullPath") == "{Binding FullAddress}");
        var dataGrid = document.Descendants(presentation + "DataGrid").First(element =>
            (string?)element.Attribute("SelectedItem") == "{Binding SelectedCandidate, Mode=TwoWay}");
        Assert.Equal("All", (string?)dataGrid.Attribute("GridLinesVisibility"));
        Assert.Contains(dataGrid.Descendants(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "HorizontalContentAlignment"
            && (string?)setter.Attribute("Value") == "Center");
    }

    [Fact]
    public void ShellPastePrompt_KeepsApplicationAliveUntilOperationWindowIsShown()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "App.xaml.cs"));
        var reviewIndex = source.IndexOf("await ReviewShellConflictsAsync(operationRequests, scanWindow)", StringComparison.Ordinal);
        var promptIndex = source.IndexOf("var operationWindow = new ShellActionPromptWindow();", StringComparison.Ordinal);
        var explicitShutdownIndex = source.LastIndexOf(
            "ShutdownMode = ShutdownMode.OnExplicitShutdown;",
            promptIndex,
            StringComparison.Ordinal);
        var mainWindowIndex = source.IndexOf("MainWindow = operationMainWindow;", promptIndex, StringComparison.Ordinal);
        var mainWindowShutdownIndex = source.IndexOf(
            "ShutdownMode = ShutdownMode.OnMainWindowClose;",
            mainWindowIndex,
            StringComparison.Ordinal);

        Assert.True(explicitShutdownIndex >= 0 && explicitShutdownIndex < promptIndex);
        Assert.True(reviewIndex >= 0 && reviewIndex < promptIndex);
        Assert.True(mainWindowIndex > promptIndex);
        Assert.True(mainWindowShutdownIndex > mainWindowIndex);
    }

    [Fact]
    public void ExplorerIntegrationWindow_IsTallEnoughForWindows11Notice()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ExplorerIntegrationWindow.xaml"));

        Assert.True(double.Parse(document.Root!.Attribute("Height")!.Value, System.Globalization.CultureInfo.InvariantCulture) >= 280);
        Assert.True(double.Parse(document.Root.Attribute("MinHeight")!.Value, System.Globalization.CultureInfo.InvariantCulture) >= 260);
    }
}
