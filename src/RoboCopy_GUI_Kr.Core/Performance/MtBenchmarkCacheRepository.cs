using System.Text.Json;

namespace RoboCopy_GUI_Kr.Core.Performance;

public sealed record MtBenchmarkCacheEntry(
    string SourceVolume,
    string DestinationVolume,
    int ThreadCount,
    double AverageBytesPerSecond,
    int SampleCount,
    DateTimeOffset TestedAt,
    double TotalWeight = 0,
    double LatestBytesPerSecond = 0);

public sealed class MtBenchmarkCacheRepository
{
    private static readonly TimeSpan MaximumAge = TimeSpan.FromDays(30);
    private readonly object _gate = new();
    private readonly string _filePath;

    public MtBenchmarkCacheRepository(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboCopy_GUI_Kr",
            "mt-benchmark-cache.json");
    }

    public MtBenchmarkCacheEntry? FindBest(string sourceVolume, string destinationVolume, DateTimeOffset now)
        => FindLowestWithinThreshold(sourceVolume, destinationVolume, now, 0.95);

    public MtBenchmarkCacheEntry? FindRecommended(string sourceVolume, string destinationVolume, DateTimeOffset now)
        => FindLowestWithinThreshold(sourceVolume, destinationVolume, now, 0.70);

    private MtBenchmarkCacheEntry? FindLowestWithinThreshold(
        string sourceVolume,
        string destinationVolume,
        DateTimeOffset now,
        double threshold)
    {
        lock (_gate)
        {
            var matchingEntries = LoadEntries()
                .Where(entry => string.Equals(entry.SourceVolume, sourceVolume, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.DestinationVolume, destinationVolume, StringComparison.OrdinalIgnoreCase)
                    && now - entry.TestedAt <= MaximumAge)
                .ToArray();
            if (matchingEntries.Length == 0)
            {
                return null;
            }

            var fastest = matchingEntries.Max(entry => entry.AverageBytesPerSecond);
            return matchingEntries
                .Where(entry => entry.AverageBytesPerSecond >= fastest * threshold)
                .OrderBy(entry => entry.ThreadCount)
                .First();
        }
    }

    public IReadOnlyList<MtBenchmarkCacheEntry> FindAll(
        string sourceVolume,
        string destinationVolume,
        DateTimeOffset now)
    {
        lock (_gate)
        {
            return LoadEntries()
                .Where(entry => string.Equals(entry.SourceVolume, sourceVolume, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.DestinationVolume, destinationVolume, StringComparison.OrdinalIgnoreCase)
                    && now - entry.TestedAt <= MaximumAge)
                .OrderBy(entry => entry.ThreadCount)
                .ToArray();
        }
    }

    public void Record(
        string sourceVolume,
        string destinationVolume,
        int threadCount,
        double bytesPerSecond,
        DateTimeOffset testedAt,
        double sampleWeight = 1)
    {
        if (threadCount is < 1 or > 128
            || !double.IsFinite(bytesPerSecond)
            || bytesPerSecond <= 0
            || !double.IsFinite(sampleWeight)
            || sampleWeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesPerSecond));
        }

        lock (_gate)
        {
            var entries = LoadEntries();
            entries.RemoveAll(entry => testedAt - entry.TestedAt > MaximumAge);
            var index = entries.FindIndex(entry =>
                string.Equals(entry.SourceVolume, sourceVolume, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.DestinationVolume, destinationVolume, StringComparison.OrdinalIgnoreCase)
                && entry.ThreadCount == threadCount);
            if (index >= 0)
            {
                var current = entries[index];
                var sampleCount = current.SampleCount + 1;
                var currentWeight = current.TotalWeight > 0
                    ? current.TotalWeight
                    : current.SampleCount;
                var totalWeight = currentWeight + sampleWeight;
                var average = ((current.AverageBytesPerSecond * currentWeight)
                    + (bytesPerSecond * sampleWeight)) / totalWeight;
                entries[index] = current with
                {
                    AverageBytesPerSecond = average,
                    SampleCount = sampleCount,
                    TestedAt = testedAt,
                    TotalWeight = totalWeight,
                    LatestBytesPerSecond = bytesPerSecond,
                };
            }
            else
            {
                entries.Add(new MtBenchmarkCacheEntry(
                    sourceVolume,
                    destinationVolume,
                    threadCount,
                    bytesPerSecond,
                    SampleCount: 1,
                    testedAt,
                    TotalWeight: sampleWeight,
                    LatestBytesPerSecond: bytesPerSecond));
            }

            SaveEntries(entries);
        }
    }

    public bool Remove(string sourceVolume, string destinationVolume)
    {
        lock (_gate)
        {
            var entries = LoadEntries();
            var removedCount = entries.RemoveAll(entry =>
                string.Equals(entry.SourceVolume, sourceVolume, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.DestinationVolume, destinationVolume, StringComparison.OrdinalIgnoreCase));
            if (removedCount == 0)
            {
                return false;
            }

            SaveEntries(entries);
            return true;
        }
    }

    public bool Remove(string sourceVolume, string destinationVolume, int threadCount)
    {
        lock (_gate)
        {
            var entries = LoadEntries();
            var removedCount = entries.RemoveAll(entry =>
                string.Equals(entry.SourceVolume, sourceVolume, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.DestinationVolume, destinationVolume, StringComparison.OrdinalIgnoreCase)
                && entry.ThreadCount == threadCount);
            if (removedCount == 0)
            {
                return false;
            }

            SaveEntries(entries);
            return true;
        }
    }

    private List<MtBenchmarkCacheEntry> LoadEntries()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<MtBenchmarkCacheEntry>>(File.ReadAllText(_filePath)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void SaveEntries(IReadOnlyCollection<MtBenchmarkCacheEntry> entries)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new DirectoryNotFoundException(_filePath);
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
