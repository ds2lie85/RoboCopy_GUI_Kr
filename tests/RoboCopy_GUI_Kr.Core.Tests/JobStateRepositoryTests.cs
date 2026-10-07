using RoboCopy_GUI_Kr.Core.Jobs;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class JobStateRepositoryTests
{
    [Fact]
    public void SaveLoadDelete_RoundTripsPausedJob()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".jobs", Guid.NewGuid().ToString("N"));

        try
        {
            var repository = new JobStateRepository(testRoot);
            var expected = new PersistedJob
            {
                Id = Guid.NewGuid(),
                State = JobExecutionState.Paused,
                Options = new RobocopyJobOptions
                {
                    SourceDirectory = @"D:\원본",
                    DestinationDirectory = @"E:\대상",
                    MultiThreadCount = 16,
                    ExcludedDirectories = [@"D:\원본\Temp"],
                    ExcludedFiles = ["*.tmp"],
                },
            };

            repository.Save(expected);
            var actual = repository.Load();

            Assert.NotNull(actual);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(JobExecutionState.Paused, actual.State);
            Assert.Equal(16, actual.Options.MultiThreadCount);
            Assert.Equal(expected.Options.ExcludedDirectories, actual.Options.ExcludedDirectories);
            Assert.Equal(expected.Options.ExcludedFiles, actual.Options.ExcludedFiles);

            repository.Delete();
            Assert.Null(repository.Load());
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
