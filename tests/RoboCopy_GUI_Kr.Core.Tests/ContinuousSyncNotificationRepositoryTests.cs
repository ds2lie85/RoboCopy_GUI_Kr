using RoboCopy_GUI_Kr.Core.ContinuousSync;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ContinuousSyncNotificationRepositoryTests : IDisposable
{
    private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "RoboCopyNotificationTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AddAndLoad_PreservesSummaryAndDetailedChanges()
    {
        var repository = new ContinuousSyncNotificationRepository(_dataRoot);
        var notification = new ContinuousSyncNotification
        {
            CreatedAt = new DateTimeOffset(2026, 10, 6, 9, 30, 0, TimeSpan.Zero),
            SourceDirectory = @"C:\원본 폴더",
            DestinationDirectory = @"D:\대상 폴더",
            CopiedFiles = 2,
            DeletedFiles = 1,
            Changes =
            [
                new(ContinuousSyncChangeKind.Copied, @"하위 폴더\복사 파일.txt"),
                new(ContinuousSyncChangeKind.Deleted, @"삭제 파일.txt"),
            ],
        };

        repository.Add(notification, notification.CreatedAt);
        var loaded = Assert.Single(repository.LoadAll(notification.CreatedAt));

        Assert.Equal(notification.Id, loaded.Id);
        Assert.Equal(2, loaded.CopiedFiles);
        Assert.Equal(1, loaded.DeletedFiles);
        Assert.Equal(2, loaded.Changes.Count);
        Assert.Contains(loaded.Changes, item => item.Kind == ContinuousSyncChangeKind.Deleted && item.RelativePath == @"삭제 파일.txt");
    }

    [Fact]
    public void LoadAll_RemovesNotificationsOlderThanSevenDays()
    {
        var repository = new ContinuousSyncNotificationRepository(_dataRoot);
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        repository.Add(new ContinuousSyncNotification { CreatedAt = now.AddDays(-8), CopiedFiles = 1 }, now.AddDays(-8));
        repository.Add(new ContinuousSyncNotification { CreatedAt = now.AddDays(-6), CopiedFiles = 1 }, now);

        var loaded = repository.LoadAll(now);

        Assert.Single(loaded);
        Assert.Equal(now.AddDays(-6), loaded[0].CreatedAt);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot)) Directory.Delete(_dataRoot, true);
    }
}
