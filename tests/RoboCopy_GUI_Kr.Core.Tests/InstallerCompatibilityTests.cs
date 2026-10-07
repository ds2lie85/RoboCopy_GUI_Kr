namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class InstallerCompatibilityTests
{
    [Fact]
    public void Installer_AllowsWindows10Version1607OrLater()
    {
        var script = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Install.ps1"));

        Assert.Contains("$minimumWindowsBuild = 14393", script, StringComparison.Ordinal);
        Assert.Contains("Windows 10 버전 1607", script, StringComparison.Ordinal);
        Assert.DoesNotContain("19041", script, StringComparison.Ordinal);
    }
}
