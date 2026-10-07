using System.Globalization;

namespace RoboCopy_GUI_Kr.Core.Sync;

public sealed record OneWaySyncQuarantineItem(
    string QuarantinePath,
    string OriginalDestinationPath,
    string RelativePath,
    DateTimeOffset QuarantinedAt,
    long Size,
    DateTime CreationTimeUtc,
    DateTime LastWriteTimeUtc);

public sealed record OneWaySyncRestoreFailure(string Path, string Reason);

public sealed record OneWaySyncRestoreResult(
    IReadOnlyList<string> RestoredRelativePaths,
    IReadOnlyList<OneWaySyncRestoreFailure> Failures)
{
    public bool IsSuccess => Failures.Count == 0;
}

public sealed record OneWaySyncPermanentDeleteResult(
    int DeletedFiles,
    IReadOnlyList<OneWaySyncRestoreFailure> Failures)
{
    public bool IsSuccess => Failures.Count == 0;
}

public sealed class OneWaySyncQuarantineService
{
    private const string BatchFormat = "yyyyMMdd'T'HHmmssfff'Z'";

    public Task<OneWaySyncDeletionResult> QuarantineSelectedAsync(
        string sourceRoot,
        string destinationRoot,
        IReadOnlyCollection<OneWaySyncExtraItem> selectedItems,
        Guid taskId,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var fullSourceRoot = Path.GetFullPath(sourceRoot);
        var fullDestinationRoot = Path.GetFullPath(destinationRoot);
        var quarantineRoot = GetQuarantineRoot(fullDestinationRoot, taskId);
        EnsureHiddenDirectory(quarantineRoot);
        var batchRoot = Path.Combine(
            quarantineRoot,
            DateTimeOffset.UtcNow.ToString(BatchFormat, CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N"));
        var failures = new List<OneWaySyncDeletionFailure>();
        var movedFiles = 0;
        var movedDirectories = 0;

        foreach (var item in selectedItems.Where(item => !item.IsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var destinationPath = EnsureWithinRoot(item.DestinationPath, fullDestinationRoot);
                var relativePath = Path.GetRelativePath(fullDestinationRoot, destinationPath);
                var sourcePath = Path.Combine(fullSourceRoot, relativePath);
                if (!File.Exists(sourcePath) && File.Exists(destinationPath))
                {
                    var quarantinePath = Path.Combine(batchRoot, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(quarantinePath)!);
                    File.Move(destinationPath, quarantinePath);
                    movedFiles++;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                failures.Add(new OneWaySyncDeletionFailure(item.DestinationPath, exception.Message));
            }
        }

        foreach (var item in selectedItems.Where(item => item.IsDirectory).OrderByDescending(item => item.DestinationPath.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var destinationPath = EnsureWithinRoot(item.DestinationPath, fullDestinationRoot);
                var relativePath = Path.GetRelativePath(fullDestinationRoot, destinationPath);
                var sourcePath = Path.Combine(fullSourceRoot, relativePath);
                if (!Directory.Exists(sourcePath)
                    && Directory.Exists(destinationPath)
                    && !Directory.EnumerateFileSystemEntries(destinationPath).Any())
                {
                    Directory.CreateDirectory(Path.Combine(batchRoot, relativePath));
                    Directory.Delete(destinationPath);
                    movedDirectories++;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                failures.Add(new OneWaySyncDeletionFailure(item.DestinationPath, exception.Message));
            }
        }

        if (movedFiles == 0 && movedDirectories == 0 && Directory.Exists(batchRoot))
        {
            Directory.Delete(batchRoot, recursive: true);
        }

        return new OneWaySyncDeletionResult(movedFiles, movedDirectories, failures);
    }, cancellationToken);

    public void PurgeExpired(string destinationRoot, Guid taskId, TimeSpan retention, DateTimeOffset now)
    {
        var quarantineRoot = GetQuarantineRoot(destinationRoot, taskId);
        if (!Directory.Exists(quarantineRoot))
        {
            return;
        }
        EnsureHiddenDirectory(quarantineRoot);

        foreach (var batchPath in Directory.EnumerateDirectories(quarantineRoot))
        {
            var name = Path.GetFileName(batchPath);
            var separator = name.IndexOf('_');
            if (separator <= 0
                || !DateTimeOffset.TryParseExact(
                    name[..separator],
                    BatchFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var created)
                || now - created < retention)
            {
                continue;
            }

            Directory.Delete(batchPath, recursive: true);
        }
    }

    public IReadOnlyList<OneWaySyncQuarantineItem> GetItems(string destinationRoot, Guid taskId)
    {
        var quarantineRoot = GetQuarantineRoot(destinationRoot, taskId);
        if (!Directory.Exists(quarantineRoot))
        {
            return [];
        }
        EnsureHiddenDirectory(quarantineRoot);

        var items = new List<OneWaySyncQuarantineItem>();
        foreach (var batchPath in Directory.EnumerateDirectories(quarantineRoot))
        {
            var name = Path.GetFileName(batchPath);
            var separator = name.IndexOf('_');
            if (separator <= 0
                || !DateTimeOffset.TryParseExact(
                    name[..separator],
                    BatchFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var quarantinedAt))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(batchPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(batchPath, path);
                items.Add(new OneWaySyncQuarantineItem(
                    path,
                    Path.Combine(Path.GetFullPath(destinationRoot), relativePath),
                    relativePath,
                    quarantinedAt,
                    new FileInfo(path).Length,
                    File.GetCreationTimeUtc(path),
                    File.GetLastWriteTimeUtc(path)));
            }
        }

        return items
            .OrderByDescending(item => item.QuarantinedAt)
            .ThenBy(item => item.RelativePath, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<OneWaySyncExtraItem> ExcludeEquivalentPreviouslyQuarantinedFiles(
        string destinationRoot,
        Guid taskId,
        IReadOnlyList<OneWaySyncExtraItem> items)
    {
        var identities = GetItems(destinationRoot, taskId)
            .ToLookup(item => item.RelativePath, StringComparer.OrdinalIgnoreCase);
        return items.Where(item => item.IsDirectory || !IsEquivalent(item, identities[item.RelativePath])).ToArray();
    }

    public Task<OneWaySyncRestoreResult> RestoreAsync(
        string destinationRoot,
        Guid taskId,
        IReadOnlyCollection<OneWaySyncQuarantineItem> selectedItems,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var fullDestinationRoot = Path.GetFullPath(destinationRoot);
        var quarantineRoot = GetQuarantineRoot(fullDestinationRoot, taskId);
        var restored = new List<string>();
        var failures = new List<OneWaySyncRestoreFailure>();
        foreach (var item in selectedItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var quarantinePath = EnsureWithinRoot(item.QuarantinePath, quarantineRoot);
                var originalPath = EnsureWithinRoot(item.OriginalDestinationPath, fullDestinationRoot);
                if (!File.Exists(quarantinePath))
                {
                    throw new FileNotFoundException("The quarantined file no longer exists.", quarantinePath);
                }

                if (File.Exists(originalPath) || Directory.Exists(originalPath))
                {
                    throw new IOException("A file or folder already exists at the original location.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
                File.Move(quarantinePath, originalPath);
                restored.Add(item.RelativePath);
                RemoveEmptyParents(Path.GetDirectoryName(quarantinePath), quarantineRoot);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                failures.Add(new OneWaySyncRestoreFailure(item.QuarantinePath, exception.Message));
            }
        }

        return new OneWaySyncRestoreResult(restored, failures);
    }, cancellationToken);

    public Task<OneWaySyncPermanentDeleteResult> DeletePermanentlyAsync(
        string destinationRoot,
        Guid taskId,
        IReadOnlyCollection<OneWaySyncQuarantineItem> selectedItems,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var quarantineRoot = GetQuarantineRoot(destinationRoot, taskId);
        var deleted = 0;
        var failures = new List<OneWaySyncRestoreFailure>();
        foreach (var item in selectedItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var path = EnsureWithinRoot(item.QuarantinePath, quarantineRoot);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted++;
                    RemoveEmptyParents(Path.GetDirectoryName(path), quarantineRoot);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                failures.Add(new OneWaySyncRestoreFailure(item.QuarantinePath, exception.Message));
            }
        }
        return new OneWaySyncPermanentDeleteResult(deleted, failures);
    }, cancellationToken);

    public static string GetQuarantineRoot(string destinationRoot, Guid taskId)
    {
        return Path.Combine(GetQuarantineContainer(destinationRoot), taskId.ToString("N"));
    }

    public static string GetQuarantineContainer(string destinationRoot)
    {
        var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationRoot));
        var parent = Path.GetDirectoryName(destination) ?? Path.GetPathRoot(destination)
            ?? throw new ArgumentException("Destination root could not be resolved.", nameof(destinationRoot));
        var name = Path.GetFileName(destination);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "DriveRoot";
        }

        return Path.Combine(parent, $".{name}.RoboCopy_GUI_Kr_Quarantine");
    }

