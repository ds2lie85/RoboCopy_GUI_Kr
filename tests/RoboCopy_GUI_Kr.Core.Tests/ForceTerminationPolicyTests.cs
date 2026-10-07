using RoboCopy_GUI_Kr.Core.Errors;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ForceTerminationPolicyTests
{
    [Theory]
    [InlineData(0, 100, true)]
    [InlineData(4, 100, true)]
    [InlineData(100, 100, true)]
    [InlineData(101, 100, false)]
    public void ProtectsSystemAndCurrentProcess(int processId, int currentProcessId, bool expected) =>
        Assert.Equal(expected, ForceTerminationPolicy.IsProtected(processId, currentProcessId));
}
