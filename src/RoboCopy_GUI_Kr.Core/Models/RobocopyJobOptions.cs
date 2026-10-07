namespace RoboCopy_GUI_Kr.Core.Models;

/// <summary>
/// 한 번의 Robocopy 실행에 필요한 안전한 기본 옵션입니다.
/// </summary>
public sealed record RobocopyJobOptions
{
    public JobOperationMode OperationMode { get; init; } = JobOperationMode.Copy;

    public required string SourceDirectory { get; init; }

    public required string DestinationDirectory { get; init; }

    public int MultiThreadCount { get; init; } = 8;

    public int RetryCount { get; init; } = 3;

    public int RetryWaitSeconds { get; init; } = 5;

    public bool Restartable { get; init; } = true;

    public bool IncludeEmptyDirectories { get; init; }

    public bool RecurseSubdirectories { get; init; } = true;

    public IReadOnlyList<string> FileSpecifications { get; init; } = Array.Empty<string>();

    public bool ExcludeZeroByteFiles { get; init; }

    public bool DryRun { get; init; }

    public SymbolicLinkPolicy SymbolicLinkPolicy { get; init; } = SymbolicLinkPolicy.Exclude;

    public IReadOnlyList<string> ExcludedDirectories { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ExcludedFiles { get; init; } = Array.Empty<string>();

    public string? LogFilePath { get; init; }

    public string? DestinationFileName { get; init; }

    public bool OverwriteDestination { get; init; }

    public bool SkipExistingFiles { get; init; }
}