    private static bool IsEquivalent(
        OneWaySyncExtraItem item,
        IEnumerable<OneWaySyncQuarantineItem> quarantinedItems)
    {
        if (!File.Exists(item.DestinationPath)) return false;
        var info = new FileInfo(item.DestinationPath);
        return quarantinedItems.Any(quarantined =>
            quarantined.Size == info.Length
            && quarantined.CreationTimeUtc == info.CreationTimeUtc
            && quarantined.LastWriteTimeUtc == info.LastWriteTimeUtc);
    }

    private static string EnsureWithinRoot(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The quarantine target is outside the destination root.", nameof(path));
        }

        return fullPath;
    }

    private static void RemoveEmptyParents(string? path, string stopAt)
    {
        var normalizedStop = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stopAt));
        while (!string.IsNullOrWhiteSpace(path)
               && Directory.Exists(path)
               && !Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).Equals(normalizedStop, StringComparison.OrdinalIgnoreCase)
               && !Directory.EnumerateFileSystemEntries(path).Any())
        {
            var parent = Path.GetDirectoryName(path);
            Directory.Delete(path);
            path = parent;
        }
    }

    private static void EnsureHiddenDirectory(string path)
    {
        var parent = Directory.GetParent(path)?.FullName;
        if (parent is not null && Path.GetFileName(parent).EndsWith(".RoboCopy_GUI_Kr_Quarantine", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(parent);
            File.SetAttributes(parent, File.GetAttributes(parent) | FileAttributes.Hidden);
        }
        Directory.CreateDirectory(path);
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Hidden) == 0)
        {
            File.SetAttributes(path, attributes | FileAttributes.Hidden);
        }
    }
}
