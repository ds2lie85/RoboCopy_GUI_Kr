using System.Globalization;

namespace RoboCopy_GUI_Kr.Core.Robocopy;

public sealed record RobocopySummaryAggregate(
    IReadOnlyList<long> Directories,
    IReadOnlyList<long> Files,
    IReadOnlyList<long> Bytes,
    IReadOnlyList<TimeSpan> Times,
    TimeSpan Elapsed,
    int OperationCount)
{
    public double BytesPerSecond => Elapsed.TotalSeconds <= 0 ? 0 : Bytes.ElementAtOrDefault(1) / Elapsed.TotalSeconds;
}

public sealed class RobocopySummaryAggregator
{
    private readonly long[] _directories = new long[6];
    private readonly long[] _files = new long[6];
    private readonly long[] _bytes = new long[6];
    private readonly TimeSpan[] _times = new TimeSpan[6];
    private TimeSpan _elapsed;
    private int _operationCount;

    public void Add(RobocopySummaryData summary, TimeSpan elapsed, bool includeDirectories = true)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (includeDirectories)
        {
            AddValues(_directories, summary.Directories);
        }
        AddValues(_files, summary.Files);
        AddValues(_bytes, summary.Bytes);
        AddTimes(_times, summary.Times);
        _elapsed += elapsed;
        _operationCount++;
    }

    public void AddCopiedFile(long size, TimeSpan elapsed)
    {
        _files[0]++;
        _files[1]++;
        _bytes[0] += Math.Max(0, size);
        _bytes[1] += Math.Max(0, size);
        _elapsed += elapsed;
        _operationCount++;
    }

    public RobocopySummaryAggregate Build() => new(
        _directories.ToArray(),
        _files.ToArray(),
        _bytes.ToArray(),
        _times.ToArray(),
        _elapsed,
        _operationCount);

    private static void AddValues(long[] totals, IReadOnlyList<string> values)
    {
        for (var index = 0; index < Math.Min(totals.Length, values.Count); index++)
        {
            var normalized = values[index].Replace(",", string.Empty, StringComparison.Ordinal).Trim();
            if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                totals[index] += value;
            }
        }
    }

    private static void AddTimes(TimeSpan[] totals, IReadOnlyList<string> values)
    {
        for (var index = 0; index < Math.Min(totals.Length, values.Count); index++)
        {
            if (TimeSpan.TryParse(values[index], CultureInfo.InvariantCulture, out var value))
            {
                totals[index] += value;
            }
        }
    }
}
