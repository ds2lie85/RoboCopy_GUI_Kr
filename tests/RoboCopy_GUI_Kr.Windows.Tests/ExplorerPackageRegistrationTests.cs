using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

namespace RoboCopy_GUI_Kr.Windows.Tests;

public sealed class ExplorerPackageRegistrationTests
{
    [Windows11Fact]
    public Task InstalledIdentity_IsSeparate() => VerifyAsync(unregister: false);

    [Fact]
    public Task UnregisterIsScoped() => VerifyAsync(unregister: true);

    private async Task VerifyAsync(bool unregister)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "ExplorerPackage");
        var root = Path.Combine(AppContext.BaseDirectory, "package-test 한글 [공백] & " + Guid.NewGuid().ToString("N"));
        try
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File",
                Path.Combine(AppContext.BaseDirectory, "Test-ExplorerPackageRegistration.ps1"), "-PackageSource", source, "-FixtureRoot", root })
                start.ArgumentList.Add(argument);
            if (unregister) start.ArgumentList.Add("-Unregister");
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            await output;
            Assert.True(process.ExitCode == 0, await error);
            if (unregister)
            {
                Assert.Equal(new[] { "owned" }, File.ReadAllLines(Path.Combine(root, "removed.txt")));
            }
            else
            {
                XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
                var manifest = XDocument.Load(Path.Combine(root, "ExplorerPackage", "AppxManifest.Installed.xml"));
                Assert.Equal("RoboCopyGUIKr.Explorer", (string?)manifest.Root!.Element(ns + "Identity")!.Attribute("Name"));
                var ids = manifest.Descendants().Attributes().Where(attribute => attribute.Name == "Clsid").Select(attribute => attribute.Value);
                Assert.Equal(3, ids.Count());
                Assert.Equal(new[] { ShellExplorerCommand.InstalledClassId, ShellExplorerCommand.InstalledClassId,
                    ShellExplorerCommand.InstalledPasteClassId }, ids);
                Assert.Contains(ShellExplorerCommand.ClassId, File.ReadAllText(Path.Combine(root, "ExplorerPackage", "AppxManifest.xml")));
                Assert.True(File.Exists(Path.Combine(root, "registration.json")));
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

public sealed class Windows11FactAttribute : FactAttribute
{
    public Windows11FactAttribute()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            Skip = "Windows 11 package registration requires build 22000 or later.";
    }
}
