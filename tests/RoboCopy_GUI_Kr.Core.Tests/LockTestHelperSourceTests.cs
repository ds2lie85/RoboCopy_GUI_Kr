namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class LockTestHelperSourceTests
{
    [Fact]
    public void Helper_UsesExclusiveLockAndRejectsNormalClose()
    {
        string source = ReadFixture("LockTestHelper.MainWindow.xaml.cs");
        string appSource = ReadFixture("LockTestHelper.App.xaml.cs");

        Assert.Contains("FileShare.None", source, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true", source, StringComparison.Ordinal);
        Assert.Contains("if (!_allowClose)", source, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMinutes(10)", source, StringComparison.Ordinal);
        Assert.Contains("OnSessionEnding", appSource, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true", appSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Helper_ProvidesExplicitSafeReleaseAndLocalizedUi()
    {
        string source = ReadFixture("LockTestHelper.MainWindow.xaml.cs");
        string xaml = ReadFixture("LockTestHelper.MainWindow.xaml");
        string resources = ReadFixture("LockTestHelper.Strings.ko-KR.xaml");

        Assert.Contains("ReleaseAndExitButton_Click", xaml, StringComparison.Ordinal);
        Assert.Contains("ReleaseLock();", source, StringComparison.Ordinal);
        Assert.Contains("잠금 해제 후 종료", resources, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"잠금 해제 후 종료\"", xaml, StringComparison.Ordinal);
    }

    private static string ReadFixture(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return File.ReadAllText(path);
    }
}
