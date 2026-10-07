using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ShellCopyRequestFactoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"RoboCopy GUI 한글 & {Guid.NewGuid():N}");

    [Fact]
    public void Create_UsesPositiveFileSelectionAndSeparateDirectoryRequest()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "원본 폴더")).FullName;
        var selectedDirectory = Directory.CreateDirectory(Path.Combine(source, "선택 폴더 (1)")).FullName;
        var uncheckedDirectory = Directory.CreateDirectory(Path.Combine(source, "미선택 폴더")).FullName;
        var selectedFile = Path.Combine(source, "선택 파일 &.txt");
        var uncheckedFile = Path.Combine(source, "미선택 파일.txt");
        File.WriteAllText(selectedFile, "selected");
        File.WriteAllText(uncheckedFile, "unchecked");
        var destination = Directory.CreateDirectory(Path.Combine(_root, "대상 폴더")).FullName;

        var requests = ShellCopyRequestFactory.Create(destination, [selectedFile, selectedDirectory]);

        Assert.Equal(2, requests.Count);
        var fileRequest = Assert.Single(requests.Where(request => request.FileSpecifications.Count > 0));
        Assert.Equal(source, fileRequest.SourceDirectory);
        Assert.Equal(destination, fileRequest.DestinationDirectory);
        Assert.False(fileRequest.RecurseSubdirectories);
        Assert.Equal([Path.GetFileName(selectedFile)], fileRequest.FileSpecifications);
        Assert.Empty(fileRequest.ExcludedDirectories);
        Assert.Empty(fileRequest.ExcludedFiles);

        var directoryRequest = Assert.Single(requests.Where(request => request.FileSpecifications.Count == 0));
        Assert.Equal(selectedDirectory, directoryRequest.SourceDirectory);
        Assert.Equal(Path.Combine(destination, Path.GetFileName(selectedDirectory)), directoryRequest.DestinationDirectory);
        Assert.True(directoryRequest.RecurseSubdirectories);
        Assert.Empty(directoryRequest.ExcludedDirectories);
        Assert.Empty(directoryRequest.ExcludedFiles);
    }

    [Fact]
    public void Create_DifferentParentsBecomeSeparateQueueRequests()
    {
        var firstParent = Directory.CreateDirectory(Path.Combine(_root, "첫 상위")).FullName;
        var secondParent = Directory.CreateDirectory(Path.Combine(_root, "둘째 상위")).FullName;
        var first = Path.Combine(firstParent, "하나.txt");
        var second = Path.Combine(secondParent, "둘.txt");
        File.WriteAllText(first, "1");
        File.WriteAllText(second, "2");
        var destination = Directory.CreateDirectory(Path.Combine(_root, "대상")).FullName;

        var requests = ShellCopyRequestFactory.Create(destination, [first, second]);

        Assert.Equal(2, requests.Count);
        Assert.Contains(requests, request => request.SourceDirectory == firstParent);
        Assert.Contains(requests, request => request.SourceDirectory == secondParent);
    }

    [Fact]
    public void Create_DoesNotGrowArgumentsWithUnselectedSiblings()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "many siblings")).FullName;
        var selectedFile = Path.Combine(source, "선택 파일.json");
        File.WriteAllText(selectedFile, "selected");
        for (var index = 0; index < 400; index++)
        {
            File.WriteAllText(Path.Combine(source, $"미선택-{index:D4}-{new string('x', 40)}.json"), "unchecked");
        }

        var destination = Directory.CreateDirectory(Path.Combine(_root, "대상")).FullName;
        var request = Assert.Single(ShellCopyRequestFactory.Create(destination, [selectedFile]));

        Assert.Equal(["선택 파일.json"], request.FileSpecifications);
        Assert.Empty(request.ExcludedDirectories);
        Assert.Empty(request.ExcludedFiles);
    }

    [Fact]
    public void Create_SelectionOfParentDropsRedundantSelectedDescendant()
    {
        var grandParent = Directory.CreateDirectory(Path.Combine(_root, "상위")).FullName;
        var selectedDirectory = Directory.CreateDirectory(Path.Combine(grandParent, "선택 폴더")).FullName;
        var descendant = Path.Combine(selectedDirectory, "중복.txt");
        File.WriteAllText(descendant, "data");
        var destination = Directory.CreateDirectory(Path.Combine(_root, "대상2")).FullName;

        var request = Assert.Single(ShellCopyRequestFactory.Create(destination, [selectedDirectory, descendant]));

        Assert.Equal(selectedDirectory, request.SourceDirectory);
        Assert.Equal(Path.Combine(destination, Path.GetFileName(selectedDirectory)), request.DestinationDirectory);
    }

    [Fact]
    public void Create_ManySelectedFilesAreSplitBeforeTheWindowsCommandLineLimit()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "many selected files")).FullName;
        var selected = Enumerable.Range(0, 300)
            .Select(index =>
            {
                var path = Path.Combine(source, $"selected-{index:D4}-{new string('x', 40)}.txt");
                File.WriteAllText(path, index.ToString());
                return path;
            })
            .ToArray();
        var destination = Directory.CreateDirectory(Path.Combine(_root, "batched destination")).FullName;

        var requests = ShellCopyRequestFactory.Create(destination, selected);

        Assert.True(requests.Count >= 3);
        Assert.Equal(300, requests.Sum(request => request.FileSpecifications.Count));
        Assert.All(requests, request => Assert.InRange(request.FileSpecifications.Count, 1, 128));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
