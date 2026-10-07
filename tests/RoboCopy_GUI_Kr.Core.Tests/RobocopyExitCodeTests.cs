using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class RobocopyExitCodeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void Interpret_CodesBelowEightAreSuccessful(int exitCode)
    {
        Assert.True(RobocopyExitCodeInterpreter.Interpret(exitCode).IsSuccess);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(24)]
    public void Interpret_CodesWithFailureBitsAreNotSuccessful(int exitCode)
    {
        Assert.False(RobocopyExitCodeInterpreter.Interpret(exitCode).IsSuccess);
    }

    [Fact]
    public void Interpret_SeparatesCombinedFlags()
    {
        var result = RobocopyExitCodeInterpreter.Interpret(11);

        Assert.Contains(RobocopyExitFlag.FilesCopied, result.Flags);
        Assert.Contains(RobocopyExitFlag.ExtraFilesDetected, result.Flags);
        Assert.Contains(RobocopyExitFlag.CopyFailures, result.Flags);
    }
}
