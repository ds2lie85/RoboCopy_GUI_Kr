using System.Diagnostics;
using System.Text.Json;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class InstallerDeveloperModeTests
{
    [Theory]
    [InlineData("Unchecked", 0, false, false, false)]
    [InlineData("AlreadyEnabled", 0, true, true, false)]
    [InlineData("Selected", 1, true, true, false)]
    [InlineData("Canceled", 1, false, false, true)]
    [InlineData("MenuDisabled", 0, false, false, false)]
    [InlineData("Windows10", 0, false, false, false)]
    public async Task Selection_ControlsElevationAndRegistration(string scenario, int elevations, bool enabled, bool registered, bool failed)
    {
        if (!OperatingSystem.IsWindows()) return;
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "installer-test 한글 [공백] & " + Guid.NewGuid().ToString("N"));
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        try
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File",
                Path.Combine(fixtures, "Test-InstallerDeveloperMode.ps1"), "-InstallerPath", Path.Combine(fixtures, "Install.ps1"),
                "-ResourcePath", Path.Combine(fixtures, "InstallerStrings.ko-KR.psd1"), "-FixtureRoot", fixtureRoot, "-Scenario", scenario })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            var error = await errorTask;
            Assert.True(process.ExitCode == 0, error);
            using var json = JsonDocument.Parse(output.Trim());
            Assert.Equal(elevations, json.RootElement.GetProperty("Elevations").GetInt32());
            Assert.Equal(enabled, json.RootElement.GetProperty("Enabled").GetBoolean());
            Assert.Equal(registered, json.RootElement.GetProperty("Registered").GetBoolean());
            Assert.Equal(failed, json.RootElement.GetProperty("Failed").GetBoolean());
        }
        finally { if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, true); }
    }
}
