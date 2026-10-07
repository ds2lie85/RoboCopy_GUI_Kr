using RoboCopy_GUI_Kr.Core.Paths;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ShellSelectionTargetResolverTests
{
    [Fact]
    public void Resolve_PreservesKoreanSpacesCommaAndSpecialCharacters()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".shell-target", Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(testRoot, "한글, 공백 # 폴더");
        var file = Path.Combine(folder, "보고서, 최종 #1.txt");

        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(file, "test");

            var target = ShellSelectionTargetResolver.Resolve(file);

            Assert.True(target.CanSelectItem);
            Assert.Equal(file, target.ItemPath);
            Assert.Equal(folder, target.FolderPath);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Resolve_UsesNearestExistingFolderWhenItemNoLongerExists()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".shell-target", Guid.NewGuid().ToString("N"));
        var existingFolder = Path.Combine(testRoot, "대상 폴더");
        var missingFile = Path.Combine(existingFolder, "사라진 하위 폴더", "파일.txt");

        try
        {
            Directory.CreateDirectory(existingFolder);

            var target = ShellSelectionTargetResolver.Resolve(missingFile);

            Assert.False(target.CanSelectItem);
            Assert.Equal(existingFolder, target.FolderPath);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
