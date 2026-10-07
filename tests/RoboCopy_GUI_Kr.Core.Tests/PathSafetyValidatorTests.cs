using RoboCopy_GUI_Kr.Core.Validation;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class PathSafetyValidatorTests
{
    [Fact]
    public void ValidateCopyDirectories_RejectsMissingSource()
    {
        var errors = PathSafetyValidator.ValidateCopyDirectories(@"Z:\folder-that-does-not-exist", @"Y:\backup");

        Assert.Contains("SourceNotFound", errors);
    }

    [Fact]
    public void ValidateCopyDirectories_RejectsSameDirectory()
    {
        var existingDirectory = Path.GetTempPath();

        var errors = PathSafetyValidator.ValidateCopyDirectories(existingDirectory, existingDirectory);

        Assert.Contains("SameDirectory", errors);
    }
}
