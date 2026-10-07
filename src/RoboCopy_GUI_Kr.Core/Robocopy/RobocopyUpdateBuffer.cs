using System.Collections.Concurrent;

namespace RoboCopy_GUI_Kr.Core.Robocopy;

public sealed record RobocopyUpdateBatch(
    IReadOnlyList<string> OutputLines,
    double? OverallProgress,
    IReadOnlyList<RobocopyFileProgress> FileProgressUpdates,
    bool UpdatesWereDropped);

/// <summary>
/// 빠르게 발생하는 Robocopy 출력을 유한하게 보관하고 진행률은 최신 값으로 병합합니다.
/// </summary>
public sealed class RobocopyUpdateBuffer
{
    private readonly object _fileProgressGate = new();
    private readonly object _overallProgressGate = new();
    private readonly ConcurrentQueue<string> _outputLines = new();
    private readonly Dictionary<string, RobocopyFileProgress> _fileProgressBySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maximumPendingOutputLines;
    private readonly int _maximumPendingFileUpdates;
    private int _pendingOutputCount;
    private int _updatesWereDropped;
    private double? _latestOverallProgress;

    public RobocopyUpdateBuffer(int maximumPendingOutputLines, int maximumPendingFileUpdates)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPendingOutputLines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPendingFileUpdates);
        _maximumPendingOutputLines = maximumPendingOutputLines;
        _maximumPendingFileUpdates = maximumPendingFileUpdates;
    }

    public bool HasPendingUpdates
    {
        get
        {
            if (Volatile.Read(ref _pendingOutputCount) > 0 || Volatile.Read(ref _updatesWereDropped) != 0)
            {
                return true;
            }

            lock (_overallProgressGate)
            {
                if (_latestOverallProgress.HasValue)
                {
                    return true;
                }
            }

            lock (_fileProgressGate)
            {
                return _fileProgressBySource.Count > 0;
            }
        }
    }

    public void EnqueueOutput(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _outputLines.Enqueue(line);
        Interlocked.Increment(ref _pendingOutputCount);

        while (Volatile.Read(ref _pendingOutputCount) > _maximumPendingOutputLines
               && _outputLines.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _pendingOutputCount);
            Interlocked.Exchange(ref _updatesWereDropped, 1);
        }
    }

    public void EnqueueOverallProgress(double percent)
    {
        lock (_overallProgressGate)
        {
            _latestOverallProgress = percent;
        }
    }

    public void EnqueueFileProgress(RobocopyFileProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        lock (_fileProgressGate)
        {
            if (_fileProgressBySource.TryGetValue(progress.SourcePath, out var pendingProgress)
                && pendingProgress.Percent > progress.Percent)
            {
                return;
            }

            if (!_fileProgressBySource.ContainsKey(progress.SourcePath)
                && _fileProgressBySource.Count >= _maximumPendingFileUpdates)
            {
                Interlocked.Exchange(ref _updatesWereDropped, 1);
                return;
            }

            _fileProgressBySource[progress.SourcePath] = progress;
        }
    }

    public RobocopyUpdateBatch Drain(int maximumOutputLines, int maximumFileUpdates)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputLines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFileUpdates);

        var outputLines = new List<string>(Math.Min(maximumOutputLines, Volatile.Read(ref _pendingOutputCount)));
        while (outputLines.Count < maximumOutputLines && _outputLines.TryDequeue(out var line))
        {
            Interlocked.Decrement(ref _pendingOutputCount);
            outputLines.Add(line);
        }

        double? overallProgress;
        lock (_overallProgressGate)
        {
            overallProgress = _latestOverallProgress;
            _latestOverallProgress = null;
        }

        List<RobocopyFileProgress> fileProgressUpdates;
        lock (_fileProgressGate)
        {
            fileProgressUpdates = _fileProgressBySource.Values
                .Take(maximumFileUpdates)
                .ToList();
            foreach (var progress in fileProgressUpdates)
            {
                _fileProgressBySource.Remove(progress.SourcePath);
            }
        }

        return new RobocopyUpdateBatch(
            outputLines,
            overallProgress,
            fileProgressUpdates,
            Interlocked.Exchange(ref _updatesWereDropped, 0) != 0);
    }

    public void Clear()
    {
        while (_outputLines.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref _pendingOutputCount, 0);
        Interlocked.Exchange(ref _updatesWereDropped, 0);

        lock (_overallProgressGate)
        {
            _latestOverallProgress = null;
        }

        lock (_fileProgressGate)
        {
            _fileProgressBySource.Clear();
        }
    }
}
