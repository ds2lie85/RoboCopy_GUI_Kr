using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Profiles;

public sealed record WorkProfile
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string SourceDirectory { get; init; }

    public required string DestinationDirectory { get; init; }

    public string OperationMode { get; init; } = "Copy";

    public bool UseAutomaticMultiThread { get; init; } = true;

    public int MultiThreadCount { get; init; } = 8;

    public int RetryCount { get; init; } = 3;

    public int RetryWaitSeconds { get; init; } = 5;

    public bool Restartable { get; init; } = true;

    public bool IncludeEmptyDirectories { get; init; }

    public bool ExcludeZeroByteFiles { get; init; }

    public bool SaveLog { get; init; } = true;

    public SymbolicLinkPolicy SymbolicLinkPolicy { get; init; } = SymbolicLinkPolicy.Exclude;

    public IReadOnlyList<string> ExcludedDirectories { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ExcludedFiles { get; init; } = Array.Empty<string>();

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}

