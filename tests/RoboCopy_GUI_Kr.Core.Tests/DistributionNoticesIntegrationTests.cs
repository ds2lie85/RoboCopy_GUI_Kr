using System.Diagnostics;
using System.Text.Json;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class DistributionNoticesIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        $"RoboCopy notices 한글 [특수] & {Guid.NewGuid():N}");

    [Fact]
    public void Collect_PreservesExactRuntimeVersionAndOriginalContentsWithSpecialPaths()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (publish, cache) = CreateFixture();
        var result = RunCollector(publish, cache);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Equal("core license 원문", File.ReadAllText(Path.Combine(publish,
            "third-party", "microsoft.netcore.app.runtime.win-x64", "8.0.31", "LICENSE.TXT")));
        Assert.Equal("third-party notice 원문", File.ReadAllText(Path.Combine(publish,
            "third-party", "microsoft.netcore.app.runtime.win-x64", "8.0.31", "THIRD-PARTY-NOTICES.TXT")));
        Assert.Equal("desktop license 원문", File.ReadAllText(Path.Combine(publish,
            "third-party", "microsoft.windowsdesktop.app.runtime.win-x64", "8.0.31", "LICENSE")));
        Assert.False(Directory.Exists(Path.Combine(publish, "third-party",
            "microsoft.netcore.app.runtime.win-x64", "8.0.30")));
        foreach (var document in new[] { "LICENSE", "README.md", "THIRD_PARTY_NOTICES.md" })
        {
            Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", document)),
                File.ReadAllBytes(Path.Combine(publish, document)));
        }
    }

    [Theory]
    [InlineData("LICENSE.TXT")]
    [InlineData("THIRD-PARTY-NOTICES.TXT")]
    public void Collect_MissingRequiredNoticeFailsBeforeCopying(string missingFile)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (publish, cache) = CreateFixture();
        File.Delete(Path.Combine(cache, "microsoft.netcore.app.runtime.win-x64", "8.0.31", missingFile));

        var result = RunCollector(publish, cache);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("missing", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(publish, "LICENSE")));
        Assert.False(Directory.Exists(Path.Combine(publish, "third-party")));
    }

    [Fact]
    public void Collect_RejectsManifestWithoutBundledRuntime()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (publish, cache) = CreateFixture();
        File.WriteAllText(Path.Combine(publish, "RoboCopy_GUI_Kr.deps.json"),
            "{\"libraries\":{\"RoboCopy_GUI_Kr/1.0.0\":{}}}");

        var result = RunCollector(publish, cache);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("self-contained", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(publish, "LICENSE")));
    }

    private (string Publish, string Cache) CreateFixture()
    {
        var publish = Directory.CreateDirectory(Path.Combine(_root, "배포 폴더")).FullName;
        var cache = Directory.CreateDirectory(Path.Combine(_root, "캐시 폴더")).FullName;
        var libraries = new Dictionary<string, object>
        {
            ["runtimepack.Microsoft.NETCore.App.Runtime.win-x64/8.0.31"] = new { },
            ["runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/8.0.31"] = new { },
            ["RoboCopy_GUI_Kr/1.0.0"] = new { },
        };
        File.WriteAllText(Path.Combine(publish, "RoboCopy_GUI_Kr.deps.json"),
            JsonSerializer.Serialize(new { libraries }));
        var core = Directory.CreateDirectory(Path.Combine(cache,
            "microsoft.netcore.app.runtime.win-x64", "8.0.31")).FullName;
        File.WriteAllText(Path.Combine(core, "LICENSE.TXT"), "core license 원문");
        File.WriteAllText(Path.Combine(core, "THIRD-PARTY-NOTICES.TXT"), "third-party notice 원문");
        var other = Directory.CreateDirectory(Path.Combine(cache,
            "microsoft.netcore.app.runtime.win-x64", "8.0.30")).FullName;
        File.WriteAllText(Path.Combine(other, "LICENSE.TXT"), "wrong version");
        var desktop = Directory.CreateDirectory(Path.Combine(cache,
            "microsoft.windowsdesktop.app.runtime.win-x64", "8.0.31")).FullName;
        File.WriteAllText(Path.Combine(desktop, "LICENSE"), "desktop license 원문");
        return (publish, cache);
    }

    private static (int ExitCode, string Output) RunCollector(string publish, string cache)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "installer", "Copy-DistributionNotices.ps1"),
            "-PublishDirectory", publish, "-PackageCacheDirectory", cache })
        {
            startInfo.ArgumentList.Add(argument);
        }
        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(30_000), "Notice collector exceeded its timeout.");
        return (process.ExitCode, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
