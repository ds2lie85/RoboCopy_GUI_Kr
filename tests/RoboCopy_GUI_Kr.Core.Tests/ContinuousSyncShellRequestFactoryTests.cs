using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ContinuousSyncShellRequestFactoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"RoboCopyKr-ContinuousShell-{Guid.NewGuid():N}");

    public ContinuousSyncShellRequestFactoryTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void AllSelectedChildrenBecomeOneParentToDestinationTask()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "시험 07")).FullName;
        var first = Directory.CreateDirectory(Path.Combine(source, "하위 폴더 #1")).FullName;
        var second = Path.Combine(source, "한글 파일.txt");
        File.WriteAllText(second, "test");
        var destination = Directory.CreateDirectory(Path.Combine(_root, "시험 3")).FullName;

        var request = Assert.Single(ContinuousSyncShellRequestFactory.Create(destination, [first, second]));

        Assert.Equal(source, request.SourceDirectory);
        Assert.Equal(destination, request.DestinationDirectory);
        Assert.Empty(request.ExcludedDirectories);
        Assert.Empty(request.ExcludedFiles);
    }

    [Fact]
    public void PartialSelectionKeepsOneParentTaskAndExcludesUnselectedChildren()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "원본 폴더")).FullName;
        var selected = Directory.CreateDirectory(Path.Combine(source, "선택 폴더")).FullName;
        var excludedDirectory = Directory.CreateDirectory(Path.Combine(source, "제외 폴더")).FullName;
        var excludedFile = Path.Combine(source, "제외 파일 #.txt");
        File.WriteAllText(excludedFile, "test");
        var destination = Directory.CreateDirectory(Path.Combine(_root, "대상 폴더")).FullName;

        var request = Assert.Single(ContinuousSyncShellRequestFactory.Create(destination, [selected]));

        Assert.Equal(source, request.SourceDirectory);
        Assert.Equal(destination, request.DestinationDirectory);
        Assert.Equal([excludedDirectory], request.ExcludedDirectories);
        Assert.Equal([excludedFile], request.ExcludedFiles);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
