using System.Buffers;
using System.Diagnostics;

namespace RoboCopy_GUI_Kr.Core.Performance;

public sealed record MtBenchmarkMeasurement(
    int ThreadCount,
    long CopiedBytes,
    TimeSpan Duration)
{
    public double BytesPerSecond => Duration.TotalSeconds <= 0
        ? 0
        : CopiedBytes / Duration.TotalSeconds;
}

public sealed record MtBenchmarkProgress(
    MtBenchmarkStage Stage,
    int CandidateIndex,
    int CandidateCount,
    int ThreadCount);

public enum MtBenchmarkStage
{
    Sequential,
    MixedFiles,
}

public sealed record MtBenchmarkResult(
    IReadOnlyList<MtBenchmarkMeasurement> Measurements,
    int RecommendedThreadCount,
    long SampleBytes,
    int MixedSampleCount,
    MtBenchmarkMeasurement SequentialMeasurement);

public sealed class MtBenchmarkService
{
    private const long MaximumSampleBytes = 128L * 1024 * 1024;
    private const int MixedSampleCount = 100;
    private const int MaximumScannedFiles = 20_000;
    private const double MinimumMeaningfulImprovement = 0.05;
    private static readonly int[] CandidateThreadCounts = [1, 2, 4, 8, 16, 32, 64, 128];

