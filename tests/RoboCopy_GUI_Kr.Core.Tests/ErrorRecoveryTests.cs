using RoboCopy_GUI_Kr.Core.Errors;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ErrorRecoveryTests
{
    [Theory]
    [InlineData(true, false, false, 0, true, false, true)]
    [InlineData(true, false, false, 0, false, true, true)]
    [InlineData(true, true, false, 0, true, true, false)]
    [InlineData(true, false, true, 0, true, true, false)]
    [InlineData(true, false, false, 1, true, true, false)]
    [InlineData(false, false, false, 0, true, true, false)]
    [InlineData(true, false, false, 0, false, false, false)]
    public void ErrorManagementOnlyOpensAfterQueueFinishes(bool started, bool running, bool paused,
        int waiting, bool failed, bool unresolved, bool expected)
    {
        Assert.Equal(expected, MiniUiCompletionPolicy.RequiresErrorManagement(
            started, running, paused, waiting, failed, unresolved));
    }

    [Fact]
    public void LockLookupIncludesBothExistingPathsAndDeduplicates()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "원본 공백 & [파일].dll");
        var destination = Path.Combine(root, "대상 공백 & [파일].dll");
        try
        {
            File.WriteAllText(source, "source");
            File.WriteAllText(destination, "destination");
            var item = new RobocopyErrorItem(32, RobocopyErrorCategory.Locked, source, destination, "");
            Assert.Equal(new[] { source, destination }, ErrorFileLockPaths.Collect([item, item]));
            File.Delete(source);
            Assert.Equal(new[] { destination }, ErrorFileLockPaths.Collect([item]));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
