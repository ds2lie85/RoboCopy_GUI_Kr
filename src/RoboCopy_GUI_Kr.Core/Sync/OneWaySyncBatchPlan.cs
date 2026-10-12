using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Sync;

public sealed class OneWaySyncBatchPlan
{
    private readonly Dictionary<Guid, IReadOnlyList<OneWaySyncExtraItem>> _byJob;
    public IReadOnlyList<OneWaySyncExtraItem> Items { get; }

    private OneWaySyncBatchPlan(Dictionary<Guid, IReadOnlyList<OneWaySyncExtraItem>> byJob,
        IReadOnlyList<OneWaySyncExtraItem> items)
    {
        _byJob = byJob;
        Items = items;
    }

    public static OneWaySyncBatchPlan Create(IReadOnlyDictionary<Guid, RobocopyJobOptions> jobs,
        CancellationToken cancellationToken = default)
    {
        var planner = new OneWaySyncPlanner();
        var byJob = new Dictionary<Guid, IReadOnlyList<OneWaySyncExtraItem>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var root = CommonDestinationRoot(jobs.Values.Select(job => job.DestinationDirectory));
        foreach (var (id, options) in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byJob[id] = planner.FindDestinationOnlyItems(options, cancellationToken)
                // An item supplied by another job in this batch is not destination-only.
                .Where(item => !jobs.Values.Any(job => ExistsInSource(job, item)))
                .Where(item => seen.Add(Path.GetFullPath(item.DestinationPath)))
                .ToArray();
        }
        var items = byJob.Values.SelectMany(items => items)
            .Select(item => item with { RelativePath = root is null
                ? item.DestinationPath : Path.GetRelativePath(root, item.DestinationPath) })
            .OrderBy(item => item.RelativePath, StringComparer.CurrentCultureIgnoreCase).ToArray();
        return new OneWaySyncBatchPlan(byJob, items);
    }

    public IReadOnlyDictionary<Guid, IReadOnlyList<OneWaySyncExtraItem>> ApplySelection(
        IEnumerable<OneWaySyncExtraItem> selected)
    {
        var paths = selected.Select(item => Path.GetFullPath(item.DestinationPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Empty selections are recorded too: "keep all" is a completed decision.
        return _byJob.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<OneWaySyncExtraItem>)pair.Value
                .Where(item => paths.Contains(Path.GetFullPath(item.DestinationPath))).ToArray());
    }

    private static bool ExistsInSource(RobocopyJobOptions job, OneWaySyncExtraItem item)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(job.DestinationDirectory));
        var path = Path.GetFullPath(item.DestinationPath);
        if (!path.StartsWith(DirectoryPrefix(root), StringComparison.OrdinalIgnoreCase)) return false;
        var relative = Path.GetRelativePath(root, path);
        var source = Path.Combine(job.SourceDirectory, relative);
        return item.IsDirectory ? Directory.Exists(source) : File.Exists(source);
    }

    private static string? CommonDestinationRoot(IEnumerable<string> paths)
    {
        string? common = null;
        foreach (var path in paths.Select(Path.GetFullPath))
        {
            if (common is null) { common = Path.TrimEndingDirectorySeparator(path); continue; }
            while (!path.Equals(common, StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith(DirectoryPrefix(common),
                    StringComparison.OrdinalIgnoreCase))
            {
                common = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(common));
                if (common is null) return null;
            }
        }
        return common;
    }

    private static string DirectoryPrefix(string path) => Path.EndsInDirectorySeparator(path)
        ? path : path + Path.DirectorySeparatorChar;
}
