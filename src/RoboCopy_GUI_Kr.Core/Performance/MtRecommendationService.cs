namespace RoboCopy_GUI_Kr.Core.Performance;

public sealed class MtRecommendationService
{
    private const long MinimumBenchmarkBytes = 64L * 1024 * 1024;
    private readonly StorageDetector _storageDetector = new();
    private readonly MtRecommendationCalculator _calculator = new();
    private readonly MtBenchmarkCacheRepository _cacheRepository;

    public MtRecommendationService(MtBenchmarkCacheRepository? cacheRepository = null)
    {
        _cacheRepository = cacheRepository ?? new MtBenchmarkCacheRepository();
    }

    public MtRecommendation Recommend(string sourcePath, string destinationPath)
    {
        var sourceKind = _storageDetector.Detect(sourcePath);
        var destinationKind = _storageDetector.Detect(destinationPath);
        var sameVolume = _storageDetector.AreOnSameVolume(sourcePath, destinationPath);

        var sourceVolume = _storageDetector.GetVolumeKey(sourcePath);
        var destinationVolume = _storageDetector.GetVolumeKey(destinationPath);
        if (sourceVolume is not null && destinationVolume is not null)
        {
            try
            {
                var cached = _cacheRepository.FindBest(sourceVolume, destinationVolume, DateTimeOffset.Now);
                if (cached is not null)
                {
                    return new MtRecommendation(
                        cached.ThreadCount,
                        sourceKind,
                        destinationKind,
                        sameVolume,
                        MtRecommendationReason.BenchmarkCache);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A cache failure must not prevent copying or the heuristic recommendation.
            }
        }

        return _calculator.Calculate(sourceKind, destinationKind, sameVolume, Environment.ProcessorCount);
    }

    public MtRecommendation RecommendResourceConscious(string sourcePath, string destinationPath)
    {
        var sourceKind = _storageDetector.Detect(sourcePath);
        var destinationKind = _storageDetector.Detect(destinationPath);
        var sameVolume = _storageDetector.AreOnSameVolume(sourcePath, destinationPath);
        var sourceVolume = _storageDetector.GetVolumeKey(sourcePath);
        var destinationVolume = _storageDetector.GetVolumeKey(destinationPath);
        if (sourceVolume is not null && destinationVolume is not null)
        {
            try
            {
                var cached = _cacheRepository.FindRecommended(sourceVolume, destinationVolume, DateTimeOffset.Now);
                if (cached is not null)
                {
                    return new MtRecommendation(
                        cached.ThreadCount,
                        sourceKind,
                        destinationKind,
                        sameVolume,
                        MtRecommendationReason.BenchmarkCache);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A cache failure must not prevent the heuristic fallback.
            }
        }

        return _calculator.Calculate(sourceKind, destinationKind, sameVolume, Environment.ProcessorCount);
    }

    public MtRecommendation RecommendHighestPerformance(string sourcePath, string destinationPath)
    {
        var sourceKind = _storageDetector.Detect(sourcePath);
        var destinationKind = _storageDetector.Detect(destinationPath);
        var sameVolume = _storageDetector.AreOnSameVolume(sourcePath, destinationPath);
        var sourceVolume = _storageDetector.GetVolumeKey(sourcePath);
        var destinationVolume = _storageDetector.GetVolumeKey(destinationPath);
        if (sourceVolume is not null && destinationVolume is not null)
        {
            try
            {
                var cached = _cacheRepository.FindBest(sourceVolume, destinationVolume, DateTimeOffset.Now);
                if (cached is not null)
                {
                    return new MtRecommendation(
                        cached.ThreadCount,
                        sourceKind,
                        destinationKind,
                        sameVolume,
                        MtRecommendationReason.BenchmarkCache);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A cache failure must not prevent the heuristic fallback.
            }
        }

        return _calculator.Calculate(sourceKind, destinationKind, sameVolume, Environment.ProcessorCount);
    }

    public void RecordSuccessfulRun(
        string sourcePath,
        string destinationPath,
        int threadCount,
        long copiedBytes,
        TimeSpan elapsed)
    {
        if (copiedBytes < MinimumBenchmarkBytes || elapsed < TimeSpan.FromSeconds(2))
        {
            return;
        }

        var sourceVolume = _storageDetector.GetVolumeKey(sourcePath);
        var destinationVolume = _storageDetector.GetVolumeKey(destinationPath);
        if (sourceVolume is null || destinationVolume is null)
        {
            return;
        }

        try
        {
            _cacheRepository.Record(
                sourceVolume,
                destinationVolume,
                threadCount,
                copiedBytes / elapsed.TotalSeconds,
                DateTimeOffset.Now,
                sampleWeight: 3);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Copy completion remains successful even when performance history cannot be persisted.
        }
    }

    public void RecordBenchmarkMeasurements(
        string sourcePath,
        string destinationPath,
        IEnumerable<MtBenchmarkMeasurement> measurements)
    {
        var sourceVolume = _storageDetector.GetVolumeKey(sourcePath);
        var destinationVolume = _storageDetector.GetVolumeKey(destinationPath);
        if (sourceVolume is null || destinationVolume is null)
        {
            return;
        }

        foreach (var measurement in measurements.Where(item => item.BytesPerSecond > 0))
        {
            try
            {
                _cacheRepository.Record(
                    sourceVolume,
                    destinationVolume,
                    measurement.ThreadCount,
                    measurement.BytesPerSecond,
                    DateTimeOffset.Now,
                    sampleWeight: 1);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A cache failure must not turn a completed benchmark into a copy failure.
            }
        }
    }

    public IReadOnlyList<MtBenchmarkCacheEntry> GetBenchmarkHistory(
        string sourcePath,
        string destinationPath)
    {
        var sourceVolume = _storageDetector.GetVolumeKey(sourcePath);
        var destinationVolume = _storageDetector.GetVolumeKey(destinationPath);
        if (sourceVolume is null || destinationVolume is null)
        {
            return [];
        }

        try
        {
            return _cacheRepository.FindAll(sourceVolume, destinationVolume, DateTimeOffset.Now);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public bool RemoveBenchmarkHistory(
        string sourcePath,
        string destinationPath,
        int threadCount)
    {
        var sourceVolume = _storageDetector.GetVolumeKey(sourcePath);
        var destinationVolume = _storageDetector.GetVolumeKey(destinationPath);
        if (sourceVolume is null || destinationVolume is null)
        {
            return false;
        }

        try
        {
            return _cacheRepository.Remove(sourceVolume, destinationVolume, threadCount);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private MtRecommendation CalculateHeuristic(string sourcePath, string destinationPath)
    {
        var sourceKind = _storageDetector.Detect(sourcePath);
        var destinationKind = _storageDetector.Detect(destinationPath);
        var sameVolume = _storageDetector.AreOnSameVolume(sourcePath, destinationPath);
        return _calculator.Calculate(sourceKind, destinationKind, sameVolume, Environment.ProcessorCount);
    }
}
