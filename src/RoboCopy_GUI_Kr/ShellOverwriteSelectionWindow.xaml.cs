using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr;

public partial class ShellOverwriteSelectionWindow : Window
{
    public ShellOverwriteSelectionWindow(IEnumerable<ShellConflictGroupRow> groups)
    {
        InitializeComponent();
        Rows = new ObservableCollection<ShellOverwriteGroupRow>(groups.Select(group =>
            new ShellOverwriteGroupRow(group, GetText("ExistingDestinationFile"))));
        DataContext = this;
    }

    public ObservableCollection<ShellOverwriteGroupRow> Rows { get; }

    public IReadOnlyDictionary<string, string> Winners { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Winners = Rows.ToDictionary(
            row => row.DestinationPath,
            row => row.SelectedCandidate.Path,
            StringComparer.OrdinalIgnoreCase);
        DialogResult = true;
    }

    private string GetText(string key) => FindResource(key) as string ?? key;
}

public sealed class ShellOverwriteGroupRow
{
    public ShellOverwriteGroupRow(ShellConflictGroupRow row, string existingLabel)
    {
        FileName = row.FileName;
        DestinationPath = row.Group.DestinationPath;
        DestinationDisplayPath = PathDisplayFormatter.Compact(DestinationPath, 100);
        var candidates = row.Group.Sources.Select(source => new ShellOverwriteCandidate(
                source.SourcePath,
                Path.GetFileName(source.SourcePath),
                FormatSize(source.Size),
                FormatDate(source.ModifiedAt),
                FormatDate(source.CreatedAt),
                PathDisplayFormatter.Compact(source.SourcePath, 72),
                source.SourcePath))
            .ToList();
        if (row.Group.ExistingDestination is { } existing)
        {
            candidates.Insert(0, new ShellOverwriteCandidate(
                existing.Path,
                $"[{existingLabel}] {Path.GetFileName(existing.Path)}",
                FormatSize(existing.Size),
                FormatDate(existing.ModifiedAt),
                FormatDate(existing.CreatedAt),
                PathDisplayFormatter.Compact(existing.Path, 72),
                existing.Path));
        }

        Candidates = candidates;
        SelectedCandidate = candidates[^1];
    }

    public string FileName { get; }
    public string DestinationPath { get; }
    public string DestinationDisplayPath { get; }
    public IReadOnlyList<ShellOverwriteCandidate> Candidates { get; }
    public ShellOverwriteCandidate SelectedCandidate { get; set; }

    private static string FormatDate(DateTime value) => value == default
        ? "-"
        : value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

    private static string FormatSize(long size)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var value = (double)size;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return index == 0 ? $"{size:N0} B" : $"{value:N1} {units[index]}";
    }
}

public sealed record ShellOverwriteCandidate(
    string Path,
    string FileName,
    string SizeText,
    string ModifiedAtText,
    string CreatedAtText,
    string AddressDisplay,
    string FullAddress);
