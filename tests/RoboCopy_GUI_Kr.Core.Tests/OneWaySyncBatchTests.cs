using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Sync;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class OneWaySyncBatchTests : IDisposable
{
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, ".batch-sync", Guid.NewGuid().ToString("N"));
    private RobocopyJobOptions Job(string folder) => new()
    {
        SourceDirectory = Directory.CreateDirectory(Path.Combine(_root, "source", folder)).FullName,
        DestinationDirectory = Directory.CreateDirectory(Path.Combine(_root, "destination", folder)).FullName,
        OperationMode = JobOperationMode.UpdateSync,
    };

    [Fact]
    public async Task SingleReviewCoversNestedSiblingJobsAndRoutesOnlyApprovedDeletions()
    {
        var a = Job("한글 A & [폴더]");
        var b = Job("한글 B");
        var nested = Directory.CreateDirectory(Path.Combine(a.DestinationDirectory, "nested")).FullName;
        var deleted = Path.Combine(nested, "delete.txt");
        var retained = Path.Combine(b.DestinationDirectory, "keep.txt");
        File.WriteAllText(deleted, "delete");
        File.WriteAllText(retained, "keep");
        var outside = Directory.CreateDirectory(Path.Combine(_root, "destination", "unselected")).FullName;
        File.WriteAllText(Path.Combine(outside, "protected.txt"), "protected");
        var aId = Guid.NewGuid(); var bId = Guid.NewGuid();
        var plan = OneWaySyncBatchPlan.Create(new Dictionary<Guid, RobocopyJobOptions> { [aId] = a, [bId] = b });
        Assert.Contains(plan.Items, item => item.RelativePath == Path.Combine("한글 A & [폴더]", "nested", "delete.txt"));
        Assert.Contains(plan.Items, item => item.RelativePath == Path.Combine("한글 B", "keep.txt"));
        Assert.DoesNotContain(plan.Items, item => item.DestinationPath.StartsWith(outside));
        var keepAll = plan.ApplySelection([]);
        Assert.Equal(2, keepAll.Count);
        Assert.All(keepAll.Values, items => Assert.Empty(items));
        var selection = plan.ApplySelection(plan.Items.Where(item => item.DestinationPath == deleted));
        Assert.Single(selection[aId]);
        Assert.Empty(selection[bId]);
        var result = await new OneWaySyncDeletionService().DeleteSelectedAsync(
            a.SourceDirectory, a.DestinationDirectory, selection[aId].ToArray());
        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(deleted));
        Assert.True(File.Exists(retained));
        Assert.True(File.Exists(Path.Combine(outside, "protected.txt")));
    }

    [Fact]
    public void AnotherJobsSourceProtectsSharedDestinationAndDuplicatesAppearOnce()
    {
        var a = Job("A");
        var b = Job("B") with { DestinationDirectory = a.DestinationDirectory };
        File.WriteAllText(Path.Combine(a.DestinationDirectory, "provided.txt"), "old");
        File.WriteAllText(Path.Combine(b.SourceDirectory, "provided.txt"), "new");
        File.WriteAllText(Path.Combine(a.DestinationDirectory, "extra.txt"), "extra");
        var plan = OneWaySyncBatchPlan.Create(new Dictionary<Guid, RobocopyJobOptions>
        {
            [Guid.NewGuid()] = a, [Guid.NewGuid()] = b,
        });
        Assert.Equal("extra.txt", Assert.Single(plan.Items).RelativePath);
    }

    [Fact]
    public void NonRecursiveFileJobDoesNotReviewUnselectedDescendants()
    {
        var job = Job("A") with { RecurseSubdirectories = false, FileSpecifications = ["*.txt"] };
        Directory.CreateDirectory(Path.Combine(job.DestinationDirectory, "child"));
        File.WriteAllText(Path.Combine(job.DestinationDirectory, "child", "nested.txt"), "keep");
        File.WriteAllText(Path.Combine(job.DestinationDirectory, "root.txt"), "review");
        Assert.Equal("root.txt", Assert.Single(new OneWaySyncPlanner().FindDestinationOnlyItems(job)).RelativePath);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
