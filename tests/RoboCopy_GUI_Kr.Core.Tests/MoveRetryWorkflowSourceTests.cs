namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class MoveRetryWorkflowSourceTests
{
    [Fact]
    public void MoveFailureDecisions_OpenErrorManagerAndSuccessfulMoveUsesMoveStatus()
    {
        var source = ReadFixture("MainWindow.xaml.cs");

        Assert.Contains("case MoveFailureDecision.DeferUntilAllResolved:", source, StringComparison.Ordinal);
        Assert.Contains("() => ErrorFiles_Click(this, new RoutedEventArgs())", source, StringComparison.Ordinal);
        Assert.Contains(
            "UpdateRetryState(contextRow, ErrorFileRetryState.Resolved, \"ErrorStatusMoveCompleted\")",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "UpdateRetryState(row, ErrorFileRetryState.Resolved, \"ErrorStatusMoveCompleted\")",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerCopyAndPasteMenusAreBothRequestedAtTop()
    {
        var source = ReadFixture("ExplorerContextMenuService.cs");

        Assert.Equal(2, CountOccurrences(source, "SetValue(\"Position\", \"Top\")"));
    }

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }
}
