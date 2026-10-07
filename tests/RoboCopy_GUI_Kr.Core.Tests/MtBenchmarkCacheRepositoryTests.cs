using RoboCopy_GUI_Kr.Core.Performance;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class MtBenchmarkCacheRepositoryTests
{
    [Fact]
    public void Record_AveragesSameThreadAndSelectsFastestFreshResult()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var cachePath = Path.Combine(testRoot, "cache.json");
        var repository = new MtBenchmarkCacheRepository(cachePath);
        var now = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        try
        {
            repository.Record("D:", "E:", 4, 100, now);
            repository.Record("D:", "E:", 4, 300, now);
            repository.Record("D:", "E:", 8, 250, now);

            var best = repository.FindBest("d:", "e:", now);

            Assert.NotNull(best);
            Assert.Equal(8, best.ThreadCount);
            Assert.Equal(250, best.AverageBytesPerSecond);
            var entries = File.ReadAllText(cachePath);
            Assert.Contains("\"SampleCount\": 2", entries, StringComparison.Ordinal);
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
    public void FindBest_IgnoresMeasurementsOlderThanThirtyDays()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var repository = new MtBenchmarkCacheRepository(Path.Combine(testRoot, "cache.json"));
        var testedAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        try
        {
            repository.Record("D:", "E:", 16, 500, testedAt);

            Assert.Null(repository.FindBest("D:", "E:", testedAt.AddDays(31)));
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
    public void Record_DropsExpiredAverageBeforeAddingNewMeasurement()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var repository = new MtBenchmarkCacheRepository(Path.Combine(testRoot, "cache.json"));
        var oldTime = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var newTime = oldTime.AddDays(31);

        try
        {
            repository.Record("D:", "E:", 8, 100, oldTime);
            repository.Record("D:", "E:", 8, 300, newTime);

            var entry = Assert.Single(repository.FindAll("D:", "E:", newTime));
            Assert.Equal(300, entry.AverageBytesPerSecond);
            Assert.Equal(1, entry.SampleCount);
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
    public void Record_AppliesHigherWeightToRepresentativeMeasurement()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var repository = new MtBenchmarkCacheRepository(Path.Combine(testRoot, "cache.json"));
        var now = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        try
        {
            repository.Record("D:", "E:", 8, 100, now, sampleWeight: 1);
            repository.Record("D:", "E:", 8, 200, now, sampleWeight: 3);

            var entry = Assert.Single(repository.FindAll("D:", "E:", now));
            Assert.Equal(175, entry.AverageBytesPerSecond);
            Assert.Equal(200, entry.LatestBytesPerSecond);
            Assert.Equal(2, entry.SampleCount);
            Assert.Equal(4, entry.TotalWeight);
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
    public void FindBest_PrefersLowerThreadCountWithinFivePercentOfPeak()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var repository = new MtBenchmarkCacheRepository(Path.Combine(testRoot, "cache.json"));
        var now = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        try
        {
            repository.Record("D:", "E:", 8, 960, now);
            repository.Record("D:", "E:", 16, 1_000, now);
            repository.Record("D:", "E:", 32, 1_010, now);

            var best = repository.FindBest("D:", "E:", now);

            Assert.NotNull(best);
            Assert.Equal(8, best.ThreadCount);
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
    public void FindRecommended_PrefersLowestThreadCountAtSeventyPercentOfPeak()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var repository = new MtBenchmarkCacheRepository(Path.Combine(testRoot, "cache.json"));
        var now = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

        try
        {
            repository.Record("D:", "E:", 4, 690, now);
            repository.Record("D:", "E:", 8, 710, now);
            repository.Record("D:", "E:", 16, 1_000, now);

            var recommended = repository.FindRecommended("D:", "E:", now);
            var maximumPerformance = repository.FindBest("D:", "E:", now);

            Assert.Equal(8, Assert.IsType<MtBenchmarkCacheEntry>(recommended).ThreadCount);
            Assert.Equal(16, Assert.IsType<MtBenchmarkCacheEntry>(maximumPerformance).ThreadCount);
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
    public void Remove_DeletesOnlyRequestedVolumePair()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var repository = new MtBenchmarkCacheRepository(Path.Combine(testRoot, "cache.json"));
        var now = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        try
        {
            repository.Record("D:", "E:", 16, 500, now);
            repository.Record("D:", "F:", 8, 250, now);

            Assert.True(repository.Remove("d:", "e:"));
            Assert.Null(repository.FindBest("D:", "E:", now));
            Assert.NotNull(repository.FindBest("D:", "F:", now));
            Assert.False(repository.Remove("D:", "E:"));
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
    public void FindAllAndRemoveThread_ManageOnlySelectedPairAndThread()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var repository = new MtBenchmarkCacheRepository(Path.Combine(testRoot, "cache.json"));
        var now = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        try
        {
            repository.Record("D:", "E:", 4, 100, now);
            repository.Record("D:", "E:", 8, 200, now);
            repository.Record("D:", "F:", 16, 300, now);

            var history = repository.FindAll("d:", "e:", now);

            Assert.Equal([4, 8], history.Select(entry => entry.ThreadCount));
            Assert.True(repository.Remove("D:", "E:", 4));
            Assert.Equal(8, Assert.Single(repository.FindAll("D:", "E:", now)).ThreadCount);
            Assert.NotNull(repository.FindBest("D:", "F:", now));
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
    public void RecommendationService_ReusesSuccessfulLargeCopyMeasurement()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var repository = new MtBenchmarkCacheRepository(Path.Combine(testRoot, "cache.json"));
        var service = new MtRecommendationService(repository);

        try
        {
            service.RecordSuccessfulRun(
                @"D:\원본 폴더",
                @"E:\대상 폴더",
                threadCount: 16,
                copiedBytes: 128L * 1024 * 1024,
                elapsed: TimeSpan.FromSeconds(4));

            var recommendation = service.Recommend(@"D:\다른 원본", @"E:\다른 대상");

            Assert.Equal(16, recommendation.ThreadCount);
            Assert.Equal(MtRecommendationReason.BenchmarkCache, recommendation.Reason);
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
    public void RecommendationService_StoresExplicitBenchmarkMeasurements()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var repository = new MtBenchmarkCacheRepository(Path.Combine(testRoot, "cache.json"));
        var service = new MtRecommendationService(repository);

        try
        {
            service.RecordBenchmarkMeasurements(
                @"D:\원본 폴더",
                @"E:\대상 폴더",
                [
                    new MtBenchmarkMeasurement(4, 32 * 1024 * 1024, TimeSpan.FromSeconds(2)),
                    new MtBenchmarkMeasurement(8, 32 * 1024 * 1024, TimeSpan.FromSeconds(1)),
                ]);

            var recommendation = service.Recommend(@"D:\다른 원본", @"E:\다른 대상");

            Assert.Equal(8, recommendation.ThreadCount);
            Assert.Equal(MtRecommendationReason.BenchmarkCache, recommendation.Reason);
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
    public void RecommendationService_RepeatedBenchmarkAccumulatesHistoryWithoutReset()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-cache", Guid.NewGuid().ToString("N"));
        var repository = new MtBenchmarkCacheRepository(Path.Combine(testRoot, "cache.json"));
        var service = new MtRecommendationService(repository);

        try
        {
            var measurement = new MtBenchmarkMeasurement(8, 32 * 1024 * 1024, TimeSpan.FromSeconds(1));
            service.RecordBenchmarkMeasurements(@"D:\원본", @"E:\대상", [measurement]);
            service.RecordBenchmarkMeasurements(@"D:\원본", @"E:\대상", [measurement]);

            var entry = Assert.Single(service.GetBenchmarkHistory(@"D:\원본", @"E:\대상"));
            Assert.Equal(2, entry.SampleCount);
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
