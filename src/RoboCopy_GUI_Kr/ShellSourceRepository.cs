using System.IO;
using System.Text.Json;
using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr;

internal sealed class ShellSourceRepository
{
    private readonly string _filePath;

    public ShellSourceRepository(string? baseDirectory = null)
    {
        var root = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboCopy_GUI_Kr",
            "shell");
        _filePath = Path.Combine(root, "source.json");
    }

    public void Save(string sourceDirectory) => SaveSelection([sourceDirectory]);

    public void SaveSelection(IEnumerable<string> selectedPaths)
    {
        var paths = selectedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0)
        {
            throw new ArgumentException("At least one selected path is required.", nameof(selectedPaths));
        }

        Write(new StoredSelection
        {
            Paths = paths,
            SelectedAt = DateTimeOffset.UtcNow,
        });
    }

    public void AppendSelectionInvocation(IEnumerable<string> selectedPaths, TimeSpan batchWindow)
    {
        var incoming = selectedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (incoming.Length == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var stored = Read();
        var paths = stored is null
            ? incoming
            : ShellSelectionBatchMerger.Merge(stored.Paths, stored.SelectedAt, incoming, now, batchWindow);
        Write(new StoredSelection { Paths = paths, SelectedAt = now });
    }

    public string? Load() => LoadSelection().FirstOrDefault();

    public IReadOnlyList<string> LoadSelection()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var selection = Read();
        if (selection is null)
        {
            return [];
        }

        // Path reads documents written by the single-folder implementation.
        return selection.Paths.Count > 0
            ? selection.Paths
            : string.IsNullOrWhiteSpace(selection.Path) ? [] : [selection.Path];
    }

    private StoredSelection? Read() => !File.Exists(_filePath)
        ? null
        : JsonSerializer.Deserialize<StoredSelection>(File.ReadAllText(_filePath));

    private void Write(StoredSelection selection)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporaryPath = _filePath + $".{Environment.ProcessId}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(selection));
        File.Move(temporaryPath, _filePath, overwrite: true);
    }

    private sealed record StoredSelection
    {
        public string? Path { get; init; }

        public IReadOnlyList<string> Paths { get; init; } = [];

        public DateTimeOffset SelectedAt { get; init; }
    }
}
