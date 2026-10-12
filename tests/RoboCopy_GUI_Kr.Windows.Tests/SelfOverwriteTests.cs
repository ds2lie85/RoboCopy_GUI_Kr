using RoboCopy_GUI_Kr.Core.Errors;
using System.IO;

namespace RoboCopy_GUI_Kr.Windows.Tests;

public sealed class SelfOverwriteTests
{
    [Fact]
    public void LoadedManagedAssemblyIsDetectedEvenWhenRestartManagerOmitsCaller()
    {
        var path = typeof(RestartManagerService).Assembly.Location;
        Assert.True(RestartManagerService.IsCurrentProcessUsingFile(path));
        Assert.False(RestartManagerService.IsCurrentProcessUsingFile(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll")));
        var processes = new RestartManagerService().GetLockingProcesses([path]);
        Assert.Contains(processes, process => process.ProcessId == Environment.ProcessId);
        Assert.True(ForceTerminationPolicy.IsProtected(Environment.ProcessId, Environment.ProcessId));
    }
}
