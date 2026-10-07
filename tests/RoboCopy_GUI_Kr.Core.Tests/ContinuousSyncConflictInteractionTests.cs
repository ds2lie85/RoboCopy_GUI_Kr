namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ContinuousSyncConflictInteractionTests
{
    [Fact]
    public void CancelledSettingsKeepsConflictWindowOpen()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "ContinuousSyncConflictWindow.xaml.cs"));
        var handlerStart = source.IndexOf("private void ChangeSettings_Click", StringComparison.Ordinal);
        var nextHandler = source.IndexOf("private void ShowRegistered_Click", handlerStart, StringComparison.Ordinal);
        var handler = source[handlerStart..nextHandler];

        Assert.Contains("ConfigureRequested?.Invoke(this, request);", handler, StringComparison.Ordinal);
        Assert.Contains("if (request.Applied)", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("DialogResult = true;", handler[..handler.IndexOf("if (request.Applied)", StringComparison.Ordinal)], StringComparison.Ordinal);
    }
}
