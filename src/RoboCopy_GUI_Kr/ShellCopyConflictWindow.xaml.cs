using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr;

public partial class ShellCopyConflictWindow : Window
{
    public ShellCopyConflictWindow(IEnumerable<ShellCopyConflictGroup> groups)
    {
        InitializeComponent();
        Rows = new ObservableCollection<ShellConflictGroupRow>(groups.Select(group =>
            new ShellConflictGroupRow(
                group,
                GetText(group.ExistingDestination is null ? "SelectedFilesConflict" : "ExistingFileConflict"),
                GetText("SourceFile"),
                GetText("ExistingDestinationFile"),
                GetText("SourceLocation"),
                GetText("TargetLocation"),
                CreateDecisionOptions())));
        DataContext = this;
    }

    public ObservableCollection<ShellConflictGroupRow> Rows { get; }

    public IReadOnlyList<ShellConflictResolution> Resolutions { get; private set; } = [];

    private IReadOnlyList<ShellConflictDecisionOption> CreateDecisionOptions() =>
    [
        new(ShellConflictDecision.Skip, GetText("Skip")),
        new(ShellConflictDecision.Overwrite, GetText("Overwrite")),
        new(ShellConflictDecision.KeepBoth, GetText("KeepBoth")),
    ];

    private void SkipAll_Click(object sender, RoutedEventArgs e) => SetAll(ShellConflictDecision.Skip);
    private void OverwriteAll_Click(object sender, RoutedEventArgs e) => SetAll(ShellConflictDecision.Overwrite);
    private void KeepBothAll_Click(object sender, RoutedEventArgs e) => SetAll(ShellConflictDecision.KeepBoth);

    private void SetAll(ShellConflictDecision decision)
    {
        foreach (var row in Rows)
        {
            row.Decision = decision;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var overwriteRows = Rows.Where(row => row.Decision == ShellConflictDecision.Overwrite).ToArray();
        IReadOnlyDictionary<string, string> winners = new Dictionary<string, string>();
        if (overwriteRows.Length > 0)
        {
            var winnerWindow = new ShellOverwriteSelectionWindow(overwriteRows) { Owner = this };
            if (winnerWindow.ShowDialog() != true)
            {
                return;
            }

            winners = winnerWindow.Winners;
        }

        Resolutions = Rows.Select(row => new ShellConflictResolution(
                row.Group,
                row.Decision,
                winners.GetValueOrDefault(row.Group.DestinationPath)))
            .ToArray();
        DialogResult = true;
    }

    private string GetText(string key) => FindResource(key) as string ?? key;
}

public sealed record ShellConflictDecisionOption(ShellConflictDecision Decision, string Label);

public sealed class ShellConflictGroupRow : INotifyPropertyChanged
{
    private ShellConflictDecision _decision;

    public ShellConflictGroupRow(
        ShellCopyConflictGroup group,
        string conflictType,
        string sourceLabel,
        string existingLabel,
        string sourceLocationLabel,
        string targetLocationLabel,
        IReadOnlyList<ShellConflictDecisionOption> decisionOptions)
    {
        Group = group;
        ConflictType = conflictType;
        DecisionOptions = decisionOptions;
        var files = group.Sources.Select(source => new ShellConflictFileRow(
                sourceLabel,
                sourceLocationLabel,
                targetLocationLabel,
                source.SourcePath,
                source.DestinationPath,
                PathDisplayFormatter.Compact(source.SourcePath),
                PathDisplayFormatter.Compact(source.DestinationPath),
                FormatSize(source.Size)))
            .ToList();
        if (group.ExistingDestination is { } existing)
        {
            files.Insert(0, new ShellConflictFileRow(
                existingLabel,
                sourceLocationLabel,
                targetLocationLabel,
                existing.Path,
                existing.Path,
                PathDisplayFormatter.Compact(existing.Path),
                PathDisplayFormatter.Compact(existing.Path),
                FormatSize(existing.Size)));
        }

        Files = files;
    }

    public ShellCopyConflictGroup Group { get; }
    public string FileName => Group.FileName;
    public string CountText => string.Format(CultureInfo.CurrentCulture, "{0:N0}", Files.Count);
    public string ConflictType { get; }
    public IReadOnlyList<ShellConflictFileRow> Files { get; }
    public IReadOnlyList<ShellConflictDecisionOption> DecisionOptions { get; }

    public ShellConflictDecision Decision
    {
        get => _decision;
        set
        {
            if (_decision == value) return;
            _decision = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Decision)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string FormatSize(long size)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var value = (double)size;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return index == 0 ? $"{size:N0} B" : $"{value:N1} {units[index]}";
    }
}

public sealed record ShellConflictFileRow(
    string Kind,
    string SourceLabel,
    string DestinationLabel,
    string SourcePath,
    string DestinationPath,
    string SourceDisplayPath,
    string DestinationDisplayPath,
    string SizeText);
