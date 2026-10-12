using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ShellConflictResolutionPlannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"RoboCopy resolution {Guid.NewGuid():N}");

    [Fact]
    public void Overwrite_OneSourceAndDestinationUsesSourceWithoutWinnerSelection()
    {
        var setup = CreateDuplicateSources();
        var group = CreateGroup(setup.FirstFile, setup.SecondFile, setup.DestinationFile, existing: true);
        group = group with { Sources = [group.Sources[0]] };
        Assert.False(group.RequiresOverwriteSelection);
        var result = new ShellConflictResolutionPlanner().Apply(setup.Requests,
            [new ShellConflictResolution(group, ShellConflictDecision.Overwrite)], [setup.DestinationFile]);
        var direct = Assert.Single(result.Where(request => request.DestinationFileName is not null));
        Assert.Equal(setup.FirstFile, Path.Combine(direct.SourceDirectory, Assert.Single(direct.FileSpecifications)));
        Assert.True(direct.OverwriteDestination);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Overwrite_MultipleSourcesForSameDestinationRequiresSelection(bool existing)
    {
        var setup = CreateDuplicateSources();
        var group = CreateGroup(setup.FirstFile, setup.SecondFile, setup.DestinationFile, existing);
        Assert.True(group.RequiresOverwriteSelection);
        var result = new ShellConflictResolutionPlanner().Apply(setup.Requests,
            [new ShellConflictResolution(group, ShellConflictDecision.Overwrite)], [setup.DestinationFile]);
        Assert.DoesNotContain(result, request => request.DestinationFileName is not null);
        Assert.All(result, request => Assert.True(request.SkipExistingFiles));
    }

    [Fact]
    public void Overwrite_MixedGroupsUsesSingleSourceAndExplicitMultipleSourceWinner()
    {
        var setup = CreateDuplicateSources();
        var multiple = CreateGroup(setup.FirstFile, setup.SecondFile, setup.DestinationFile, existing: true);
        var singlePath = Path.Combine(Path.GetDirectoryName(setup.FirstFile)!, "단일 공백 & #.txt");
        var singleDestination = Path.Combine(Path.GetDirectoryName(setup.DestinationFile)!, "단일 공백 & #.txt");
        var single = new ShellCopyConflictGroup(singleDestination,
            [new ShellCopyCandidate(singlePath, singleDestination, 10, DateTime.Today)],
            new ShellExistingDestination(singleDestination, 5, DateTime.Today));
        var requests = setup.Requests.Select(request => request with { FileSpecifications = [] }).ToArray();
        var result = new ShellConflictResolutionPlanner().Apply(requests,
            [new ShellConflictResolution(single, ShellConflictDecision.Overwrite),
             new ShellConflictResolution(multiple, ShellConflictDecision.Overwrite, setup.SecondFile)],
            [singleDestination, setup.DestinationFile]);
        var direct = result.Where(request => request.DestinationFileName is not null).ToArray();
        Assert.Equal(2, direct.Length);
        Assert.All(direct, request => Assert.True(request.OverwriteDestination));
        Assert.Contains(direct, request => Path.Combine(request.SourceDirectory, request.FileSpecifications[0]) == singlePath);
        Assert.Contains(direct, request => Path.Combine(request.SourceDirectory, request.FileSpecifications[0]) == setup.SecondFile);
    }

    [Fact]
    public void Skip_WithoutExistingDestinationKeepsFirstSelectedSourceOnly()
    {
        var setup = CreateDuplicateSources();
        var group = CreateGroup(setup.FirstFile, setup.SecondFile, setup.DestinationFile, existing: false);

        var result = new ShellConflictResolutionPlanner().Apply(
            setup.Requests,
            [new ShellConflictResolution(group, ShellConflictDecision.Skip)],
            [setup.DestinationFile]);
        var direct = Assert.Single(result.Where(request => request.DestinationFileName is not null));

        Assert.Equal(setup.FirstFile, Path.Combine(direct.SourceDirectory, Assert.Single(direct.FileSpecifications)));
        Assert.False(direct.OverwriteDestination);
        Assert.All(result.Where(request => request.DestinationFileName is null), request => Assert.True(request.SkipExistingFiles));
        Assert.Empty(result.SelectMany(request => request.ExcludedFiles));
    }

    [Fact]
    public void Overwrite_KeepsOnlySelectedWinner()
    {
        var setup = CreateDuplicateSources();
        var group = CreateGroup(setup.FirstFile, setup.SecondFile, setup.DestinationFile, existing: true);

        var result = new ShellConflictResolutionPlanner().Apply(
            setup.Requests,
            [new ShellConflictResolution(group, ShellConflictDecision.Overwrite, setup.SecondFile)],
            [setup.DestinationFile]);
        var direct = Assert.Single(result.Where(request => request.DestinationFileName is not null));

        Assert.Equal(setup.SecondFile, Path.Combine(direct.SourceDirectory, Assert.Single(direct.FileSpecifications)));
        Assert.True(direct.OverwriteDestination);
        Assert.All(result.Where(request => request.DestinationFileName is null), request => Assert.True(request.SkipExistingFiles));
        Assert.Empty(result.SelectMany(request => request.ExcludedFiles));
    }

    [Fact]
    public void KeepBoth_CreatesSequentialRenamedRequests()
    {
        var setup = CreateDuplicateSources();
        File.WriteAllText(setup.DestinationFile, "existing");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(setup.DestinationFile)!, "same (1).txt"), "existing 1");
        var group = CreateGroup(setup.FirstFile, setup.SecondFile, setup.DestinationFile, existing: true);

        var result = new ShellConflictResolutionPlanner().Apply(
            setup.Requests,
            [new ShellConflictResolution(group, ShellConflictDecision.KeepBoth)],
            [setup.DestinationFile]);
        var renamed = result.Where(request => request.DestinationFileName is not null).ToArray();

        Assert.Equal(2, renamed.Length);
        Assert.Contains(renamed, request => request.DestinationFileName == "same (2).txt");
        Assert.Contains(renamed, request => request.DestinationFileName == "same (3).txt");
    }

    private (string FirstFile, string SecondFile, string DestinationFile, ShellCopyRequest[] Requests) CreateDuplicateSources()
    {
        var first = Directory.CreateDirectory(Path.Combine(_root, "\uCCAB \uBC88\uC9F8 \uC6D0\uBCF8")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_root, "\uB458\uC9F8 \uBC88\uC9F8 \uC6D0\uBCF8")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(_root, "\uB300\uC0C1 \uD3F4\uB354")).FullName;
        var firstFile = Path.Combine(first, "same.txt");
        var secondFile = Path.Combine(second, "same.txt");
        File.WriteAllText(firstFile, "first");
        File.WriteAllText(secondFile, "second");
        return (
            firstFile,
            secondFile,
            Path.Combine(destination, "same.txt"),
            [
                new ShellCopyRequest(first, destination, ["same.txt"], false),
                new ShellCopyRequest(second, destination, ["same.txt"], false),
            ]);
    }

    private static ShellCopyConflictGroup CreateGroup(
        string first,
        string second,
        string destination,
        bool existing) =>
        new(
            destination,
            [
                new ShellCopyCandidate(first, destination, new FileInfo(first).Length, File.GetLastWriteTime(first), 0),
                new ShellCopyCandidate(second, destination, new FileInfo(second).Length, File.GetLastWriteTime(second), 1),
            ],
            existing ? new ShellExistingDestination(destination, 1, DateTime.Now) : null);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
