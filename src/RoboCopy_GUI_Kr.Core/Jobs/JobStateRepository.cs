using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoboCopy_GUI_Kr.Core.Jobs;

public sealed class JobStateRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _jobFilePath;

    public JobStateRepository(string? dataRoot = null)
    {
        var root = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboCopy_GUI_Kr");
        _jobFilePath = Path.Combine(root, "jobs", "running.json");
    }

    public void Save(PersistedJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var directory = Path.GetDirectoryName(_jobFilePath)
            ?? throw new InvalidOperationException("Job directory could not be resolved.");
        Directory.CreateDirectory(directory);

        var temporaryPath = _jobFilePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(job, SerializerOptions));
        File.Move(temporaryPath, _jobFilePath, overwrite: true);
    }

    public PersistedJob? Load()
    {
        if (!File.Exists(_jobFilePath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PersistedJob>(File.ReadAllText(_jobFilePath), SerializerOptions);
    }

    public void Delete()
    {
        if (File.Exists(_jobFilePath))
        {
            File.Delete(_jobFilePath);
        }
    }
}
