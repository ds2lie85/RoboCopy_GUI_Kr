using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoboCopy_GUI_Kr.Core.Profiles;

public sealed class WorkProfileRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _profileFilePath;

    public WorkProfileRepository(string? dataRoot = null)
    {
        var root = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboCopy_GUI_Kr");
        _profileFilePath = Path.Combine(root, "profiles", "profiles.json");
    }

    public IReadOnlyList<WorkProfile> LoadAll()
    {
        if (!File.Exists(_profileFilePath))
        {
            return Array.Empty<WorkProfile>();
        }

        return (JsonSerializer.Deserialize<List<WorkProfile>>(
                File.ReadAllText(_profileFilePath),
                SerializerOptions)
            ?? []).OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public void Save(WorkProfile profile)
    {
        Validate(profile);
        var profiles = LoadAll().ToList();
        if (profiles.Any(existing =>
                existing.Id != profile.Id
                && string.Equals(existing.Name, profile.Name.Trim(), StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new InvalidOperationException("A profile with the same name already exists.");
        }

        var normalized = profile with
        {
            Name = profile.Name.Trim(),
            ExcludedDirectories = profile.ExcludedDirectories.ToArray(),
            ExcludedFiles = profile.ExcludedFiles.ToArray(),
            UpdatedAt = DateTimeOffset.Now,
        };
        var index = profiles.FindIndex(existing => existing.Id == normalized.Id);
        if (index >= 0)
        {
            profiles[index] = normalized;
        }
        else
        {
            profiles.Add(normalized);
        }

        WriteAll(profiles);
    }

    public void Delete(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Profile ID is required.", nameof(id));
        }

        var profiles = LoadAll().Where(profile => profile.Id != id).ToArray();
        WriteAll(profiles);
    }

    private static void Validate(WorkProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Id == Guid.Empty)
        {
            throw new ArgumentException("Profile ID is required.", nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Trim().Length > 80)
        {
            throw new ArgumentException("Profile name must contain 1 to 80 characters.", nameof(profile));
        }

        if (profile.MultiThreadCount is < 1 or > 128
            || profile.RetryCount is < 0 or > 999
            || profile.RetryWaitSeconds is < 0 or > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "Profile numeric options are out of range.");
        }
    }

    private void WriteAll(IEnumerable<WorkProfile> profiles)
    {
        var directory = Path.GetDirectoryName(_profileFilePath)
            ?? throw new InvalidOperationException("Profile directory could not be resolved.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _profileFilePath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(
                profiles.OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase),
                SerializerOptions));
        File.Move(temporaryPath, _profileFilePath, overwrite: true);
    }
}

