using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Profiles;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class WorkProfileRepositoryTests
{
    [Fact]
    public void SaveUpdateLoadDelete_RoundTripsNamedProfileWithStableId()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".profiles", Guid.NewGuid().ToString("N"));

        try
        {
            var repository = new WorkProfileRepository(testRoot);
            var id = Guid.NewGuid();
            var profile = CreateProfile(id, "한글 백업");

            repository.Save(profile);
            repository.Save(profile with { RetryCount = 7, ExcludedFiles = ["*.tmp", "표시 파일"] });
            var loaded = Assert.Single(repository.LoadAll());

            Assert.Equal(id, loaded.Id);
            Assert.Equal("한글 백업", loaded.Name);
            Assert.True(loaded.UseAutomaticMultiThread);
            Assert.Equal(7, loaded.RetryCount);
            Assert.Equal(["*.tmp", "표시 파일"], loaded.ExcludedFiles);

            repository.Delete(id);
            Assert.Empty(repository.LoadAll());
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
    public void Save_RejectsDuplicateNameForDifferentIds()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".profiles", Guid.NewGuid().ToString("N"));

        try
        {
            var repository = new WorkProfileRepository(testRoot);
            repository.Save(CreateProfile(Guid.NewGuid(), "사진 백업"));

            Assert.Throws<InvalidOperationException>(() =>
                repository.Save(CreateProfile(Guid.NewGuid(), "사진 백업")));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static WorkProfile CreateProfile(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        SourceDirectory = @"C:\원본 폴더",
        DestinationDirectory = @"D:\대상 폴더",
        OperationMode = "Copy",
        UseAutomaticMultiThread = true,
        MultiThreadCount = 16,
        RetryCount = 3,
        RetryWaitSeconds = 5,
        Restartable = true,
        SaveLog = true,
        SymbolicLinkPolicy = SymbolicLinkPolicy.Exclude,
        ExcludedDirectories = [@"C:\원본 폴더\Temp"],
        ExcludedFiles = ["*.bak"],
    };
}