    public async Task<MtBenchmarkResult> RunAsync(
        string sourceDirectory,
        string destinationDirectory,
        IProgress<MtBenchmarkProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var sampleSet = await Task.Run(
            () => SelectSamples(sourceDirectory, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        if (sampleSet.MixedSamples.Count == 0)
        {
            throw new InvalidOperationException("No readable non-empty files were found for the benchmark.");
        }

        var candidates = CandidateThreadCounts.ToList();

        var benchmarkRoot = Path.Combine(
            destinationDirectory,
            $".RoboCopy_GUI_Kr_Benchmark_{Guid.NewGuid():N}");
        Directory.CreateDirectory(benchmarkRoot);
        var measurements = new List<MtBenchmarkMeasurement>();
        var consecutiveSmallImprovements = 0;
        MtBenchmarkMeasurement sequentialMeasurement;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new MtBenchmarkProgress(
                MtBenchmarkStage.Sequential,
                1,
                candidates.Count + 1,
                1));
            var sequentialDirectory = Path.Combine(benchmarkRoot, "sequential");
            Directory.CreateDirectory(sequentialDirectory);
            var sequentialStopwatch = Stopwatch.StartNew();
            long sequentialBytes;
            try
            {
                sequentialBytes = await CopyChunkAsync(
                    sampleSet.SequentialSample,
                    Path.Combine(sequentialDirectory, "large-sample.bin"),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                sequentialStopwatch.Stop();
            }

            sequentialMeasurement = new MtBenchmarkMeasurement(1, sequentialBytes, sequentialStopwatch.Elapsed);
            await DeleteBenchmarkDirectoryAsync(benchmarkRoot, sequentialDirectory).ConfigureAwait(false);

            for (var index = 0; index < candidates.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var threadCount = candidates[index];
                progress?.Report(new MtBenchmarkProgress(
                    MtBenchmarkStage.MixedFiles,
                    index + 2,
                    candidates.Count + 1,
                    threadCount));
                var candidateDirectory = Path.Combine(benchmarkRoot, $"mt-{threadCount}");
                Directory.CreateDirectory(candidateDirectory);

                var stopwatch = Stopwatch.StartNew();
                long copiedBytes = 0;
                try
                {
                    await Parallel.ForEachAsync(
                        sampleSet.MixedSamples.Select((sample, sampleIndex) => (sample, sampleIndex)),
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = threadCount,
                            CancellationToken = cancellationToken,
                        },
                        async (item, token) =>
                        {
                            var outputPath = Path.Combine(candidateDirectory, $"sample-{item.sampleIndex:D4}.bin");
                            var copied = await CopyChunkAsync(item.sample, outputPath, token).ConfigureAwait(false);
                            Interlocked.Add(ref copiedBytes, copied);
                        }).ConfigureAwait(false);
                }
                finally
                {
                    stopwatch.Stop();
                }

                var measurement = new MtBenchmarkMeasurement(
                    threadCount,
                    copiedBytes,
                    stopwatch.Elapsed);
                measurements.Add(measurement);
                await DeleteBenchmarkDirectoryAsync(benchmarkRoot, candidateDirectory).ConfigureAwait(false);

                if (measurements.Count >= 2)
                {
                    var previous = measurements[^2].BytesPerSecond;
                    var improvement = previous <= 0
                        ? 1
                        : (measurement.BytesPerSecond - previous) / previous;
                    consecutiveSmallImprovements = improvement < MinimumMeaningfulImprovement
                        ? consecutiveSmallImprovements + 1
                        : 0;
                    if (consecutiveSmallImprovements >= 2)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            if (Directory.Exists(benchmarkRoot))
            {
                await DeleteBenchmarkDirectoryAsync(benchmarkRoot, benchmarkRoot).ConfigureAwait(false);
            }
        }

        var fastest = measurements.Max(measurement => measurement.BytesPerSecond);
        var recommended = measurements
            .Where(measurement => measurement.BytesPerSecond >= fastest * 0.95)
            .OrderBy(measurement => measurement.ThreadCount)
            .First();
        return new MtBenchmarkResult(
            measurements,
            recommended.ThreadCount,
            sampleSet.MixedSamples.Sum(sample => sample.Length),
            sampleSet.MixedSamples.Count,
            sequentialMeasurement);
    }

    private static async Task DeleteBenchmarkDirectoryAsync(string benchmarkRoot, string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(benchmarkRoot));
        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!target.Equals(root, StringComparison.OrdinalIgnoreCase)
            && !target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Cleanup target is outside the benchmark directory.", nameof(path));

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
                return;
            }
            catch (DirectoryNotFoundException) { return; }
            catch (IOException exception) when (attempt < 7 && (exception.HResult & 0xffff) is 32 or 33)
            {
                // Temporary file users (e.g. scanners) may briefly retain handles after
                // our streams close. Never force-close them or alter access permissions.
                await Task.Delay(100 * (attempt + 1)).ConfigureAwait(false);
            }
        }
    }

    private static MtBenchmarkSampleSet SelectSamples(
        string sourceDirectory,
        CancellationToken cancellationToken)
    {
        var files = new List<FileInfo>();
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        var scanned = 0;
        foreach (var path in Directory.EnumerateFiles(sourceDirectory, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++scanned > MaximumScannedFiles)
            {
                break;
            }

            try
            {
                var file = new FileInfo(path);
                if (file.Length == 0)
                {
                    continue;
                }

                if (files.Count < 1_000 && CanReadSample(file.FullName))
                {
                    files.Add(file);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // An inaccessible sample must not stop discovery of other readable files.
            }
        }

        if (files.Count == 0)
        {
            return new MtBenchmarkSampleSet([], new MtBenchmarkSample(string.Empty, 0, 0));
        }

        var largestFile = files.MaxBy(file => file.Length)!;
        var sequentialSample = new MtBenchmarkSample(
            largestFile.FullName,
            0,
            Math.Min(largestFile.Length, MaximumSampleBytes));
        var random = new Random(0x524F424F);
        var samples = new List<MtBenchmarkSample>(MixedSampleCount);
        long totalBytes = 0;
        for (var index = 0; index < MixedSampleCount && totalBytes < MaximumSampleBytes; index++)
        {
            var file = files[random.Next(files.Count)];
            var maximumLength = Math.Min(file.Length, 2L * 1024 * 1024);
            var minimumLength = Math.Min(maximumLength, 64L * 1024);
            var length = minimumLength == maximumLength
                ? maximumLength
                : random.NextInt64(minimumLength, maximumLength + 1);
            if (totalBytes + length > MaximumSampleBytes)
            {
                length = MaximumSampleBytes - totalBytes;
            }

            var maximumOffset = file.Length - length;
            var offset = maximumOffset <= 0 ? 0 : random.NextInt64(0, maximumOffset + 1);
            samples.Add(new MtBenchmarkSample(file.FullName, offset, length));
            totalBytes += length;
        }

        return new MtBenchmarkSampleSet(samples, sequentialSample);
    }

    private static bool CanReadSample(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1,
                FileOptions.RandomAccess);
            _ = stream.ReadByte();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async ValueTask<long> CopyChunkAsync(
        MtBenchmarkSample sample,
        string outputPath,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 128 * 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            await using var source = new FileStream(
                sample.SourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.RandomAccess);
            await using var destination = new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            source.Seek(sample.Offset, SeekOrigin.Begin);

            var remaining = sample.Length;
            long copiedBytes = 0;
            while (remaining > 0)
            {
                var read = await source.ReadAsync(
                    buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                remaining -= read;
                copiedBytes += read;
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            return copiedBytes;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private sealed record MtBenchmarkSample(string SourcePath, long Offset, long Length);

    private sealed record MtBenchmarkSampleSet(
        IReadOnlyList<MtBenchmarkSample> MixedSamples,
        MtBenchmarkSample SequentialSample);
}
