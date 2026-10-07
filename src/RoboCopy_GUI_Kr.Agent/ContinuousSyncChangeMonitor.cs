using System.IO;

namespace RoboCopy_GUI_Kr.Agent;

internal sealed class ContinuousSyncChangeMonitor : IDisposable
{
    private readonly FileSystemWatcher? _sourceWatcher;
    private readonly FileSystemWatcher? _destinationWatcher;
    private long _version = 1;
    private long _processedVersion;

    public ContinuousSyncChangeMonitor(string sourcePath, string destinationPath)
    {
        SourcePath = Path.GetFullPath(sourcePath);
        DestinationPath = Path.GetFullPath(destinationPath);
        try
        {
            _sourceWatcher = CreateWatcher(SourcePath);
            _destinationWatcher = CreateWatcher(DestinationPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            IsFallback = true;
            _sourceWatcher?.Dispose();
            _destinationWatcher?.Dispose();
        }
    }

    public string SourcePath { get; }
    public string DestinationPath { get; }
    public bool IsFallback { get; }
    public bool IsDirty => IsFallback || Interlocked.Read(ref _version) != Interlocked.Read(ref _processedVersion);
    public long CaptureVersion() => Interlocked.Read(ref _version);
    public void MarkProcessed(long version) => Interlocked.Exchange(ref _processedVersion, version);

    public bool Matches(string sourcePath, string destinationPath) =>
        string.Equals(SourcePath, Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase)
        && string.Equals(DestinationPath, Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase);

    private FileSystemWatcher CreateWatcher(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size
                           | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            InternalBufferSize = 32 * 1024,
        };
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnError;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => Interlocked.Increment(ref _version);
    private void OnRenamed(object sender, RenamedEventArgs e) => Interlocked.Increment(ref _version);
    private void OnError(object sender, ErrorEventArgs e) => Interlocked.Increment(ref _version);

    public void Dispose()
    {
        _sourceWatcher?.Dispose();
        _destinationWatcher?.Dispose();
    }
}
