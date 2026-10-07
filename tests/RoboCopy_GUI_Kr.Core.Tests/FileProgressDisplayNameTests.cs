using RoboCopy_GUI_Kr.Core.Progress;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class FileProgressDisplayNameTests
{
    [Fact]
    public void Format_AddsParentContextForNugetPlaceholderFile()
    {
        var displayName = FileProgressDisplayName.Format(
            "_._",
            @"C:\원본 폴더\netstandard1.0\_._");

        Assert.Equal(@"netstandard1.0\_._", displayName);
    }

    [Fact]
    public void Format_AddsParentContextForRegularFileWithKoreanAndSpecialCharacters()
    {
        var displayName = FileProgressDisplayName.Format(
            "보고서 #1.txt",
            @"C:\원본 폴더\보고서 #1.txt");

        Assert.Equal(@"원본 폴더\보고서 #1.txt", displayName);
    }
}
