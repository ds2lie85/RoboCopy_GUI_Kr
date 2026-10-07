using System.Text.Json;

namespace RoboCopy_GUI_Kr.Core.ContinuousSync;

public sealed class ContinuousSyncNotificationRepository
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ContinuousSyncNotificationRepository(string? dataRoot = null)
    {
        var root = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboCopy_GUI_Kr",
            "continuous-sync");
        _filePath = Path.Combine(root, "notifications.json");
    }

    public IReadOnlyList<ContinuousSyncNotification> LoadAll(DateTimeOffset? now = null)
    {
        var cutoff = (now ?? DateTimeOffset.Now) - Retention;
        var notifications = ReadAll()
            .Where(item => item.CreatedAt >= cutoff)
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();
        WriteAll(notifications);
        return notifications;
    }

    public void Add(ContinuousSyncNotification notification, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var cutoff = (now ?? DateTimeOffset.Now) - Retention;
        var notifications = ReadAll()
            .Where(item => item.CreatedAt >= cutoff)
            .Prepend(notification)
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();
        WriteAll(notifications);
    }

    private IReadOnlyList<ContinuousSyncNotification> ReadAll()
    {
        if (!File.Exists(_filePath)) return [];
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<ContinuousSyncNotification[]>(json, _jsonOptions) ?? [];
    }

    private void WriteAll(IReadOnlyList<ContinuousSyncNotification> notifications)
    {
        var directory = Path.GetDirectoryName(_filePath) ?? throw new InvalidOperationException("Notification path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(notifications, _jsonOptions));
        File.Move(temporaryPath, _filePath, true);
    }
}
