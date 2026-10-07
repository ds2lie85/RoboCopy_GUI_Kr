using System.Collections.Concurrent;
using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Progress;

public sealed class DestinationFileProgressMonitor : IDisposable
{
    private readonly string _sourceRoot;
    private readonly string _destinationRoot;
    private readonly IReadOnlyDictionary<string, long> _fileSizes;
    private readonly Action<RobocopyFileProgress> _progressChanged;
    private readonly ConcurrentDictionary<string, RobocopyFileProgress> _activeDestinations = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _completionTimer;
    private bool _disposed;

    public DestinationFileProgressMonitor(
        string sourceDirectory,
        string destinationDirectory,
        FileWorkloadSnapshot workload,
        Action<RobocopyFileProgress> progressChanged)
    {
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(progressChanged);

        _sourceRoot = NormalizeDirectory(sourceDirectory);
        _destinationRoot = NormalizeDirectory(destinationDirectory);
        _fileSizes = workload.FileSizes;
        _progressChanged = progressChanged;

        Directory.CreateDirectory(destinationDirectory);
        _watcher = new FileSystemWatcher(destinationDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            InternalBufferSize = 64 * 1024,
        };
        _watcher.Created += FileChanged;
        _watcher.Changed += FileChanged;
        _watcher.Renamed += FileRenamed;
        _watcher.EnableRaisingEvents = true;
        _completionTimer = new Timer(CheckForCompletedFiles, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    public bool TryCreateProgress(string destinationPath, out RobocopyFileProgress? progress)
    {
        progress = null;
        try
        {
            var fullDestinationPath = Path.GetFullPath(destinationPath);
            if (!fullDestinationPath.StartsWith(_destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var relativePath = Path.GetRelativePath(_destinationRoot, fullDestinationPath);
            if (relativePath == "." || relativePath.StartsWith("..", StringComparison.Ordinal))
            {
                return false;
            }

            var sourcePath = Path.GetFullPath(Path.Combine(_sourceRoot, relativePath));
            if (!_fileSizes.TryGetValue(sourcePath, out var sourceLength))
            {
                return false;
            }

            var destinationLength = new FileInfo(fullDestinationPath).Length;
            var measuredPercent = sourceLength <= 0 ? 99d : destinationLength * 100d / sourceLength;

            // Robocopy can preallocate the final file length before all bytes are written.
            // Keep destination observations below 100%; the Robocopy completion line removes the row.
            var percent = Math.Clamp(measuredPercent, 0, 99);
            progress = new RobocopyFileProgress(
                Path.GetFileName(sourcePath),
                percent,
                sourcePath,
                fullDestinationPath,
                string.Empty);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= FileChanged;
        _watcher.Changed -= FileChanged;
        _watcher.Renamed -= FileRenamed;
        _watcher.Dispose();
        _completionTimer.Dispose();
        _activeDestinations.Clear();
    }

    private void FileChanged(object sender, FileSystemEventArgs e) => Publish(e.FullPath);

    private void FileRenamed(object sender, RenamedEventArgs e) => Publish(e.FullPath);

    private void Publish(string destinationPath)
    {
        if (!_disposed && TryCreateProgress(destinationPath, out var progress) && progress is not null)
        {
            _activeDestinations[progress.DestinationPath] = progress;
            _progressChanged(progress);
        }
    }

    private void CheckForCompletedFiles(object? state)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var entry in _activeDestinations)
        {
            if (!CanOpenCompletedFile(entry.Value))
            {
                continue;
            }

            if (_activeDestinations.TryRemove(entry.Key, out var completed))
            {
                _progressChanged(completed with { Percent = 100 });
            }
        }
    }

    private bool CanOpenCompletedFile(RobocopyFileProgress progress)
    {
        try
        {
            if (!_fileSizes.TryGetValue(progress.SourcePath, out var sourceLength))
            {
                return false;
            }

            var destinationInfo = new FileInfo(progress.DestinationPath);
            if (!destinationInfo.Exists || destinationInfo.Length != sourceLength)
            {
                return false;
            }

            using var stream = new FileStream(
                progress.DestinationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                bufferSize: 1,
                FileOptions.SequentialScan);
            return stream.Length == sourceLength;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim())) + Path.DirectorySeparatorChar;
    }
}
