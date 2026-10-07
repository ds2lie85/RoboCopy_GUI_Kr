using RoboCopy_GUI_Kr.Core.Jobs;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class JobQueueTests
{
    [Fact]
    public void TryStartNext_UsesRegistrationOrderAndAllowsOnlyOneRunningJob()
    {
        var queue = new JobQueue();
        var first = queue.Enqueue(CreateOptions(@"D:\첫 원본", @"E:\첫 대상"));
        var second = queue.Enqueue(CreateOptions(@"D:\둘째 원본", @"E:\둘째 대상"));

        Assert.True(queue.TryStartNext(out var running));
        Assert.Equal(first.Id, running?.Id);
        Assert.False(queue.TryStartNext(out _));

        Assert.True(queue.SetState(first.Id, JobQueueEntryState.Completed));
        Assert.True(queue.TryStartNext(out running));
        Assert.Equal(second.Id, running?.Id);
    }

    [Fact]
    public void Update_RequeuesFinishedJobAndRunningJobCannotBeEditedOrDeleted()
    {
        var queue = new JobQueue();
        var entry = queue.Enqueue(CreateOptions(@"D:\원본", @"E:\대상"));

        Assert.True(queue.TryStartNext(out _));
        Assert.False(queue.Update(entry.Id, CreateOptions(@"D:\수정", @"E:\수정")));
        Assert.False(queue.Remove(entry.Id));

        Assert.True(queue.SetState(entry.Id, JobQueueEntryState.Completed));
        Assert.True(queue.Update(entry.Id, CreateOptions(@"D:\수정", @"E:\수정")));
        Assert.Equal(JobQueueEntryState.Waiting, Assert.Single(queue.Items).State);
        Assert.Equal(@"D:\수정", Assert.Single(queue.Items).Options.SourceDirectory);
    }

    private static RobocopyJobOptions CreateOptions(string source, string destination)
    {
        return new RobocopyJobOptions
        {
            SourceDirectory = source,
            DestinationDirectory = destination,
        };
    }
}
