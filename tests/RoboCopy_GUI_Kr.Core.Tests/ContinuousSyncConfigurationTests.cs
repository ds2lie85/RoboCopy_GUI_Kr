using RoboCopy_GUI_Kr.Core.ContinuousSync;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ContinuousSyncConfigurationTests
{
    [Fact]
    public void RepositoryRoundTripsConfigurationWithKoreanAndSpacePaths()
    {
        var root = CreateTemporaryDirectory();
        var source = Directory.CreateDirectory(Path.Combine(root, "원본 폴더")).FullName;
        var destination = Path.Combine(root, "대상 폴더");
        try
        {
            var repository = new ContinuousSyncConfigurationRepository(root);
            var expected = new ContinuousSyncConfiguration
            {
                MonitorIntervalMinutes = 7,
                MultiThreadMode = ContinuousSyncMultiThreadMode.MaximumPerformance,
                DestinationOnlyPolicy = DestinationOnlyPolicy.Ask,
                LimitToSchedule = true,
                ScheduleStartMinute = 23 * 60,
                ScheduleEndMinute = 6 * 60,
                PauseOnBattery = true,
                PauseOnMeteredNetwork = true,
                QuarantineDeletedItems = true,
                RetainedDestinationOnlyPaths = [Path.Combine("보관", "유지.txt")],
                JobOptions = new RobocopyJobOptions
                {
                    OperationMode = JobOperationMode.UpdateSync,
                    SourceDirectory = source,
                    DestinationDirectory = destination,
                    MultiThreadCount = 16,
                    ExcludedFiles = ["*.tmp"],
                },
            };

            repository.Save(expected);
            var actual = Assert.Single(repository.LoadAll());

            Assert.Equal(7, actual.MonitorIntervalMinutes);
            Assert.Equal(ContinuousSyncMultiThreadMode.MaximumPerformance, actual.MultiThreadMode);
            Assert.Equal(DestinationOnlyPolicy.Ask, actual.DestinationOnlyPolicy);
            Assert.Equal(source, actual.JobOptions.SourceDirectory);
            Assert.Equal(destination, actual.JobOptions.DestinationDirectory);
            Assert.Equal(16, actual.JobOptions.MultiThreadCount);
            Assert.Equal(["*.tmp"], actual.JobOptions.ExcludedFiles);
            Assert.True(actual.LimitToSchedule);
            Assert.Equal(23 * 60, actual.ScheduleStartMinute);
            Assert.Equal(6 * 60, actual.ScheduleEndMinute);
            Assert.True(actual.PauseOnBattery);
            Assert.True(actual.PauseOnMeteredNetwork);
            Assert.True(actual.QuarantineDeletedItems);
            Assert.Equal([Path.Combine("보관", "유지.txt")], actual.RetainedDestinationOnlyPaths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RepositoryUpdatesAndDeletesOnlySelectedTask()
    {
        var root = CreateTemporaryDirectory();
        var sourceA = Directory.CreateDirectory(Path.Combine(root, "source-a")).FullName;
        var sourceB = Directory.CreateDirectory(Path.Combine(root, "source-b")).FullName;
        try
        {
            var repository = new ContinuousSyncConfigurationRepository(root);
            var first = CreateConfiguration(sourceA, Path.Combine(root, "destination-a"), 5);
            var second = CreateConfiguration(sourceB, Path.Combine(root, "destination-b"), 10);
            repository.Save(first);
            repository.Save(second);
            repository.Save(first with { MonitorIntervalMinutes = 15 });

            var saved = repository.LoadAll();
            Assert.Equal(2, saved.Count);
            Assert.Equal(15, saved.Single(item => item.Id == first.Id).MonitorIntervalMinutes);

            repository.Delete(second.Id);
            Assert.Equal(first.Id, Assert.Single(repository.LoadAll()).Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RepositoryLoadsTaskWhileSourceDriveOrFolderIsTemporarilyUnavailable()
    {
        var root = CreateTemporaryDirectory();
        var source = Directory.CreateDirectory(Path.Combine(root, "removable-source")).FullName;
        try
        {
            var repository = new ContinuousSyncConfigurationRepository(root);
            var configuration = CreateConfiguration(source, Path.Combine(root, "destination"), 5);
            repository.Save(configuration);
            Directory.Delete(source);

            Assert.Equal(configuration.Id, Assert.Single(repository.LoadAll()).Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RepositoryRejectsDestinationThatOverlapsAnotherTask()
    {
        var root = CreateTemporaryDirectory();
        var sourceA = Directory.CreateDirectory(Path.Combine(root, "source-a")).FullName;
        var sourceB = Directory.CreateDirectory(Path.Combine(root, "source-b")).FullName;
        var destination = Path.Combine(root, "destination");
        try
        {
            var repository = new ContinuousSyncConfigurationRepository(root);
            repository.Save(CreateConfiguration(sourceA, destination, 5));

            Assert.Throws<ContinuousSyncTaskConflictException>(() =>
                repository.Save(CreateConfiguration(sourceB, Path.Combine(destination, "nested"), 5)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RepositoryFindsAndReplacesConflictingTaskWhilePreservingItsIdentity()
    {
        var root = CreateTemporaryDirectory();
        var oldSource = Directory.CreateDirectory(Path.Combine(root, "old-source")).FullName;
        var newSource = Directory.CreateDirectory(Path.Combine(root, "new-source")).FullName;
        var destination = Path.Combine(root, "destination");
        try
        {
            var repository = new ContinuousSyncConfigurationRepository(root);
            var existing = CreateConfiguration(oldSource, destination, 5);
            var candidate = CreateConfiguration(newSource, destination, 10);
            repository.Save(existing);

            var conflict = Assert.Single(repository.FindConflicts(candidate));
            Assert.Equal(existing.Id, conflict.Id);

            var replacement = repository.ReplaceConflicting(candidate);
            var saved = Assert.Single(repository.LoadAll());
            Assert.Equal(existing.Id, replacement.Id);
            Assert.Equal(existing.Id, saved.Id);
            Assert.Equal(newSource, saved.JobOptions.SourceDirectory);
            Assert.Equal(10, saved.MonitorIntervalMinutes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RepositoryReplacementRemovesEveryConflictingTask()
    {
        var root = CreateTemporaryDirectory();
        var firstSource = Directory.CreateDirectory(Path.Combine(root, "first-source")).FullName;
        var secondSource = Directory.CreateDirectory(Path.Combine(root, "second-source")).FullName;
        var newSource = Directory.CreateDirectory(Path.Combine(root, "new-source")).FullName;
        try
        {
            var repository = new ContinuousSyncConfigurationRepository(root);
            repository.Save(CreateConfiguration(firstSource, Path.Combine(root, "destination", "first"), 5));
            repository.Save(CreateConfiguration(secondSource, Path.Combine(root, "destination", "second"), 5));
            var candidate = CreateConfiguration(newSource, Path.Combine(root, "destination"), 15);

            Assert.Equal(2, repository.FindConflicts(candidate).Count);
            repository.ReplaceConflicting(candidate);

            var saved = Assert.Single(repository.LoadAll());
            Assert.Equal(newSource, saved.JobOptions.SourceDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void RepositoryRejectsInvalidMonitorInterval(int minutes)
    {
        var root = CreateTemporaryDirectory();
        var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        try
        {
            var configuration = new ContinuousSyncConfiguration
            {
                MonitorIntervalMinutes = minutes,
                JobOptions = new RobocopyJobOptions
                {
                    SourceDirectory = source,
                    DestinationDirectory = Path.Combine(root, "destination"),
                },
            };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ContinuousSyncConfigurationRepository(root).Save(configuration));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StartupCommandQuotesExecutablePath()
    {
        var path = Path.Combine(Path.GetTempPath(), "RoboCopy Agent", "RoboCopy_GUI_Kr.Agent.exe");

        Assert.Equal($"\"{Path.GetFullPath(path)}\"", ContinuousSyncAgentCommand.BuildStartupCommand(path));
    }

    [Theory]
    [InlineData(23, 30, true)]
    [InlineData(5, 59, true)]
    [InlineData(12, 0, false)]
    public void OvernightScheduleEvaluatesExpectedTimes(int hour, int minute, bool expected)
    {
        Assert.Equal(expected, ContinuousSyncExecutionPolicy.IsWithinSchedule(
            new TimeSpan(hour, minute, 0),
            23 * 60,
            6 * 60));
    }

    [Fact]
    public void ExecutionPolicyPausesForBatteryBeforeSchedule()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var configuration = CreateConfiguration(
                Directory.CreateDirectory(Path.Combine(root, "source")).FullName,
                Path.Combine(root, "destination"),
                5) with
            {
                PauseOnBattery = true,
                LimitToSchedule = true,
                ScheduleStartMinute = 23 * 60,
                ScheduleEndMinute = 6 * 60,
            };

            Assert.Equal(ContinuousSyncPauseReason.Battery, ContinuousSyncExecutionPolicy.Evaluate(
                configuration,
                new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(9)),
                isOnBattery: true,
                isMeteredNetwork: false));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RunRequestsAccumulateDistinctSelectedTasksAndAreConsumedOnce()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var requests = new ContinuousSyncRunRequestRepository(root);

            requests.Enqueue([first]);
            requests.Enqueue([first, second, Guid.Empty]);

            Assert.Equal([first, second], requests.Consume());
            Assert.Empty(requests.Consume());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RepositoryDeletesAllCheckedTasksInOneUpdate()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = new ContinuousSyncConfigurationRepository(root);
            var first = CreateConfiguration(
                Directory.CreateDirectory(Path.Combine(root, "원본 폴더 1")).FullName,
                Path.Combine(root, "대상 폴더 1"), 1);
            var second = CreateConfiguration(
                Directory.CreateDirectory(Path.Combine(root, "원본 폴더 2")).FullName,
                Path.Combine(root, "대상 폴더 2"), 2);
            var retained = CreateConfiguration(
                Directory.CreateDirectory(Path.Combine(root, "원본 폴더 #3")).FullName,
                Path.Combine(root, "대상 폴더 #3"), 3);
            repository.Save(first);
            repository.Save(second);
            repository.Save(retained);

            repository.Delete([first.Id, second.Id]);

            Assert.Equal(retained.Id, Assert.Single(repository.LoadAll()).Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"RoboCopyKr-Continuous-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static ContinuousSyncConfiguration CreateConfiguration(string source, string destination, int interval) => new()
    {
        MonitorIntervalMinutes = interval,
        JobOptions = new RobocopyJobOptions
        {
            OperationMode = JobOperationMode.UpdateSync,
            SourceDirectory = source,
            DestinationDirectory = destination,
        },
    };
}
