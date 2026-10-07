using System.Text.Json;

namespace RoboCopy_GUI_Kr.Core.ContinuousSync;

public sealed class ContinuousSyncRunRequestRepository
{
    private readonly string _requestPath;

    public ContinuousSyncRunRequestRepository(string? dataRoot = null)
    {
        var root = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboCopy_GUI_Kr");
        _requestPath = Path.Combine(root, "continuous-sync", "run-requests.json");
    }

    public void Enqueue(IEnumerable<Guid> configurationIds)
    {
        var requestedIds = configurationIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (requestedIds.Length == 0)
        {
            return;
        }

        WithLock(() =>
        {
            var ids = Read().Concat(requestedIds).Distinct().ToArray();
            var directory = Path.GetDirectoryName(_requestPath)
                ?? throw new InvalidOperationException("Continuous synchronization request directory could not be resolved.");
            Directory.CreateDirectory(directory);
            var temporaryPath = _requestPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(ids));
            File.Move(temporaryPath, _requestPath, overwrite: true);
        });
    }

    public IReadOnlyList<Guid> Consume()
    {
        IReadOnlyList<Guid> result = [];
        WithLock(() =>
        {
            result = Read();
            if (File.Exists(_requestPath))
            {
                File.Delete(_requestPath);
            }
        });
        return result;
    }

    private IReadOnlyList<Guid> Read()
    {
        if (!File.Exists(_requestPath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Guid[]>(File.ReadAllText(_requestPath)) ?? [];
    }

    private static void WithLock(Action action)
    {
        using var mutex = new Mutex(false, @"Local\RoboCopy_GUI_Kr_ContinuousSync_RunRequests");
        var entered = false;
        try
        {
            try
            {
                entered = mutex.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch (AbandonedMutexException)
            {
                entered = true;
            }

            if (!entered)
            {
                throw new TimeoutException("Timed out while accessing continuous synchronization requests.");
            }

            action();
        }
        finally
        {
            if (entered)
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
