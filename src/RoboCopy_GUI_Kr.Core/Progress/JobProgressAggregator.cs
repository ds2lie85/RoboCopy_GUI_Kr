using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Progress;

public sealed class JobProgressAggregator
{
    private readonly object _gate = new();
    private readonly IReadOnlyDictionary<string, long> _fileSizes;
    private readonly Dictionary<string, double> _latestFractions = new(StringComparer.OrdinalIgnoreCase);
    private readonly double _totalWeight;
    private double _completedWeight;

    public JobProgressAggregator(FileWorkloadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _fileSizes = snapshot.FileSizes;
        _totalWeight = snapshot.FileSizes.Values.Sum(size => Math.Max(1d, size));
    }

    public double Report(RobocopyFileProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        lock (_gate)
        {
            if (_totalWeight <= 0 || !_fileSizes.TryGetValue(progress.SourcePath, out var fileSize))
            {
                return 0;
            }

            var fraction = Math.Clamp(progress.Percent / 100d, 0, 1);
            _latestFractions.TryGetValue(progress.SourcePath, out var previousFraction);
            if (fraction > previousFraction)
            {
                _completedWeight += Math.Max(1d, fileSize) * (fraction - previousFraction);
                _latestFractions[progress.SourcePath] = fraction;
            }

            return Math.Clamp((_completedWeight / _totalWeight) * 100d, 0, 100);
        }
    }
}
