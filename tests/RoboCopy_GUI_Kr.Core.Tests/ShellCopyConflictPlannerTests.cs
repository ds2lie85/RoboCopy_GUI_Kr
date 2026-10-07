using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ShellCopyConflictPlannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"RoboCopy collision 한글 & {Guid.NewGuid():N}");

    [Fact]
    public void FindConflicts_DetectsExistingDestinationFile()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "원본 폴더")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(_root, "대상 폴더")).FullName;
        File.WriteAllText(Path.Combine(source, "같은 이름.txt"), "source");
        File.WriteAllText(Path.Combine(destination, "같은 이름.txt"), "destination");
        var request = new ShellCopyRequest(source, destination, ["같은 이름.txt"], false);

        var conflict = Assert.Single(new ShellCopyConflictPlanner().FindConflicts([request]));

        Assert.Equal(Path.Combine(source, "같은 이름.txt"), Assert.Single(conflict.Sources).SourcePath);
        Assert.Equal(Path.Combine(destination, "같은 이름.txt"), conflict.DestinationPath);
        Assert.NotNull(conflict.ExistingDestination);
    }

    [Fact]
    public void FindConflicts_DetectsSameDestinationFromDifferentParents()
    {
        var first = Directory.CreateDirectory(Path.Combine(_root, "상위 1")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_root, "상위 2")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(_root, "대상")).FullName;
        File.WriteAllText(Path.Combine(first, "중복.json"), "first");
        File.WriteAllText(Path.Combine(second, "중복.json"), "second");
        var requests = new[]
        {
            new ShellCopyRequest(first, destination, ["중복.json"], false),
            new ShellCopyRequest(second, destination, ["중복.json"], false),
        };

        var conflict = Assert.Single(new ShellCopyConflictPlanner().FindConflicts(requests));

        Assert.Equal(2, conflict.Sources.Count);
        Assert.Contains(conflict.Sources, candidate => candidate.SourcePath == Path.Combine(first, "중복.json"));
        Assert.Contains(conflict.Sources, candidate => candidate.SourcePath == Path.Combine(second, "중복.json"));
        Assert.Null(conflict.ExistingDestination);
    }

    [Fact]
    public async Task AnalyzeAsync_PreservesExplorerRequestOrderAndReportsBothStages()
    {
        var first = Directory.CreateDirectory(Path.Combine(_root, "selection 1")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_root, "selection 2")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(_root, "ordered destination")).FullName;
        File.WriteAllText(Path.Combine(first, "same.txt"), "first");
        File.WriteAllText(Path.Combine(second, "same.txt"), "second");
        var reports = new List<ShellConflictScanProgress>();
        var progress = new SynchronousProgress<ShellConflictScanProgress>(reports.Add);

        var analysis = await new ShellCopyConflictPlanner().AnalyzeAsync(
            [
                new ShellCopyRequest(second, destination, ["same.txt"], false),
                new ShellCopyRequest(first, destination, ["same.txt"], false),
            ],
            progress);

        var group = Assert.Single(analysis.Groups);
        Assert.Equal(Path.Combine(second, "same.txt"), group.Sources[0].SourcePath);
        Assert.Contains(reports, report => report.Stage == ShellConflictScanStage.CollectingFiles);
        Assert.Contains(reports, report => report.Stage == ShellConflictScanStage.ComparingDestinations);
    }

    [Fact]
    public async Task AnalyzeAsync_LargeSelectionBuildsOneDestinationIndex()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "large source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(_root, "large destination")).FullName;
        var names = Enumerable.Range(0, 520).Select(index => $"file-{index:D4}.txt").ToArray();
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(source, name), name);
        }

        var reports = new List<ShellConflictScanProgress>();
        var analysis = await new ShellCopyConflictPlanner().AnalyzeAsync(
            [new ShellCopyRequest(source, destination, names, false)],
            new SynchronousProgress<ShellConflictScanProgress>(reports.Add));

        Assert.Empty(analysis.Groups);
        Assert.Contains(reports, report => report.Stage == ShellConflictScanStage.ScanningDestinations);
    }

    [Fact]
    public void FindConflicts_MapsSelectedDirectoryContentsBelowItsDestinationFolder()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "선택 폴더")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(source, "하위")).FullName;
        File.WriteAllText(Path.Combine(nested, "파일.dat"), "source");
        var destination = Directory.CreateDirectory(Path.Combine(_root, "대상", "선택 폴더")).FullName;
        Directory.CreateDirectory(Path.Combine(destination, "하위"));
        File.WriteAllText(Path.Combine(destination, "하위", "파일.dat"), "destination");
        var request = new ShellCopyRequest(source, destination, [], true);

        var conflict = Assert.Single(new ShellCopyConflictPlanner().FindConflicts([request]));

        Assert.Equal(Path.Combine(destination, "하위", "파일.dat"), conflict.DestinationPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
